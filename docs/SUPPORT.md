# Support and troubleshooting

Start with the build shown in F2, operating system, game version, save label (NORMAL/AP), exact action and error text. Is the server reachable? Did a native game process exit or just the connection drop?

## Useful diagnostics

In installed BallisticNGArchipelago/Logs: events.tsv, panel-bridge.log, setup-panel.log for first-connect errors, offline-normal.log for local-return errors, save-isolation.txt for AP routing. Current panel-status.txt and other communication files are under Data/. Under User/ArchipelagoSaves: normal-verification.json, selected-profile.txt and the relevant metadata if requested. Logs can contain local paths, slot/player names and chat; review before posting publicly. Do not request passwords.

## First Connect

Install Python 3.11+ with the Windows launcher. Connect creates its private venv and installs websockets from requirements.txt. If it fails, keep setup-panel.log; use installed Setup-Panel.cmd to see manual repair output. A moved virtual environment may require rebuilding only .venv, with the game/client closed. Do not delete bridge-state or save folders to repair Python.

## Locks and crashes

Current bridge.lock remains on disk intentionally; OS ownership ends when the process exits. Do not delete it while a bridge runs. Never broadly kill unrelated Python processes.

For a game crash, capture Unity's game log before relaunch overwrites it. A Steam launch option can direct it to an explicit writable path:
`-logFile "G:\SteamLibrary\steamapps\common\BallisticNG\User\ap-crash-test.log"`
Replace that example path with the actual installation. Preserve existing launch options; remove the logging option when finished. The idle-demo crash is unresolved.

## Saves

Use F2 restart buttons, not manual folder replacement while running. For offline return, use F2 or Return-To-Normal.cmd. If verification fails, preserve the affected files and baseline; ordinary normal play or Cloud downloads can also change them. Do not treat every hash difference as corruption or blindly restore a backup.

AP profile deletion is destructive. Backup the full relevant User save/profile directories and PanelRuntime/bridge-state before migration or reset. Steam Cloud conflicts need a deliberate choice of intended copy; file dates alone do not prove which copy is correct.
