# 01: Home screen tracer

**What to build:** After setup the Launcher opens on a home screen: PLAY with a status line, then labelled Settings, Help and Open logs actions, and Safe start in a small "Trouble starting?" area. The status line says when the game exe is missing as soon as the window opens, not when PLAY is pressed. Settings opens its own page with Back, showing today's renderer setting with a label and plain options ("Native (recommended)", "Classic (compatibility)") and a line saying the other settings are in game and how to open them. Help and Open logs open the README's new help section and the logs folder. The dev build places the published Launcher beside `topspin4.exe`, so a developer sees what players see. The window is split into Setup, Home and Settings pages, each drawn from a model in Launcher.Core, so later tickets fill pages instead of growing one window. Spec: `.scratch/launcher-ux/spec.md` (User Stories 18–20, 32, 33, 39, 48, 49, 55; ADR-0008).

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [x] Launcher.Tests: the home model gives "ready" with both prepared folders and the game exe present, and "game exe missing" without the exe, before any Play
- [ ] The dev build output has the Launcher beside `topspin4.exe`, and that Launcher shows the renderer setting on the Settings page
- [ ] Home shows PLAY, a status line, Settings, Help, Open logs and a "Trouble starting?" area holding Safe start
- [x] The README has a help section (dumping your own disc and TU1, the in-game Settings page keys) with no pointer to sources of game data (ADR-0006)
- [x] `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-28, launcher-ux integration (qa reviewed):
- Home model: `HomeModelTests` (ready; game exe missing before any Play).
- README help section: `README.md` Help section, no pointer to game-data sources.
- `pwsh scripts/build.ps1 -Action Check` passed at 64e3827 (QA rerun at 61b4b4d; rerun after the QA fixes with TOPSPIN4_EXE set, 285/285 Launcher tests, 403 host cases).
- Unticked: Launcher beside `topspin4.exe` is observed in the build output, but the renderer row on the Settings page and the Home layout have not been seen in a window. Remaining boxes need the real window; they wait on ticket 08 (owner run).
