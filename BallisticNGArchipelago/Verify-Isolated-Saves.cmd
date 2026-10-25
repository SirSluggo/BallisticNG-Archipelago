@echo off
setlocal
cd /d "%~dp0"
if not exist "PanelRuntime\.venv\Scripts\python.exe" (
  echo Run Setup-Panel.cmd first.
  pause
  exit /b 1
)
"PanelRuntime\.venv\Scripts\python.exe" "PanelRuntime\isolated_profiles.py"
pause
