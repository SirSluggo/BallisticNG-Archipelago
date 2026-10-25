@echo off
setlocal
cd /d "%~dp0"
py -3 PanelRuntime\maintenance.py
if errorlevel 1 (
  echo Organization stopped. Preserve the files and send the error text.
  pause
  exit /b 1
)
pause
