# BallisticNG Archipelago

An unofficial Archipelago integration for BallisticNG's AGL 2159 campaign.

Race to send items across the multiworld, then use received invitations, track permits, speed licenses, ships and appearances to open more events. To become **AGL Champion**, unlock and win AGL 2159 Finale A, B and C at the Zen Zeus Invitational. Each finale needs its own Entry item and the relevant access requirements.

**v0.37.4 — development prerelease.** Tested on Windows with the Steam game. An intermittent crash when returning from the idle menu demo remains unresolved. A full-seed regression run on this build is in progress. See [known issues](docs/KNOWN-ISSUES.md).

**AI disclosure:** ChatGPT/Codex generated and revised substantial portions of this implementation, tests and documentation. The owner directed design and performed live tests; independent human code review has not been completed. Read the [full disclosure and suggested pinned text](AI-DISCLOSURE.md).

## Install

1. Install Python 3.11 or newer with the Windows Python launcher (`py`).
2. With the game and AP client closed, copy the release's `BallisticNGArchipelago` folder into `BallisticNG/User/Mods/Code Mods/`. If updating an existing installation, back up the full mod folder first; do not install two copies.
3. Enable **BallisticNGArchipelago** in the game's code-mod menu. Open F2 at the main menu, enter server, port and slot, and Connect. First Connect prepares a private Python environment and downloads its dependency.
4. Use **Restart into connected AP save** when prompted. F3 opens **Chat, Hints & Log**.

The player ZIP includes a short plain-text [START-HERE.txt](START-HERE.txt). Use `ballisticng.apworld` and `BallisticNG.yaml` when generating your own room; players connecting to someone else's compatible room do not need to generate another seed. No BepInEx installation is required.

## Included

- Campaign invitations, track permits, speed licenses, ships and base-game ship appearances.
- Random starting ship and starting Breeze access.
- Bronze, Silver or Gold medal target; separate Survival milestones, with progression restricted to tiers at or below the selected target.
- Separate Finale A/B/C Entry items and a goal requiring all three overall wins.
- Campaign availability/hint indicators, item notifications, chat and hint requests.
- Optional traps, AI presets and campaign barrel rolls.
- Separate local saves per seed and slot, including return to the normal save without a server connection.

Expansion campaigns and AGL 2280 are not included.

## New in v0.37.4

- Bronze, Silver and Gold checks across all 101 supported events: 303 locations. Higher medals also award lower checks.
- 36 Autopilot Assists, lasting eight seconds each, with native audio and a subtle countdown. Configure controls with **F4**.
- Golden Race Passes earned from each event's first Platinum per seed. A pass clears one event or the current tournament round; finales and Survival are excluded by default.
- Small in-race assist/pass counts and a pass-use button, plus an opaque **F3** chat panel.
- With traps enabled, all remaining filler items are traps instead of Pit Crew Encouragement.
- Fixed Golden Race Pass results and final-round tournament elimination checks. Improved campaign barrel-roll handling and hosted server connection defaults.

New medal checks and item pools require a fresh seed. Existing seeds remain compatible and keep their original checks and items. See [release notes](RELEASE-NOTES.md) for details.

## Saves and updates

Normal progress remains in `User/Save`. AP saves are under `User/ArchipelagoSaves`; F2 switches by restarting the game. A clean mod-folder install starts new local connection/history data, while these external save folders remain in place. Back up the old mod folder rather than deleting it if you want to retain its history.

AP saves and bridge history are local and do not automatically transfer between computers. Steam Cloud remained enabled during one Windows save-isolation test; multi-computer behavior has not been tested. Keep backups before resolving Cloud conflicts or moving PCs.

Generated files are organized into `Data`, `Logs` and `Cache`. Persistent received-item history is under `PanelRuntime/bridge-state`; Python lives in `PanelRuntime/.venv`. Do not treat either history or game saves as disposable cache.

## Development and support

- [Support and diagnostic files](docs/SUPPORT.md)
- [Known issues](docs/KNOWN-ISSUES.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Testing record](docs/TESTING.md)
- [Contributing](CONTRIBUTING.md)
- [Release notes](RELEASE-NOTES.md)
- [Publication checklist](docs/PUBLICATION-CHECKLIST.md)

`python tools/run_tests.py` runs the Python suite. `python tools/build_release.py` verifies source/runtime parity and builds matching player and source archives into `dist/`. World-generation audits require a separate compatible Archipelago source checkout and dependencies. BallisticNG performs native C# compilation; syntax checks alone do not validate it.

## License and credits

Original code: [MIT](LICENSE). Harmony's MIT notice is included separately. See [third-party credits](THIRD-PARTY.md). This project is not endorsed by BallisticNG's developers, Archipelago's maintainers, or implementations referenced during development.
