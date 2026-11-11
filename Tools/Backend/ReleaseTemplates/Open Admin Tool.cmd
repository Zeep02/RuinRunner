@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Open-AdminTool.ps1"
if errorlevel 1 pause
