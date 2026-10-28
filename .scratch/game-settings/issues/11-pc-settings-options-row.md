# 11: PC SETTINGS row in the game's OPTIONS menu

**What to build:** The game's own OPTIONS menu shows a PC SETTINGS row that looks like its other rows, and selecting it opens the Settings page. Build it on the route ticket 10 picked, changing the game's data only at runtime (ADR-0006, ADR-0007). F4 and the pad combo keep working without it. Spec: `.scratch/game-settings/spec.md` (The OPTIONS row).

**Blocked by:** 03 (Settings page tracer bullet), 10 (RE: OPTIONS scene data and the Xbox 3D row)

**Status:** ready-for-agent

- [ ] A scene run reaches OPTIONS, finds the PC SETTINGS row with its label, selects it, and the Settings page opens
- [ ] Closing the page returns to OPTIONS, and the game's menu still works
- [ ] Both of the above work on Classic and on Native
- [ ] All-default scenes still match the Oracle, apart from the new row in OPTIONS
