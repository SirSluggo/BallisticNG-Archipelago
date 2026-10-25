# Architecture

## Archipelago world

ballisticng/__init__.py creates items, locations, regions and access rules. options.py defines YAML options and their descriptions. medals.py derives Survival milestone locations and medal requirements from manifest.json.

The world tells the generator which locations are reachable. Runtime gates must enforce the same requirements; a working menu alone does not establish correct generation logic. Forced-ship events require their progression ship; optional ships and appearances fill other item slots. Survival tiers above the selected requirement cannot contain progression.

The goal is all three finale tournament wins, not possession of their Entry items alone. Software release versions and bridge protocol 30 are separate values; documentation or UI changes do not themselves require a protocol/catalog change.

## Python bridge and F2 client

bridge.py connects to the Archipelago server via websockets, validates slot/catalog data and received-item history, sends queued checks and publishes local runtime files. panel_client.py handles F2 commands and post-exit switching. bootstrap.py creates/repairs a private venv on first Connect using only standard-library code until dependencies are ready. Server, port and slot are remembered; passwords are sent to the process through stdin and are not intentionally persisted.

The mod and bridge exchange files under the installed mod directory, with session identities and heartbeats. history_recovery.py handles inconsistent item history through an explicit recovery preview. Reconnection replays item history quietly to restore gates without replaying old notifications or already-started traps.

bridge_lock.py uses process-lifetime OS locks. A bridge.lock file remaining on disk does not mean a process still owns it. Do not unlink it while a current client runs.

## Native C# mod

BallisticNG's code-mod loader compiles the .cs files. BallisticNGArchipelago.cs observes race results, checks session/save identity, enforces access and queues check files. Supporting files handle ship filtering, cosmetics, campaign labels, notifications, AI presets, barrel rolls and queued traps. ConnectionPanel.cs and ChatPanel.cs implement F2 and F3.

Native event awards, placement and event identity are checked before generating a result check. Duplicate check delivery is ultimately reconciled by Archipelago's checked locations; local files are not the server's authoritative item history.

## Save selection and isolation

isolated_profiles.py chooses User/ArchipelagoSaves/slots/<identity hash>/Save. It validates profile metadata, snapshots the normal save on normal-to-AP selection and writes the selected profile key. Selection requires the game to be closed and is protected by a process guard. Incomplete/mismatched existing profiles are refused rather than silently replaced with fresh progress.

SaveProfile.cs fixes the profile at boot. On AP boots, SaveRouting.cs loads Harmony and redirects the inspected NeoFs persistence paths under User/Save into the chosen AP folder. Cached campaign/stat/achievement state is reloaded. The normal profile leaves native save paths in use. Routing remains installed until process exit to avoid late save writers returning to the normal folder.

Normal save stays in User/Save. save_profiles.py contains shared identity, file-validation and process-guard helpers, plus compatibility code. The current isolated-profile selector does not move the normal save folder.

offline_normal.py waits for the game to exit, waits for any old client's lock to release, validates/selects normal, and relaunches locally. It does not need the server. Validation failure stops it and produces offline-normal.log rather than overwriting saves.

## Change ownership

- Location/item/option edits: update world logic and manifest coherently, then rerun generation audits.
- Menu or race integration: test native scene changes, event completion and restoration of temporary state.
- Save or launcher changes: test normal/AP round trips, multiple seeds, interrupted exit and error refusal.
- Packaging: runtime duplicates and manifests must match; release source must match the packaged source.

## Generated-file layout
All runtime paths route through runtime_files.py / RuntimeFiles.cs into Data, Logs or Cache. Locks stay at the root so old/new processes coordinate on the same OS objects. PanelRuntime/bridge-state remains persistent. maintenance.py migrates known legacy files while closed and cleans inactive sessions only under exclusive bridge ownership. Never call cleanup concurrently with the network task, which may be updating persistent item history. Unknown sessions are retained.
