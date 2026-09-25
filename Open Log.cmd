@echo off
setlocal
set "LOG=%LOCALAPPDATA%\DiscordSleepCallKeepalive\DiscordKeepAlive.log"
if not exist "%LOG%" (
  echo No log exists yet. It will be created after the first status check or keepalive.
  pause
  exit /b 1
)
start "" notepad.exe "%LOG%"
