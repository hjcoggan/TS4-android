# 05: Game files location

**What to build:** The setup page shows "Game files go to … [Change…]" with the free space there and the ~6 GB needed, and warns before setup starts when there isn't enough. After setup the home screen offers the same Change…, which moves the prepared files to the new place with a progress bar, verifies them against the manifest, records the new place and removes the old copy; an interrupted move keeps the old copy in use. The Launcher records a changed location in a small location file in the per-user folder; the game reads it when looking for prepared data, after the command line and before the default prepared-data folder. The shader cache stays beside the prepared data. Spec: User Stories 11–13, 46, 47; Implementation Decisions "Game files location", "Game files location shared with the game".

**Blocked by:** 01 (Home screen tracer)

**Status:** resolved

- [x] Launcher.Tests: not enough free space is reported before setup starts; setup into a chosen place records it in the location file
- [x] Launcher.Tests: a move lands verified files in the new place, records it and removes the old copy; an interrupted move leaves the old place recorded and intact
- [x] Host test: prepared-data lookup prefers the command line, then the location file, then the default folder
- [x] Headless smoke run with developer folders on the command line is unchanged
- [x] `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-28, launcher-ux integration (qa reviewed):
- Launcher.Tests: `GameFilesLocationTests` (space before setup, location file, verified move, interrupted move); the move also checks free space and carries the shader cache (review fixes, 61b4b4d).
- Host tests: `test_user_paths` lookup order; UTF-8 location file and completion marker (64e3827).
- Smoke runs alive at 30 s, no fatals or unregistered targets: worktree t05 `analysis/smoke-20261028-175040`, tfix `analysis/smoke-20261028-182809` and `smoke-20261028-183737`.
- Oracle comparison not applicable: no rendering change, and the smoke runs take no screenshots.
- `pwsh scripts/build.ps1 -Action Check` passed at 64e3827 (QA rerun at 61b4b4d; rerun after the QA fixes with TOPSPIN4_EXE set, 285/285 Launcher tests, 403 host cases).
