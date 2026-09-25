<p align="center"><img src="assets/icon.svg" width="84" height="84" alt="Moon and signal icon"></p>

<h1 align="center">Sleep Call Keepalive</h1>

<p align="center"><strong>Keep the quiet hours connected.</strong><br>A small, open-source Windows utility for Discord voice calls affected by long periods of computer inactivity.</p>

<p align="center"><img src="assets/night-call-hero.png" alt="Moonlit desktop with an uninterrupted signal line"></p>

<p align="center"><a href="https://github.com/xdtrammell/Discord-Sleep-Call-Keepalive/releases/latest"><strong>Download for Windows</strong></a> · <a href="#how-it-works">How it works</a> · <a href="#limitations">Limitations</a></p>

Sleep calls, quiet study sessions, and other long voice calls can sometimes end after hours without keyboard or mouse activity. Sleep Call Keepalive addresses one possible cause by sending an F15 key event when Windows has been idle for long enough and Discord is open. It does not change your microphone, mute state, or Discord installation.

## Install

1. Download **Sleep-Call-Keepalive-Windows-v1.1.1.zip** from [the latest release](https://github.com/xdtrammell/Discord-Sleep-Call-Keepalive/releases/latest) and extract it. GitHub's automatic “Source code” archive is a different download.
2. Double-click **Install.cmd**. The installer builds the included source on your PC, checks the native input layout, and creates a scheduled task. It needs no administrator access, AutoHotkey, separate .NET SDK, or game list on a typical 64-bit Windows 10/11 installation.
3. Leave Windows awake and signed in during the call. The display can turn off.

Re-running **Install.cmd** updates an existing installation while keeping its settings and log. **Uninstall.cmd** removes the scheduled task and installed folder, including its settings and logs.

## How it works

Every 15 minutes, Windows Task Scheduler starts a brief check and the program exits. It sends one F15 press and release only when all of these are true:

- A supported Discord desktop process is running (`Discord`, `DiscordPTB`, `DiscordCanary`, or `Vesktop`).
- Windows reports at least **165 minutes** since the last keyboard or mouse input.
- The desktop is unlocked and no detected full-screen Direct3D or presentation mode is active.
- The optional process blocklist has no match.

There is no process running continuously. Ordinary input resets Windows' idle timer; the synthetic F15 event also resets it when sent. F15 normally has no visible effect, but software with an F15 shortcut may react.

## Settings and log

After installation, open `%LOCALAPPDATA%\DiscordSleepCallKeepalive` in File Explorer. Edit `settings.ini` to pause the utility (`Enabled=false`), change `IdleThresholdMinutes` (30–240), or change full-screen suppression and skipped-run logging. Changes apply on the next check. The default avoids logging every routine skip.

The optional `blocked-processes.txt` can be created in that folder with one executable name per line if you need an extra block for a specific program. **You do not need to maintain a game list.**

`DiscordKeepAlive.log` records sent inputs and errors. A `SENT` line means Windows accepted F15; it does not prove a call stayed connected. The installer also writes `status.txt`. For an on-demand status check, run this in PowerShell:

```powershell
& "$env:LOCALAPPDATA\DiscordSleepCallKeepalive\DiscordKeepAlive.exe" --status
Get-Content "$env:LOCALAPPDATA\DiscordSleepCallKeepalive\status.txt"
```

## Limitations

- This can help with **idle-related** disconnects, but cannot guarantee a call will never end. It cannot fix network outages, Discord-side changes, or a PC that sleeps or hibernates.
- It checks for a running Discord process, **not an active call**. Browser-only Discord is not supported.
- Full-screen detection is best effort. An app that binds F15, including a game, could respond to it; disable the scheduled task before playing if that matters to you.
- The locally built executable is **unsigned** and Windows Smart App Control may block it. Smart App Control has no per-app allowlist; do not turn off system-wide protection just for this utility.

The source uses Windows `GetLastInputInfo`, `SendInput`, and `SHQueryUserNotificationState`. It makes no network requests or telemetry. This is an independent community utility, not affiliated with Discord. Licensed under [MIT](LICENSE).
