# BallisticNG Archipelago — first public prerelease draft

Build: v0.36.1. Windows/Steam is the tested configuration.

**Known issue:** returning from the main-menu idle demo can crash the game. Investigation is ongoing. A full-seed regression run on this build is in progress.

## Play the AGL 2159 multiworld

Send checks by earning your chosen Bronze, Silver or Gold medal target. Receive campaign invitations, track permits, speed licenses, ships and ship appearances. Become **AGL Champion** by unlocking and taking first place overall in AGL 2159 Finale A, B and C at the Zen Zeus Invitational.

The integration includes campaign availability and hint indicators, item notifications, in-game chat/hints, optional traps, AI presets and campaign barrel rolls. Survival has separate medal milestones; tiers above the chosen target cannot contain progression items.

## Install

Install Python 3.11+ with the Windows launcher, then copy BallisticNGArchipelago into the game's Code Mods folder and enable it. F2 handles connection and save switching; first Connect sets up the private Python environment. F3 opens Chat, Hints & Log. See START-HERE.txt for the short instructions.

The apworld and sample YAML are included for seed generation. Players joining a hosted compatible room do not need to generate another seed.

## Saves and support scope

Each seed/slot has its own local save. F2 can return to the normal save without a server connection. AP saves and received-item history do not automatically transfer between computers. Back up the full mod folder and relevant User save/profile directories before moving PCs or replacing an existing installation.

Single-computer Steam Cloud behavior has received limited testing. Multi-computer Cloud behavior and Linux/Proton support are unverified. Expansion campaigns, AGL 2280, Golden Race Passes and Platinum bonus rewards are not included.

## Validation and disclosure

Substantial campaign progress has been made without errors during the tested gameplay. The open idle-demo crash is a separate known issue. The current full-seed run remains in progress.

The baseline Python suite passed 110 tests, with four additional template-sync checks also passing. Release/source parity is checked by the build script. These checks do not replace native gameplay testing or independent human code review, which has not been completed.

ChatGPT/Codex was used extensively to develop the implementation, tests and documentation. Read AI-DISCLOSURE.md for the scope, human testing and review limits. Original code is MIT licensed; third-party notices are included.

## Release attachments

The player ZIP and matching source ZIP should be published together with this build's source tag. This document is a prerelease draft; it does not indicate that anything has been published or that Archipelago moderators have approved the implementation.

The bundled YAML uses Archipelago's standard generated format and defaults. Its slot name is Player{number}; use the actual generated slot name when connecting.
