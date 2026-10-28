# 07: Controller and accessibility pass

**What to build:** The whole window behaves like a desktop app first and still works from the couch. PLAY and "Set up the game" are default buttons; Esc is Back. Pad input drives directional focus (XYFocus) so focus moves to what is visually next to it; A selects, B goes back, and on the home screen B does nothing harmful. Pad button hints appear only after pad input and hide again on mouse or keyboard use. Paragraphs are left-aligned and small text is raised to a readable size. Status and error lines are live regions. Spec: User Stories 50–54; Implementation Decisions "Window behaviour".

**Blocked by:** 02 (Settings page: every setting), 04 (Setup page: check on pick)

**Status:** ready-for-human

- [x] Launcher.Tests: pad directions map to directional focus moves; B maps to Back on Settings and setup sub-steps
- [ ] Every page is usable with a pad only, and with keyboard only (owner run, ticket 08)
- [x] `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-28, launcher-ux integration (qa reviewed):
- Launcher.Tests: `WindowInputTests` (directions, B on Settings, B stops a running setup, hints).
- `pwsh scripts/build.ps1 -Action Check` passed at 64e3827 (QA rerun at 61b4b4d; rerun after the QA fixes with TOPSPIN4_EXE set, 285/285 Launcher tests, 403 host cases).
- Remaining boxes need the real window; they wait on ticket 08 (owner run).
