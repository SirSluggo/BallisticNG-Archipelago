@echo off
setlocal
cd /d "%~dp0PanelRuntime"
if not exist ".venv\Scripts\python.exe" (
  py -3 -m venv .venv
  if errorlevel 1 goto failure
)
.venv\Scripts\python.exe -m pip --version >nul 2>&1
if errorlevel 1 (
  .venv\Scripts\python.exe -m ensurepip --upgrade
  if errorlevel 1 goto failure
)
.venv\Scripts\python.exe -m pip install -r requirements.txt
if errorlevel 1 goto failure
echo Setup complete. Start BallisticNG, open F2 at the menu, and Connect.
pause
exit /b 0
:failure
echo Setup failed. Send this error text before proceeding.
pause
exit /b 1
