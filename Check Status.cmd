@echo off
setlocal
set "APP=%LOCALAPPDATA%\DiscordSleepCallKeepalive\DiscordKeepAlive.exe"
if not exist "%APP%" (
  echo Discord Sleep-Call Keepalive is not installed.
  echo Run Install.cmd first.
  pause
  exit /b 1
)
start "" /wait "%APP%" --status
type "%LOCALAPPDATA%\DiscordSleepCallKeepalive\status.txt"
echo.
pause
