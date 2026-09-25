using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DiscordSleepCallKeepalive
{
    internal static class Program
    {
        private const int DefaultIdleThresholdMinutes = 165;
        private const ushort VkF15 = 0x7E;
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const uint DesktopSwitchDesktop = 0x0100;

        private static readonly string AppDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiscordSleepCallKeepalive");

        private static readonly string LogPath = Path.Combine(AppDirectory, "DiscordKeepAlive.log");
        private static readonly string StatusPath = Path.Combine(AppDirectory, "status.txt");
        private static readonly string SettingsPath = Path.Combine(AppDirectory, "settings.ini");
        private static readonly string BlocklistPath = Path.Combine(AppDirectory, "blocked-processes.txt");

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Directory.CreateDirectory(AppDirectory);
                RotateLogIfNeeded();

                string mode = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "--run";
                if (mode == "--version")
                {
                    Console.WriteLine("Discord Sleep-Call Keepalive 1.1.1");
                    return 0;
                }

                if (mode == "--self-test")
                {
                    int actualSize = Marshal.SizeOf(typeof(Input));
                    int expectedSize = GetExpectedInputSize();
                    string message = actualSize == expectedSize
                        ? "PASS: native INPUT layout is " + actualSize + " bytes (expected " + expectedSize + ")."
                        : "FAIL: native INPUT layout is " + actualSize + " bytes (expected " + expectedSize + ").";
                    File.WriteAllText(StatusPath, message + Environment.NewLine, new UTF8Encoding(false));
                    Console.WriteLine(message);
                    return actualSize == expectedSize ? 0 : 3;
                }

                Settings settings = Settings.Load(SettingsPath);
                Evaluation evaluation = Evaluate(settings);

                if (mode == "--status" || mode == "--diagnose")
                {
                    string status = BuildStatus(settings, evaluation);
                    File.WriteAllText(StatusPath, status, new UTF8Encoding(false));
                    Console.Write(status);
                    Log("STATUS", evaluation.Summary);
                    return 0;
                }

                if (mode == "--test-input")
                {
                    bool sent = SendF15();
                    string testMessage = sent
                        ? "Manual F15 input test succeeded."
                        : "Manual F15 input test failed. Win32 error " + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture) + ".";
                    Log(sent ? "TEST" : "ERROR", testMessage);
                    File.WriteAllText(StatusPath, testMessage + Environment.NewLine, new UTF8Encoding(false));
                    Console.WriteLine(testMessage);
                    return sent ? 0 : 2;
                }

                if (mode != "--run")
                {
                    Console.Error.WriteLine("Unknown option: " + mode);
                    Console.Error.WriteLine("Valid options: --run, --status, --test-input, --self-test, --version");
                    return 64;
                }

                if (!evaluation.ShouldSend)
                {
                    if (settings.LogSkippedRuns)
                    {
                        Log("SKIP", evaluation.Summary);
                    }

                    return 0;
                }

                if (SendF15())
                {
                    Log("SENT", "F15 keepalive sent after " + FormatMinutes(evaluation.IdleMinutes) + " of genuine user inactivity.");
                    return 0;
                }

                int error = Marshal.GetLastWin32Error();
                Log("ERROR", "SendInput failed with Win32 error " + error.ToString(CultureInfo.InvariantCulture) + ".");
                return 2;
            }
            catch (Exception ex)
            {
                TryLogFatal(ex);
                return 1;
            }
        }

        private static Evaluation Evaluate(Settings settings)
        {
            Evaluation result = new Evaluation();
            result.IdleMinutes = GetIdleMilliseconds() / 60000.0;
            result.DiscordRunning = IsDiscordRunning();
            result.SessionInteractive = IsInteractiveDesktopAvailable();
            result.FullScreenSuppression = settings.SkipWhenFullScreen && IsFullScreenOrPresentationActive();
            result.BlockedProcess = FindBlockedProcess();

            if (!settings.Enabled)
            {
                result.Summary = "Disabled in settings.ini.";
            }
            else if (!result.DiscordRunning)
            {
                result.Summary = "Discord is not running.";
            }
            else if (!result.SessionInteractive)
            {
                result.Summary = "The interactive desktop is unavailable or the Windows session is locked.";
            }
            else if (result.FullScreenSuppression)
            {
                result.Summary = "A full-screen Direct3D app or presentation mode is active.";
            }
            else if (!String.IsNullOrEmpty(result.BlockedProcess))
            {
                result.Summary = "Optional blocked process is running: " + result.BlockedProcess + ".";
            }
            else if (result.IdleMinutes < settings.IdleThresholdMinutes)
            {
                result.Summary = "Idle for " + FormatMinutes(result.IdleMinutes) + "; threshold is " + settings.IdleThresholdMinutes.ToString(CultureInfo.InvariantCulture) + " minutes.";
            }
            else
            {
                result.ShouldSend = true;
                result.Summary = "Ready to send F15 after " + FormatMinutes(result.IdleMinutes) + " of genuine user inactivity.";
            }

            return result;
        }

        private static string BuildStatus(Settings settings, Evaluation evaluation)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("Discord Sleep-Call Keepalive status");
            output.AppendLine("-----------------------------------");
            output.AppendLine("Enabled:                 " + settings.Enabled);
            output.AppendLine("Discord running:         " + evaluation.DiscordRunning);
            output.AppendLine("Interactive desktop:     " + evaluation.SessionInteractive);
            output.AppendLine("Idle time:               " + FormatMinutes(evaluation.IdleMinutes));
            output.AppendLine("Idle threshold:          " + settings.IdleThresholdMinutes + " minutes");
            output.AppendLine("Native INPUT size:       " + Marshal.SizeOf(typeof(Input)) + " bytes (expected " + GetExpectedInputSize() + ")");
            output.AppendLine("Full-screen suppression: " + evaluation.FullScreenSuppression);
            output.AppendLine("Blocked process:         " + (String.IsNullOrEmpty(evaluation.BlockedProcess) ? "None" : evaluation.BlockedProcess));
            output.AppendLine("Decision:                " + (evaluation.ShouldSend ? "SEND" : "SKIP"));
            output.AppendLine("Reason:                  " + evaluation.Summary);
            output.AppendLine("Log:                     " + LogPath);
            return output.ToString();
        }

        private static bool IsDiscordRunning()
        {
            string[] names = { "Discord", "DiscordCanary", "DiscordPTB", "Vesktop" };
            foreach (string name in names)
            {
                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    if (processes != null)
                    {
                        foreach (Process process in processes)
                        {
                            process.Dispose();
                        }
                    }
                }
            }

            return false;
        }

        private static string FindBlockedProcess()
        {
            if (!File.Exists(BlocklistPath))
            {
                return null;
            }

            HashSet<string> blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(BlocklistPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    line = line.Substring(0, line.Length - 4);
                }

                blocked.Add(line);
            }

            foreach (string name in blocked)
            {
                Process[] processes = null;
                try
                {
                    processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0)
                    {
                        return name + ".exe";
                    }
                }
                finally
                {
                    if (processes != null)
                    {
                        foreach (Process process in processes)
                        {
                            process.Dispose();
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsFullScreenOrPresentationActive()
        {
            QueryUserNotificationState state;
            int hr = SHQueryUserNotificationState(out state);
            if (hr != 0)
            {
                return false;
            }

            return state == QueryUserNotificationState.RunningD3dFullScreen
                || state == QueryUserNotificationState.PresentationMode;
        }

        private static bool IsInteractiveDesktopAvailable()
        {
            IntPtr desktop = OpenInputDesktop(0, false, DesktopSwitchDesktop);
            if (desktop == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return SwitchDesktop(desktop);
            }
            finally
            {
                CloseDesktop(desktop);
            }
        }

        private static ulong GetIdleMilliseconds()
        {
            LastInputInfo info = new LastInputInfo();
            info.CbSize = (uint)Marshal.SizeOf(typeof(LastInputInfo));
            if (!GetLastInputInfo(ref info))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "GetLastInputInfo failed.");
            }

            uint now = unchecked((uint)Environment.TickCount);
            return unchecked(now - info.DwTime);
        }

        private static bool SendF15()
        {
            int inputSize = Marshal.SizeOf(typeof(Input));
            int expectedSize = GetExpectedInputSize();
            if (inputSize != expectedSize)
            {
                throw new InvalidOperationException(
                    "Native INPUT structure is " + inputSize.ToString(CultureInfo.InvariantCulture)
                    + " bytes; Windows expects " + expectedSize.ToString(CultureInfo.InvariantCulture) + " bytes.");
            }

            Input[] inputs = new Input[2];
            inputs[0].Type = InputKeyboard;
            inputs[0].Union.Keyboard = new KeyboardInput
            {
                VirtualKey = VkF15,
                ScanCode = 0,
                Flags = 0,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            };

            inputs[1].Type = InputKeyboard;
            inputs[1].Union.Keyboard = new KeyboardInput
            {
                VirtualKey = VkF15,
                ScanCode = 0,
                Flags = KeyEventKeyUp,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            };

            uint sent = SendInput((uint)inputs.Length, inputs, inputSize);
            return sent == (uint)inputs.Length;
        }

        private static int GetExpectedInputSize()
        {
            return IntPtr.Size == 8 ? 40 : 28;
        }

        private static void Log(string category, string message)
        {
            Directory.CreateDirectory(AppDirectory);
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " | " + category.PadRight(6) + " | " + message + Environment.NewLine;
            File.AppendAllText(LogPath, line, new UTF8Encoding(false));
        }

        private static void RotateLogIfNeeded()
        {
            try
            {
                if (!File.Exists(LogPath))
                {
                    return;
                }

                FileInfo file = new FileInfo(LogPath);
                if (file.Length <= 1024 * 1024)
                {
                    return;
                }

                string previous = LogPath + ".previous";
                if (File.Exists(previous))
                {
                    File.Delete(previous);
                }

                File.Move(LogPath, previous);
            }
            catch
            {
                // Log rotation must never prevent the keepalive check.
            }
        }

        private static void TryLogFatal(Exception ex)
        {
            try
            {
                Log("FATAL", ex.GetType().Name + ": " + ex.Message);
            }
            catch
            {
                // Nothing else is safe to do if logging itself fails.
            }
        }

        private static string FormatMinutes(double minutes)
        {
            if (minutes < 2.0)
            {
                return minutes.ToString("0.0", CultureInfo.InvariantCulture) + " minutes";
            }

            int totalMinutes = (int)Math.Floor(minutes);
            int hours = totalMinutes / 60;
            int remaining = totalMinutes % 60;
            if (hours == 0)
            {
                return totalMinutes.ToString(CultureInfo.InvariantCulture) + " minutes";
            }

            return hours.ToString(CultureInfo.InvariantCulture) + "h " + remaining.ToString(CultureInfo.InvariantCulture) + "m";
        }

        private sealed class Evaluation
        {
            public bool DiscordRunning;
            public bool SessionInteractive;
            public bool FullScreenSuppression;
            public string BlockedProcess;
            public double IdleMinutes;
            public bool ShouldSend;
            public string Summary;
        }

        private sealed class Settings
        {
            public bool Enabled = true;
            public int IdleThresholdMinutes = DefaultIdleThresholdMinutes;
            public bool SkipWhenFullScreen = true;
            public bool LogSkippedRuns = false;

            public static Settings Load(string path)
            {
                Settings settings = new Settings();
                if (!File.Exists(path))
                {
                    return settings;
                }

                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int equals = line.IndexOf('=');
                    if (equals <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    bool boolValue;
                    int intValue;

                    if (key.Equals("Enabled", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out boolValue))
                    {
                        settings.Enabled = boolValue;
                    }
                    else if (key.Equals("IdleThresholdMinutes", StringComparison.OrdinalIgnoreCase)
                        && Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue)
                        && intValue >= 30 && intValue <= 240)
                    {
                        settings.IdleThresholdMinutes = intValue;
                    }
                    else if (key.Equals("SkipWhenFullScreen", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out boolValue))
                    {
                        settings.SkipWhenFullScreen = boolValue;
                    }
                    else if (key.Equals("LogSkippedRuns", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out boolValue))
                    {
                        settings.LogSkippedRuns = boolValue;
                    }
                }

                return settings;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint CbSize;
            public uint DwTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;

            // INPUT's native union is sized by its largest member. On 64-bit
            // Windows MOUSEINPUT is 32 bytes, making INPUT 40 bytes total.
            // Omitting this member makes Marshal.SizeOf(INPUT) too small and
            // causes SendInput to fail with ERROR_INVALID_PARAMETER (87).
            [FieldOffset(0)]
            public MouseInput Mouse;

            [FieldOffset(0)]
            public HardwareInput Hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HardwareInput
        {
            public uint Message;
            public ushort ParameterLow;
            public ushort ParameterHigh;
        }

        private enum QueryUserNotificationState
        {
            NotPresent = 1,
            Busy = 2,
            RunningD3dFullScreen = 3,
            PresentationMode = 4,
            AcceptsNotifications = 5,
            QuietTime = 6,
            App = 7
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

        [DllImport("user32.dll")]
        private static extern bool SwitchDesktop(IntPtr desktop);

        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr desktop);

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out QueryUserNotificationState state);
    }
}
