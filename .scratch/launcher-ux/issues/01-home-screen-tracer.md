# 01: Home screen tracer

**What to build:** After setup the Launcher opens on a home screen: PLAY with a status line, then labelled Settings, Help and Open logs actions, and Safe start in a small "Trouble starting?" area. The status line says when the game exe is missing as soon as the window opens, not when PLAY is pressed. Settings opens its own page with Back, showing today's renderer setting with a label and plain options ("Native (recommended)", "Classic (compatibility)") and a line saying the other settings are in game and how to open them. Help and Open logs open the README's new help section and the logs folder. The dev build places the published Launcher beside `topspin4.exe`, so a developer sees what players see. The window is split into Setup, Home and Settings pages, each drawn from a model in Launcher.Core, so later tickets fill pages instead of growing one window. Spec: `.scratch/launcher-ux/spec.md` (User Stories 18–20, 32, 33, 39, 48, 49, 55; ADR-0008).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Launcher.Tests: the home model gives "ready" with both prepared folders and the game exe present, and "game exe missing" without the exe, before any Play
- [ ] The dev build output has the Launcher beside `topspin4.exe`, and that Launcher shows the renderer setting on the Settings page
- [ ] Home shows PLAY, a status line, Settings, Help, Open logs and a "Trouble starting?" area holding Safe start
- [ ] The README has a help section (dumping your own disc and TU1, the in-game Settings page keys) with no pointer to sources of game data (ADR-0006)
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
