@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Scripts\Build-Installer.ps1"
set "buildResult=%ERRORLEVEL%"
if "%buildResult%"=="0" (
  echo Installer is ready in the Output folder.
  explorer.exe "%~dp0Output"
) else (
  echo Build failed. See the error above.
)
pause
exit /b %buildResult%
