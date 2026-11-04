@echo off
setlocal
title CTCCrossing - Update pushen
where pwsh.exe >nul 2>&1
if errorlevel 1 (
    set "publish_shell=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
) else (
    set "publish_shell=pwsh.exe"
)
"%publish_shell%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Publish-Interactive.ps1"
set "publish_result=%errorlevel%"
echo.
pause
exit /b %publish_result%
