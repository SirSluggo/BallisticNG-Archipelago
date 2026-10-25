# Known issues — v0.36.1

## Idle-demo crash (open)

The game can crash when leaving the main-menu attract/idle demo. Crashes have occurred on both normal and AP saves. F2 visibility was not recorded for every incident. Logs show a native Unity crash after ship-file unloading; menu-selection exceptions and workshop shader/material warnings are clues rather than an established cause.

Roughly 20 demos ran without a crash with the AP mod disabled, including some workshop content. An AP mod interaction is therefore under investigation. No fix is claimed, and being connected has not been verified to prevent the demo or the crash.

## Platform and Cloud limits

Windows/Steam is the tested configuration. Current launcher paths, process checks and .cmd helpers are Windows-oriented. Linux/Proton support is unverified.

Single-computer Cloud tests preserved normal save files and AP medals, without AP paths appearing in the inspected Steam cache. This does not establish safety across computers. AP profiles and bridge history remain local; Steam Cloud is not a transfer mechanism for them.

## Validation status

Substantial campaign progress has been made without errors during tested gameplay. A full-seed regression on this build is in progress. Independent human code review has not been completed, and community moderator approval is not claimed.

Expansion campaigns, AGL 2280, Golden Race Passes and Platinum bonus rewards are not implemented. Debug marker files are excluded from player distributions.
