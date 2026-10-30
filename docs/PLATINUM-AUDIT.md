# Platinum and Golden Race Pass audit

Audit date: 2026-10-26. Scope: the installed Windows/Steam game and the integration's 101 supported AGL 2159 events. This checks native award paths and serialized campaign targets; it is not a claim that every event has been completed live.

**Result: all 101 supported events have a native Platinum route.** With Platinum rewards enabled, the implementation therefore supports up to 101 Golden Race Pass rewards per seed, one for each event's first legitimate Platinum. A tournament is one event and one reward, not a reward per round. Pass-generated/debug attempts and historical normal-save medals do not earn passes.

| Route | Events |
| --- | ---: |
| Native hard-mode placement/team/score result | 78 |
| Time Trial Platinum time | 11 |
| Survival Platinum zone | 12 |
| Total | 101 |

The 78 hard-mode events include 40 single Race events, 13 tournaments, 7 Knockout events, 4 Rush Hour events, 5 Team Races, 5 Eliminators, and 4 Upsurge events. A native Gold result becomes Platinum in hard mode; lower hard-mode results become no award. The menu labels native hard mode as expert. The AP AI preset does not replace this native mode.

Every Time Trial target is finite, positive, and stricter than Gold; every Survival target is finite, positive, and above Gold. All 23 have HardMultiplier 1 in the inspected data. Times below use nine significant digits for identification; the game compares its stored single-precision values with a strict less-than test. Survival uses greater-than-or-equal zones.

All 101 event records matched their manifest campaign/group/index, event name, mode, and track list. None has HasNoAward set. Forced ship and access restrictions still apply. This confirms supported reward paths, not the physical difficulty or live completion of every target.

Native APIs inspected: NgCampaignEvent.CalculateAwardFinalPosition, CalculateAwardTournament, CalculateAwardScore, CalculateAwardSurvivor, CalculateAwardRaceTeamStandings, GetHardmodeAward, CalculateAwardTime; Gamemode.CalculateAwardTime; GmSurvivalBackend. The relevant native game-mode callers and hard-mode menu selection were also checked. No decompiled game source or raw game assets are included in this report.

## Per-event audit

| Campaign | Event | Mode | Platinum condition |
| --- | --- | --- | --- |
| Breeze | Cassandra Introduction Race | Race | First place in native hard mode |
| Breeze | Zephyr Ridge Introduction Race | Race | First place in native hard mode |
| Breeze | Harpstone Introduction Race | Race | First place in native hard mode |
| Breeze | Harpstone Pure Race | Race | First place in native hard mode |
| Breeze | Breeze Tournament | Race | First overall in native hard mode |
| Techno | Aciknovae Introduction Race | Race | First place in native hard mode |
| Techno | Luna Introduction Race | Race | First place in native hard mode |
| Techno | Dover Introduction Race | Race | First place in native hard mode |
| Techno | Luna Pure Race | Race | First place in native hard mode |
| Techno | Techno Tournament | Race | First overall in native hard mode |
| Urban | Ishtar Citadel Introduction Race | Race | First place in native hard mode |
| Urban | Omega Harbour Introduction Race | Race | First place in native hard mode |
| Urban | Utah Project Introduction Race | Race | First place in native hard mode |
| Urban | Utah Project Pure Race | Race | First place in native hard mode |
| Urban | Urban Tournament | Race | First overall in native hard mode |
| Reflex | Nova Split Introduction Race | Race | First place in native hard mode |
| Reflex | Arrivon XI Introduction Race | Race | First place in native hard mode |
| Reflex | Marina Rush Introduction Race | Race | First place in native hard mode |
| Reflex | Arrivon XI Pure Race | Race | First place in native hard mode |
| Reflex | Reflex Tournament | Race | First overall in native hard mode |
| Ascension | Cassandra Reverse Race | Race | First place in native hard mode |
| Ascension | Harpstone Reverse Race | Race | First place in native hard mode |
| Ascension | Luna Reverse Race | Race | First place in native hard mode |
| Ascension | Harpstone Reverse Pure Race | Race | First place in native hard mode |
| Ascension | Dover Reverse Race | Race | First place in native hard mode |
| Ascension | Utah Project Reverse Race | Race | First place in native hard mode |
| Ascension | Ishtar Citadel Reverse Race | Race | First place in native hard mode |
| Ascension | Hyperions Ascension 1v1 | Race | First place in native hard mode |
| Ascension | Ascension A Tournament | Race | First overall in native hard mode |
| Ascension | Ascension B Tournament | Race | First overall in native hard mode |
| Descension | Nova Split Reverse Race | Race | First place in native hard mode |
| Descension | Aciknovae Reverse Race | Race | First place in native hard mode |
| Descension | Omega Harbour Reverse Race | Race | First place in native hard mode |
| Descension | Omega Harbour Pure Race | Race | First place in native hard mode |
| Descension | Arrivon XI Reverse Race | Race | First place in native hard mode |
| Descension | Zephyr Ridge Reverse Race | Race | First place in native hard mode |
| Descension | Marina Rush Reverse Race | Race | First place in native hard mode |
| Descension | Scorpios Descension 1v1 | Race | First place in native hard mode |
| Descension | Decension A Tournament | Race | First overall in native hard mode |
| Descension | Decension B Tournament | Race | First overall in native hard mode |
| Speed Freak | Cassandra Race | Race | First place in native hard mode |
| Speed Freak | Nexuses Speed Freak 1v1 | Race | First place in native hard mode |
| Speed Freak | Aciknovae Double Damage Race | Race | First place in native hard mode |
| Speed Freak | Luna Reverse Afterburner Challenge | Race | First place in native hard mode |
| Speed Freak | Speed Freak A Tournament | Race | First overall in native hard mode |
| Speed Freak | Speed Freak B Tournament | Race | First overall in native hard mode |
| Zen Zeus | Nova Split Afterburner Challenge | Race | First place in native hard mode |
| Zen Zeus | Cassandra Afterburner Challenge | Race | First place in native hard mode |
| Zen Zeus | Utah Project Endurance Race | Race | First place in native hard mode |
| Zen Zeus | Ishtar Citadel Double Damage Race | Race | First place in native hard mode |
| Zen Zeus | AGL 2159 Finale A | Race | First overall in native hard mode |
| Zen Zeus | AGL 2159 Finale B | Race | First overall in native hard mode |
| Zen Zeus | AGL 2159 Finale C | Race | First overall in native hard mode |
| Breeze | G-Teks Cassandra Trial | Time Trial | Time < 57.5331497 seconds |
| Breeze | Scorpios Zephyr Ridge Trial | Time Trial | Time < 73.7303619 seconds |
| Breeze | Hyperions Harpstone Trial | Time Trial | Time < 82.3622894 seconds |
| Techno | Omnicoms Luna Trial | Time Trial | Time < 129.805099 seconds |
| Techno | Diavolts Aciknovae Trial | Time Trial | Time < 151.778595 seconds |
| Techno | Wyverns Dover Trial | Time Trial | Time < 116.871399 seconds |
| Urban | Nexus Omega Trial | Time Trial | Time < 198.636795 seconds |
| Urban | Tenrais Ishtar Trial | Time Trial | Time < 107.153 seconds |
| Urban | G-Teks Utah Trial | Time Trial | Time < 114.3451 seconds |
| Reflex | Hyperions Arrivon XI Trial | Time Trial | Time < 114.052696 seconds |
| Reflex | Omnicoms Nova Split Trial | Time Trial | Time < 113.208298 seconds |
| Breeze | Zephyr Ridge Knockout | Knockout | Last survivor in native hard mode |
| Breeze | Harpstone Knockout | Knockout | Last survivor in native hard mode |
| Techno | Luna Knockout | Knockout | Last survivor in native hard mode |
| Urban | Utah Project Knockout | Knockout | Last survivor in native hard mode |
| Reflex | Marina Rush Knockout | Knockout | Last survivor in native hard mode |
| Descension | Arrivon XI Reverse Knockout | Knockout | Last survivor in native hard mode |
| Speed Freak | Nova Split Pure Knockout | Knockout | Last survivor in native hard mode |
| Breeze | Cassandra Survival | Survival | Reach zone 35 |
| Breeze | Zephyr Ridge Survival | Survival | Reach zone 45 |
| Techno | Aciknovae Survival | Survival | Reach zone 50 |
| Techno | Dover Survival | Survival | Reach zone 50 |
| Urban | Ishtar Citadel Survival | Survival | Reach zone 70 |
| Urban | Omega Harbour Survival | Survival | Reach zone 65 |
| Reflex | Nova Split Survival | Survival | Reach zone 65 |
| Reflex | Arrivon XI Survival | Survival | Reach zone 67 |
| Reflex | Marina Rush Survival | Survival | Reach zone 56 |
| Speed Freak | Harpstone Survival | Survival | Reach zone 60 |
| Speed Freak | Luna Survival | Survival | Reach zone 60 |
| Zen Zeus | Utah Project Survival | Survival | Reach zone 68 |
| Techno | Dover Rush Hour | Rush Hour | Last survivor in native hard mode |
| Ascension | Cassandra Reverse Rush Hour | Rush Hour | Last survivor in native hard mode |
| Descension | Zephyr Ridge Reverse Rush Hour | Rush Hour | Last survivor in native hard mode |
| Zen Zeus | Marina Rush Hour | Rush Hour | Last survivor in native hard mode |
| Ascension | Cassandra Reverse Eliminator | Eliminator | Highest final score in native hard mode |
| Ascension | Ishtar Citadel Reverse Eliminator | Eliminator | Highest final score in native hard mode |
| Descension | Aciknovae Reverse Eliminator | Eliminator | Highest final score in native hard mode |
| Speed Freak | Utah Project Eliminator | Eliminator | Highest final score in native hard mode |
| Zen Zeus | Dover Eliminator | Eliminator | Highest final score in native hard mode |
| Urban | Ishtar Citadel Upsurge | Upsurge | Highest final score in native hard mode |
| Reflex | Marina Rush Team Race | Team Race | First team in native hard mode |
| Ascension | Luna Reverse Team Race | Team Race | First team in native hard mode |
| Ascension | Dover Reverse Upsurge | Upsurge | Highest final score in native hard mode |
| Descension | Nova Split Reverse Team Race | Team Race | First team in native hard mode |
| Descension | Marina Rush Reverse Upsurge | Upsurge | Highest final score in native hard mode |
| Speed Freak | Zephyr Ridge Reverse Team Race | Team Race | First team in native hard mode |
| Zen Zeus | Omega Harbour Team Race | Team Race | First team in native hard mode |
| Zen Zeus | Nova Split Upsurge | Upsurge | Highest final score in native hard mode |

## Inspected file fingerprints

Unity player version: 2020.3.5f1. Game content is identified by the local file hashes below; this audit does not imply compatibility with later builds.

- Managed/Assembly-CSharp.dll: `95a9d16217bbdfa3aa0bf75a96fc1e00ee7a0db233e9343aa8ad8c992a921da2`
- level3: `32749cf30bf05e67c88b914d1cc5ad0d3f30f706dbd7a769a273e455ef9427df`
