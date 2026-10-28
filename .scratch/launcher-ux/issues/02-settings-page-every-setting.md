# 02: Settings page: every setting

**What to build:** The Launcher's Settings page shows every setting the game has, in one tab per section of the export (same sections and order as the in-game Settings page). Each row shows its label, value, choices and help, and Native-only settings are shown unavailable on Classic with the reason. The Graphics tab opens with the Preset row: Original, Enhanced, and Custom after a tweak, matching the game exactly (Native-only settings left out of the match on Classic). Each change writes that one setting to the Settings file at once with the existing line-preserving rules; choosing a Preset writes all its settings; "Reset to defaults" writes every default. When the export can't be read, the page says why instead of showing nothing.

To make this possible the game's settings export moves to format 2: it adds the Graphics Presets (value, label, each covered setting's value) and what bindings need to be shown (device, action label). The Launcher reads format 2 and refuses others. Binding rows are shown read-only here; remapping is ticket 03. Spec: User Stories 21–27, 30, 31, 34; Implementation Decisions "Settings screen model", "Settings export format 2".

**Blocked by:** 01 (Home screen tracer)

**Status:** ready-for-agent

- [ ] Host test: export format 2 holds every registry setting, the Presets with their covered values and the binding fields; reading it back gives the same registry and Presets
- [ ] Launcher.Tests: tabs follow the export's order; every setting is present; Original on defaults; Enhanced writes its settings; a tweak gives Custom; Native-only settings don't cause Custom on Classic; Native-only rows are unavailable on Classic
- [ ] Launcher.Tests: a change rewrites only its line in an existing Settings file; Reset to defaults writes every default; an unreadable export gives a reason, not an empty page
- [ ] The Launcher keeps no list of settings or Presets of its own
- [ ] A value changed in the Launcher shows the same on the in-game Settings page (owner run, ticket 08)
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
