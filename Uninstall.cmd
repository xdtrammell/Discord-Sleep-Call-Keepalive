@echo off
setlocal
title Discord Sleep-Call Keepalive Uninstaller
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
exit /b %errorlevel%
