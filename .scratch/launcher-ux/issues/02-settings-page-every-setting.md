# 02: Settings page: every setting

**What to build:** The Launcher's Settings page shows every setting the game has, in one tab per section of the export (same sections and order as the in-game Settings page). Each row shows its label, value, choices and help, and Native-only settings are shown unavailable on Classic with the reason. The Graphics tab opens with the Preset row: Original, Enhanced, and Custom after a tweak, matching the game exactly (Native-only settings left out of the match on Classic). Each change writes that one setting to the Settings file at once with the existing line-preserving rules; choosing a Preset writes all its settings; "Reset to defaults" writes every default. When the export can't be read, the page says why instead of showing nothing.

To make this possible the game's settings export moves to format 2: it adds the Graphics Presets (value, label, each covered setting's value) and what bindings need to be shown (device, action label). The Launcher reads format 2 and refuses others. Binding rows are shown read-only here; remapping is ticket 03. Spec: User Stories 21–27, 30, 31, 34; Implementation Decisions "Settings screen model", "Settings export format 2".

**Blocked by:** 01 (Home screen tracer)

**Status:** ready-for-human

- [x] Host test: export format 2 holds every registry setting, the Presets with their covered values and the binding fields; reading it back gives the same registry and Presets
- [x] Launcher.Tests: tabs follow the export's order; every setting is present; Original on defaults; Enhanced writes its settings; a tweak gives Custom; Native-only settings don't cause Custom on Classic; Native-only rows are unavailable on Classic
- [x] Launcher.Tests: a change rewrites only its line in an existing Settings file; Reset to defaults writes every default; an unreadable export gives a reason, not an empty page
- [x] The Launcher keeps no list of settings or Presets of its own
- [ ] A value changed in the Launcher shows the same on the in-game Settings page (owner run, ticket 08)
- [x] `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-28, launcher-ux integration (qa reviewed):
- Host tests: "reads back as the same registry", "carries the Graphics Presets", "says which settings are bindings".
- Launcher.Tests: `SettingsPageModelTests`, `SettingsStoreTests`; the real exe's format 2 export read through a pipe passes (`Reading_the_export_from_a_gui_exe_through_a_pipe_works`, run with TOPSPIN4_EXE).
- No own list: `The_page_keeps_no_list_of_its_own...`; the renderer choice labels are the ones ticket 01 asks for.
- `pwsh scripts/build.ps1 -Action Check` passed at 64e3827 (QA rerun at 61b4b4d; rerun after the QA fixes with TOPSPIN4_EXE set, 285/285 Launcher tests, 403 host cases).
- Remaining boxes need the real window; they wait on ticket 08 (owner run).
