@echo off
setlocal
cd /d "%~dp0"
set "PS1=%~dp0_setup.ps1"
if not exist "%PS1%" set "PS1=%~dp0payload\_setup.ps1"
if not exist "%PS1%" (
  echo [X] _setup.ps1 not found. Please re-extract the full package.
  pause
  exit /b 1
)
echo Uninstalling MOD, please wait...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%" uninstall %*
echo.
pause
