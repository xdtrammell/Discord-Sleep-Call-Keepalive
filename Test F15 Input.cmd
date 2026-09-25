@echo off
setlocal
set "APP=%LOCALAPPDATA%\DiscordSleepCallKeepalive\DiscordKeepAlive.exe"
if not exist "%APP%" (
  echo Discord Sleep-Call Keepalive is not installed.
  echo Run Install.cmd first.
  pause
  exit /b 1
)
echo This sends one F15 key-down and key-up event now.
echo It does not modify Discord or your microphone.
echo.
pause
start "" /wait "%APP%" --test-input
type "%LOCALAPPDATA%\DiscordSleepCallKeepalive\status.txt"
echo.
pause
