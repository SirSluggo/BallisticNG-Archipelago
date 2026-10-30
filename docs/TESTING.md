# Larger multiworld generation audit — 2026-10-26

Using AP core 0.6.7 and the v0.37.4 world logic, 240 additional fills passed across 3, 4, 8, and 16 players. Each size received 30 uniform-setting fills and 30 fills with medal/trap settings varied between players: five random seeds for each Bronze/Silver/Gold and traps off/on configuration or rotation. All slots in these tests were BallisticNG, not a mixture of third-party games.

Every fill passed exact item-count/classification checks, rejected progression (including synthetic foreign advancement and skip-balancing items) above the receiving location's medal target, and reached every modeled goal through a collection-state sweep without above-target locations. No fill failures occurred. This is finite sampling of access/item logic, not a proof of every player count, every seed, mixed-game compatibility, network load, or live gameplay completion.

Reproduce with compatible AP core on PYTHONPATH:

```text
python audit_ap_fill.py . 5 --players 3,4,8,16
python audit_ap_fill.py . 5 --players 3,4,8,16 --mixed-settings
```

# v0.37.4 audit and outstanding live checks

The installed native award paths and all 101 campaign records were audited. All support Platinum: 78 hard-mode placement/team/score results, 11 positive time targets, and 12 positive zone targets. See PLATINUM-AUDIT.md for every event, thresholds, evidence scope and inspected file hashes. This does not claim live completion of every event.

User confirmed final-round tournament elimination produced Gold/checks correctly. User confirmed pass and Assist balances persist across reconnect. Barrel rolls with Custom Race modifier OFF have NOT yet been tested. Full exact-build seed regression remains pending. The idle-demo native crash remains open; review of the existing crash log confirms an unsymbolized UnityPlayer access violation after ship unloading, with no decisive AP managed stack. No crash fix is claimed.

F4 no longer has an automatic right-stick shortcut. Use the ordinary Controller Change button and physically press the stick. Existing bindings should be preserved; no gameplay/save changes are introduced by this UI removal.

# v0.37.3 tournament elimination test

In a fresh tournament attempt, lead after the earlier rounds, then become eliminated in the final round. Compare the native overall result to newly sent AP medal checks. Gold overall should send all three; Silver should send Bronze/Silver; Bronze should send Bronze. An early-round elimination must not send tournament checks, and a historical Gold must not upgrade a lower result on a later attempt. Test one normal final-round finish as well. Goal still requires first overall in all three finales and server acknowledgment.

# v0.37.2 focused checks

Use an Assist: confirm a subtle countdown appears next to its inventory count, pauses with the game, and disappears on expiration/exit. No binding labels should appear in the race overlay.

Set Custom Race > Modifiers > Barrel Rolls OFF before entering AP campaign. With the seed's barrel_rolls option ON, test the normal native roll controls (enough airtime and more than 25 shield). Exit/restart and verify Custom Race preferences are unchanged. Also test an AP seed with barrel_rolls OFF. This native setup correction needs live confirmation; no specific native reset has been established as the root cause.

User live observation: pass and Assist inventory persisted across disconnect/reconnect. No broader save or gameplay guarantee follows from this test.

# v0.37.1 validation and live-test checklist

Current automated results: 126 Python tests; 12 regression checks against the production pass completion helper; 19 checks against the production C# consumable ledger; 300 AP 0.6.7 fills across all medal targets, solo/two-player and traps off/on; 1,023 trap-toggle pool combinations plus 192 varied trap-subset fills. These audits check pool sizes, optional-tier progression restrictions (including foreign progression), and goal reachability without optional-tier progression. Native C# compilation against the installed Windows/Steam assemblies passed. YAML was generated with AP 0.6.7's official generator.

Connection-focused checks passed on websockets 14.2 and 15.0.1 before the medal/consumable changes; the current full suite uses 16.1.1. Live hosted-room verification remains pending.

## Short live-test sequence

1. Back up the old mod folder outside Code Mods, install cleanly, and create a fresh test seed. F2 should show the correct AP save and F3 should show 303 checks. Verify a hosted `wss://archipelago.gg` connection, disconnect, and reconnect.
2. Earn Bronze, then Silver, then Gold on a normal race. Confirm each check once; a fresh Gold result should send all three. Repeat for Team Race, Knockout/Rush Hour, Eliminator/Upsurge, Time Trial, and Survival. Test a tournament with different individual/overall places: no tournament checks before the overall result. Test hard-mode lower/first results and threshold Platinums.
3. Earn a legitimate Platinum, restart/reconnect, and verify one pass persists. Repeat that event's Platinum: no extra pass. Set an Assist binding in F4; right-stick click is accepted only when unmapped. Verify an already-mapped pickup/camera button is refused and no consumable is spent.
4. Click the small Pass count button after countdown, starting from a low race position (repeat Arrivon XI Introduction Race). Verify a native Gold medal and all three AP checks in normal mode. Verify one spent use after restart and no new Platinum pass. In a tournament, verify only that round clears, and a later overall Platinum from that assisted attempt gives no pass. Confirm finale/Survival denial by default. With their explicit options enabled, test a finale round and a Survival Gold/end-of-run clear.
5. Receive and use an Assist. Verify engaged voice, calculating loop during the eight seconds, disengaged at the end, one spent item after restart/reconnect, no overlap with Backseat Gaming/native autopilot, and no use in Time Trial/Survival. Exit/restart a race during an assist and verify native controls return. Verify all three overall finale wins still report goal only after server confirmation.
6. Verify F2 has no binding section, F4 is a separate simple panel, F3 chat is opaque, and the race overlay contains only the two small inventory counts. A pass reward should display an item-style card. Generate a new traps-on seed and confirm zero Pit Crew Encouragement items (existing seeds keep their pool).
7. Connect an old v0.36.1 seed: 125 checks, original goal IDs, and no new consumables. Finish with a normal/AP save round trip and compare the normal-save backup.

The idle/attract-demo crash is still open; do not infer a fix from these changes.

## Previous testing record (v0.36.1 and earlier)

# Testing record

## Current build: v0.36.1

Substantial campaign progress has been made without errors during tested gameplay. Native compilation and connection have been verified. A full-seed regression run is in progress; completion is not yet recorded for this exact build. The idle-demo crash remains under investigation.

## Automated checks

The baseline source checkout passed 110 Python tests covering bootstrap setup, bridge/history behavior, notifications, traps, access activity, medal handling, isolated profiles, process locks, offline return, file routing, migration conflicts, log rotation and durable cleanup refusal paths.

Build checks verify runtime/source parity, manifest copies, C# catalog digest, the bundled Harmony hash and matching distributed source bytes. C# syntax and RuntimeFiles.File argument counts were checked separately. These are not a complete C# compiler or runtime analysis.

Bootstrap subprocess tests are simulated. A fresh mod-folder setup was also verified manually on Windows. World-generation and template audits require a separate compatible Archipelago source checkout and dependencies. The option-order audit was checked against Archipelago 0.6.7. Full fill audits were performed during logic development; they are not claimed as a newly run full matrix for this prerelease preparation.

## Live test coverage

The following behaviors have been exercised during development. This is coverage evidence, not a claim that every scenario was repeated on the current build:

- Supported event types, individual finale Entry gates and all-three-finale goal.
- Required-medal checks and Survival milestones, with above-target tiers excluded from progression.
- Item delivery, hints, cosmetics, traps and quiet history restoration.
- Limited two-game multiworld interaction.
- Save persistence across restart, two-seed switching and normal-save return.
- Menu-level forced process-tree termination and reconnect without manually deleting current-format bridge.lock; normal-save comparisons passed. Interruption during a save write was not tested.
- One Windows Cloud-enabled AP/normal round trip, including normal-file comparisons and recorded Steam hashes. No multi-computer guarantee follows.
- Fresh setup, correct-save restart disabled and offline normal return.

Complete seeds reached goal during development. The current build's full-seed regression is still in progress.

## Crash investigation

Game logs captured a native Unity crash after unloading ship files. Menu-selection exceptions and workshop shader warnings are not sufficient to identify the cause. About 20 idle demos with the AP mod disabled did not crash; some included workshop content. This supports investigating a mod interaction without proving a specific bug.

## Commands

From the repository root with requirements installed:

```text
python tools/run_tests.py
python tools/build_release.py
```

Record exact versions, options/seed and observed outcomes for new tests. Automated/AI checks and player tests do not constitute independent human code review.

The bundled BallisticNG.yaml is generated by Archipelago rather than maintained separately. With a compatible Archipelago checkout and its dependencies installed, run `python tools/generate_template.py --ap-source PATH_TO_ARCHIPELAGO` to refresh it, or add `--check` to verify exact parity. Run this after option-description or default changes. The release builder also checks custom-option descriptions/defaults against options.py without needing an AP checkout.

Four additional template-sync tests passed: canonical template acceptance, stale trap-description rejection, common-default mismatch rejection and custom-default mismatch rejection. Bundled template regeneration/check passed using the official Archipelago 0.6.7 generator and template resource; line endings are normalized for portability.
