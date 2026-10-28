# 04: Setup page: check on pick

**What to build:** First run opens a setup page with two numbered rows, "1 Disc" and "2 Title Update 1". Each row has one sentence of help (what the file is, what it is usually called) and a "How do I get these files?" link to the README help section. Picking a file runs the existing header and identity checks at once and shows the result on that row: ✓ with the edition or update found, or a plain reason, with the media ID, version and exception text kept behind "Details" and in the log. The disc row takes only a disc `.iso`, the TU1 row only the TU1 file. Nothing is searched for or filled in automatically (the Downloads/Desktop search and "Next one found" go). "Set up the game" is enabled only when both rows have a ✓. Setup shows one progress bar across check, extract and unpack (indeterminate while unknown) with the time left, can be stopped and resumed ("Resume setup" / "Start over"), and ends on the home screen. One term throughout: "set up". Spec: User Stories 1–10, 14–17; Implementation Decisions "Setup page model".

**Blocked by:** 01 (Home screen tracer)

**Status:** ready-for-agent

- [ ] Launcher.Tests: a supported disc gives an accepted row naming it; a different edition, a non-disc file and an unreadable file each give a rejected row with a plain reason and separate details; the same for the TU1 row
- [ ] Launcher.Tests: no row is filled without a choice; "Set up the game" is enabled only with both rows accepted; progress is one value that never goes back
- [ ] No message names a control the window doesn't have (the "choose a cache folder" wording is gone or points to ticket 05's control)
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
