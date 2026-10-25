# Credits, dependencies and assets

## Harmony

The mod bundles Andreas Pardeike's Harmony 2.2.2.0, net35 build, in BallisticNGArchipelago/lib/0Harmony.dll. License: MIT; retain licenses/HARMONY-LICENSE.txt in source and player distributions.

Official source: https://github.com/pardeike/Harmony
Official release: https://github.com/pardeike/Harmony/releases/tag/v2.2.2.0
Bundled DLL SHA-256: 9611251f080e4855cd9d5fa54b5e56034df25b70b374c49ba09550b08a1bf875

## Python and websockets

Python is separately installed by the player. requirements.txt declares websockets>=14,<17; first-connect setup installs it into the mod's private virtual environment. It is not vendored in this repository or player ZIP. Source: https://github.com/python-websockets/websockets

## Archipelago and BallisticNG

The world requires a separate Archipelago installation. Native game types come from the player's own BallisticNG installation. Game assemblies and inspected decompiled game source are not included here. Game names and native content belong to their respective rights holders. This is an unofficial integration.

## Game artwork

Art Appreciation Trap loads concept art already present in the installed game; those art files are not bundled.

## Other implementations studied

Save architecture in the Neon White integration/NeonLite, ClusterTruck and A Short Hike randomizer was studied as reference. These projects are not bundled dependencies. This is an architectural credit, not a claim of review or endorsement by their maintainers.

- https://github.com/stxticOVFL/NWArchipelagoMod
- https://github.com/Faustas156/NeonLite
- https://github.com/Nullctipus/ArchipelagoClusterTruck
- https://github.com/BrandenEK/AShortHike.Randomizer
