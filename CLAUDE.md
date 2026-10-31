# Top Spin 4 Recomp

- Check before committing: `pwsh scripts/build.ps1 -Action Check` (pytest, host tests, Launcher tests).
- Build: `pwsh scripts/build.ps1 -Action Build` (`-Action Codegen` first if `topspin4_manifest.toml` changed).
- Game run, when a change can alter runtime behaviour: `scripts/smoke.ps1`, or a scene with `python scripts/scene_runner.py tests/scenes/<scene>.toml` (`--native 1` for Native). One run at a time.
- Never commit game data, extracted executables or generated C++ (ADR-0006).
- Commit verified steps freely; ask before any push.
- Open work: `TODO.md`. Scope and milestones: `docs/goals.md`. Terms: `GLOSSARY.md`. Decisions: `docs/adr/`.
- Smoke run or codegen fails (unregistered target, crash, stall): `docs/agents/codegen-debugging.md`.
- Native draws differently from Classic, or adding a D3D hook: `docs/agents/native-debugging.md`.
- A bounded reverse-engineering question about the TU1 Modules can go to the `re-investigator` agent, to keep Ghidra output out of the main session.
