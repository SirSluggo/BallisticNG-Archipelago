# BallisticNG Archipelago v0.37.4 — local test build

- Completed a per-event native Platinum audit: all 101 supported events have a reward path (78 hard-mode placement/team/score results, 11 Time Trial targets, 12 Survival targets). See docs/PLATINUM-AUDIT.md. This is source/data evidence, not 101 live completions.
- Removed the unreliable automatic right-stick shortcut. F4 keeps the normal keyboard/controller Change controls and existing saved bindings. Press the physical right stick while binding to use it, even when the controller names it Button 10.
- Fixed dark panel-title text in focused/hovered window styles.
- No generation, item-pool, save, or gameplay-rule changes in this patch. Existing seeds remain compatible. Barrel-roll verification, the full-seed regression and the idle-demo crash investigation remain open.

# BallisticNG Archipelago v0.37.3 — local test build

- Final-round tournament elimination now processes the native overall standings. The prior Race-result guard incorrectly rejected eliminated players, and the elimination path did not trigger ordinary finish handling. A player who remains first overall receives Bronze/Silver/Gold checks; second/third overall receives the appropriate lower checks. No checks are awarded from an intermediate round or from a saved historical medal.
- The handler waits for native elimination standings processing and retains session, save, event, track, mode and roster validation. Added current-attempt tournament result diagnostics.
- Please repeat the final-round elimination case and compare overall placement to the AP checks. Existing saved Gold medals are not automatically imported.

# BallisticNG Archipelago v0.37.2 — local test build

- Active Autopilot Assist displays a small remaining-seconds countdown beside the assist inventory count. It reads the native timer, freezes with pause, and disappears when the assist ends. No binding text or extra race messages were added.
- Campaign barrel-roll configuration is now applied again after native race setup and maintained only during a validated AP campaign session. The intention is to remove the reported dependency on enabling the Custom Race barrel-roll modifier. The mod does not change saved Custom Race preferences or enable global cheat flags. Please test with the Custom Race modifier OFF and the seed's barrel_rolls option ON.
- The user confirmed pass and assist balances persisted across disconnect/reconnect in live testing. The right-stick shortcut remains unchanged while the user continues testing.

# BallisticNG Archipelago v0.37.1 — local test build

- Fixed the reported Arrivon XI Introduction Race pass failure: production passes now supply winning placement or score standings before invoking native result processing. They no longer depend on debug-gated ranking helpers or enable global debug mode. Tournament passes still clear only the current round.
- Autopilot Assist now plays native engaged/disengaged voices and the calculating loop, including pause and exit cleanup.
- Moved assist bindings from F2 to a separate F4 panel with keyboard/controller rows. Existing assist bindings are read from the previous two-row settings format. Golden Race Pass bindings were removed.
- Races show only two small bottom-left inventory counts. The pass count becomes a small button while passes are available. No binding labels or activation messages appear in the race overlay. Earning a pass displays an item-style reward card.
- F3 chat now has a solid dark background.
- With traps on, all 160 remaining filler slots are traps, with no Pit Crew Encouragement. Assists remain 36 copies. The pool change requires a new seed; existing seeds keep their item placements.
- Closed-game session cleanup now uses the seed's location schema so universal Survival check keys recover their new IDs rather than legacy IDs.

This is a review/test build, not published. The idle-demo crash remains unresolved. New UI, audio, and pass behavior need live verification; native compilation and regression checks do not replace gameplay testing. Existing v0.37.0 seeds can use the client/mod fixes without regeneration, but their item pools do not change.

# Previous v0.37.0 changes

This build is prepared for review and gameplay testing. It has not been published. The idle-demo return crash remains unresolved.

- All 101 supported campaign events have Bronze, Silver, and Gold checks (303 locations). A higher medal sends lower checks too. Only tiers at or below the selected target may contain progression for any game. Tournament medals use overall standings; goal still requires all three overall finale wins.
- Fresh seeds use protocol 31 and a separate location ID range. Existing v0.36.1 seeds keep their original check definitions and do not gain consumables.
- Golden Race Passes are local rewards for each event's first current-attempt Platinum per seed. One pass clears one event or the current tournament round. Pass-generated attempts cannot reward passes. Master reward toggle defaults on; finale and Survival permissions default off.
- The expanded pool includes 36 Autopilot Assists, lasting eight seconds. No Time Trial/Survival use or overlap with another autopilot. The original v0.37.0 build provided bindings in F2; v0.37.1 replaces that layout as described above.
- The original v0.37.0 pool used 42 trap slots; v0.37.1 replaces all remaining filler with traps when enabled. Each enabled trap type appears at least once. Traps remain off by default.
- Hosted names default to encrypted WebSockets; explicit schemes and local ws connections remain supported. Connection retry diagnostics contain the exception class and transport, without exception messages, credentials, or packets.

Automated validation and native compilation do not replace live gameplay testing. See docs/TESTING.md for the current record and short test sequence. Previous live gameplay observations apply to v0.36.1 and earlier builds, not these new features. Linux and multi-computer Steam Cloud behavior remain unverified.

ChatGPT/Codex generated and revised substantial portions of the code, tests and documentation. The owner directed design and performed earlier live tests. No independent human code review is claimed.
