# 03: Remapping in the Launcher

**What to build:** On the Controls tab of the Launcher's Settings page a player can remap pad buttons and keys. Selecting a binding row waits for the next key or pad button pressed (Esc or a timeout cancels), writes it to the Settings file in the format the game's Controls section uses, and points out when it clashes with another binding of the same device. "Reset controls" restores the original layout. Spec: User Stories 28, 29; Implementation Decisions "Bindings".

**Blocked by:** 02 (Settings page: every setting)

**Status:** ready-for-human

- [x] Launcher.Tests: a captured key and a captured pad button are written in the game's binding format; a binding equal to another of the same device is reported as a clash; Reset controls writes the defaults of the Controls section only
- [ ] A binding changed in the Launcher works in game and shows the same on the in-game Settings page (owner run, ticket 08)
- [x] `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-28, launcher-ux integration (qa reviewed):
- Launcher.Tests: `BindingTests`, `BindingCaptureTests`.
- Known gaps for the owner run: a pad cannot cancel a key capture (Esc or the 8 s timeout does); key rows always show.
- `pwsh scripts/build.ps1 -Action Check` passed at 64e3827 (QA rerun at 61b4b4d; rerun after the QA fixes with TOPSPIN4_EXE set, 285/285 Launcher tests, 403 host cases).
- Remaining boxes need the real window; they wait on ticket 08 (owner run).
