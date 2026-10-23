@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-DeviceCheck.ps1" -PerformanceOnly
if errorlevel 1 echo Performance check failed. Preserve the output folder and player.log.
pause
