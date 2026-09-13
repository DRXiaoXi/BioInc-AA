@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo 正在启动卸载程序...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0payload\_setup.ps1" uninstall %*
echo.
pause
