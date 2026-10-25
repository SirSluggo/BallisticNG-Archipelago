@echo off
setlocal
cd /d "%~dp0"
PanelRuntime\.venv\Scripts\python.exe PanelRuntime\offline_normal.py
if errorlevel 1 (
  echo Return stopped. Read Logs\offline-normal.log before trying again.
  pause
  exit /b 1
)
