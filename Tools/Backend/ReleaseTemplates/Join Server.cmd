@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Join-Server.ps1"
if errorlevel 1 pause
