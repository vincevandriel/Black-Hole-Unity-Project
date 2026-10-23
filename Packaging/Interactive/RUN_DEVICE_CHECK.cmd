@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-DeviceCheck.ps1"
if errorlevel 1 echo Device check failed. Preserve the output folder and player.log.
pause
