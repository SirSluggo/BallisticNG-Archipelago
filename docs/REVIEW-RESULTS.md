# Prerelease package checks

This package uses the v0.36.1 source. Documentation and repository scaffolding were refreshed; game/client behavior is unchanged by this preparation.

- Substantial current-build campaign progress has been made without errors during tested gameplay; full-seed testing is ongoing.
- Baseline: 110 Python tests passed. Four additional template-sync regression tests passed.
- Builder checks duplicate runtime modules, manifest parity, C# catalog digest, Harmony SHA-256 and release/source byte parity.
- Generated state, venvs, logs, saves, cached mod DLLs, debug marker files and unapproved game assemblies are excluded.
- MIT license and Harmony notice retained.
- Idle-demo crash, Cloud limits, Windows scope and absence of independent human review are explicit.

No GitHub publishing, tagging or community messaging was performed. The prerelease notes are a draft for the owner's decision, not a statement that release gates are complete.
