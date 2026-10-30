# BallisticNG Archipelago

An unofficial Archipelago integration for BallisticNG's AGL 2159 campaign.

Race to send items across the multiworld, then use received invitations, track permits, speed licenses, ships and appearances to open more events. To become **AGL Champion**, unlock and win AGL 2159 Finale A, B and C at the Zen Zeus Invitational. Each finale needs its own Entry item and the relevant access requirements.

**v0.37.4 — local test build, not published.** Native code compiles against the installed Windows/Steam game assemblies. The new medal checks and consumables still need live gameplay verification. The intermittent crash when returning from the idle menu demo remains unresolved. See [known issues](docs/KNOWN-ISSUES.md).

**AI disclosure:** ChatGPT/Codex generated and revised substantial portions of this implementation, tests and documentation. The owner directed design and performed live tests; independent human code review has not been completed. Read the [full disclosure and suggested pinned text](AI-DISCLOSURE.md).

## Install

1. Install Python 3.11 or newer with the Windows Python launcher (`py`).
2. With the game and AP client closed, copy the release's `BallisticNGArchipelago` folder into `BallisticNG/User/Mods/Code Mods/`. For a clean install, move the old mod folder outside Code Mods and keep it as a backup before copying the new folder. Do not enable two copies.
3. Enable **BallisticNGArchipelago** in the game's code-mod menu. Open F2 at the main menu, enter server, port and slot, and Connect. First Connect prepares a private Python environment and downloads its dependency.
4. Use **Restart into connected AP save** when prompted. F3 opens **Chat, Hints & Log**.

The player ZIP includes a short plain-text [START-HERE.txt](START-HERE.txt). Use `ballisticng.apworld` and `BallisticNG.yaml` when generating your own room; players connecting to someone else's compatible room do not need to generate another seed. No BepInEx installation is required.

## Included

- Campaign invitations, track permits, speed licenses, ships and base-game ship appearances.
- Random starting ship and starting Breeze access.
- Bronze, Silver, and Gold checks for all 101 supported events: 303 locations. Higher medals award lower checks too. Progression for any game is restricted to tiers at or below the selected target.
- Separate Finale A/B/C Entry items and a goal requiring all three overall wins.
- Campaign availability/hint indicators, item notifications, chat and hint requests.
- Optional traps, AI presets and the optional campaign barrel roll ability.
- 36 shuffled Autopilot Assists: eight seconds each, unavailable in Time Trial or Survival. No overlapping autopilot activations.
- Optional local Golden Race Pass rewards for first Platinum results per event and seed. Each pass clears one event or the current tournament round; finales and Survival are excluded by default.
- Separate local saves per seed and slot, including return to the normal save without a server connection.

Expansion campaigns, AGL 2280, hint rewards, and a separate Platinum AP location are not included.

## Medal checks and consumables

Generate a **fresh seed** with this apworld for universal checks and consumables. Protocol 31 explicitly selects the new location schema; existing v0.36.1 seeds retain their original 125 checks and do not gain these features. Older clients reject the new protocol. The new locations use a separate ID range; do not reuse an old room/seed identity with different checks.

The 303-item pool retains all 67 appearances and existing unlocks/ships. It adds 36 Autopilot Assists. When traps are enabled, all 160 remaining filler slots contain traps and no Pit Crew Encouragement remains. Each enabled trap type appears at least once; remaining copies are random. With traps off, those 160 slots contain Pit Crew Encouragement. The new pool requires a newly generated seed; existing rooms keep their generated item placements.

Press **F4** at the main menu or while fully paused for a dedicated Autopilot Assist control panel. Use **Change** beside Keyboard or Controller and press the desired key/button. To use right-stick click, press the stick inward during controller binding; generic names such as Button 10 are supported. Conflicting game bindings are refused. F2 remains connection/save controls, and F3 chat now has an opaque background.

During racing, a small bottom-left `Assist N` / `Pass N` display shows inventory with no binding text or activation messages. While an Assist is active, its count includes a subtle remaining-seconds countdown. When you have a pass, click its small count button after countdown to use it. Passes have no keyboard/controller binding. Earning a pass shows the same notification card used for items. Assist activation plays the native engaged voice and calculating loop; completion stops the loop and plays disengaged. The loop pauses with the game and stops on exit or native-autopilot takeover.

The [per-event Platinum audit](docs/PLATINUM-AUDIT.md) verifies native reward paths for all 101 supported events (78 hard-mode results, 11 Time Trials, 12 Survival events). This is code/data verification rather than complete live testing.

Golden Race Pass rewards are enabled by default and remain local; they never fill a shuffled item slot. Historical normal-save Platinums do not grant passes. A pass-generated event attempt cannot grant a Platinum pass, including the remaining rounds of the same tournament. A clean new attempt can earn its first genuine Platinum later. When Survival passes are explicitly enabled, a pass reaches Gold and ends the run.

Autopilot Assist cannot overlap native autopilot or Backseat Gaming. There is no inventory cap beyond items actually received, and activation consumes one item. Finish, elimination, exit, or disconnection ends a mod-owned assist. Spent counts and first-Platinum rewards are stored in `User/ArchipelagoSaves/slots/<profile-key>/consumables-v1.txt`, with a checksum and previous-file backup. A damaged or decreased item history disables consumables instead of resetting the balance. Preserve this ledger when backing up an AP save. An interruption after consumption but before an effect can lose that use; it cannot replay a spent item.

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

Hosted rooms: use wss://archipelago.gg in the Server field and the room port in the separate Port field. Bare hosted names now select wss automatically; verify connection in game. Explicit ws:// remains available for local servers.
