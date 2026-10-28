# 11: PC SETTINGS row in the game's OPTIONS menu

**What to build:** The game's own OPTIONS menu shows a PC SETTINGS row that looks like its other rows, and selecting it opens the Settings page. Build it on the route ticket 10 picked, changing the game's data only at runtime (ADR-0006, ADR-0007). F4 and the pad combo keep working without it. Spec: `.scratch/game-settings/spec.md` (The OPTIONS row).

**Blocked by:** 03 (Settings page tracer bullet), 10 (RE: OPTIONS scene data and the Xbox 3D row)

**Status:** ready-for-human

- [ ] A scene run reaches OPTIONS, finds the PC SETTINGS row with its label, selects it, and the Settings page opens
- [ ] Closing the page returns to OPTIONS, and the game's menu still works
- [ ] Both of the above work on Classic and on Native
- [ ] All-default scenes still match the Oracle, apart from the new row in OPTIONS

## Comments

**2026-10-28, implementation (not run).** Built on the route in `docs/research/game-settings.md`, Addendum 2. Three hooks in `src/settings/options_row_hooks.cpp`, built into the Swing Module's DLL (`cmake/d3d_hooks.cmake`), reaching the exe through the D3D sink library (`src/settings/options_row_sink.h`). No manifest change.
- `IsMoveController` `8877B2B8`: asks `881CF4C0` for the owning scene's name of the node in the command context (`*(r3+4)`) and answers 1 only if it contains `S4MainMenuSubOptions`. Any failure answers 0.
- Text resolver `881EC108`: after the original, answers "PC SETTINGS" for `PS3MoveCalibrate` (`8819A5C8`, `8819BAD0`, `88151AE0`).
- `ForceMoveRecalibrate` `8877B288`: holds the game's input at once and opens the page on the UI thread.
- To confirm the route on a run, look in the log for `options row:` lines: `IsMoveController is called`, `IsMoveController from scene '<name>' -> 0|1` (once per distinct name, up to 16), `the label PS3MoveCalibrate is resolved`, `ForceMoveRecalibrate is called`, then `settings page: opened from the OPTIONS row`. The scene-name lookup and `*(r3+4)` as the node are the medium-confidence parts: if the scene line shows an empty or unexpected name, the row never appears and that line says why.

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in cea0404; review fixes in b694c42. Host or Launcher tests cover: label/scene-name helpers. Needs the owner's run: reach OPTIONS, find PC SETTINGS, select it, page opens, close returns; check the log for the 'options row:' first-hit lines (route is medium confidence); Oracle match apart from the new row. No game run was made (owner tests).
