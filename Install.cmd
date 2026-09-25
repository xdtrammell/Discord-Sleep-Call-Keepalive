@echo off
setlocal
title Discord Sleep-Call Keepalive Installer
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
exit /b %errorlevel%
