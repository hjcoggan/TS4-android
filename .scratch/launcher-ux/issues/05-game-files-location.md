# 05: Game files location

**What to build:** The setup page shows "Game files go to … [Change…]" with the free space there and the ~6 GB needed, and warns before setup starts when there isn't enough. After setup the home screen offers the same Change…, which moves the prepared files to the new place with a progress bar, verifies them against the manifest, records the new place and removes the old copy; an interrupted move keeps the old copy in use. The Launcher records a changed location in a small location file in the per-user folder; the game reads it when looking for prepared data, after the command line and before the default prepared-data folder. The shader cache stays beside the prepared data. Spec: User Stories 11–13, 46, 47; Implementation Decisions "Game files location", "Game files location shared with the game".

**Blocked by:** 01 (Home screen tracer)

**Status:** ready-for-agent

- [ ] Launcher.Tests: not enough free space is reported before setup starts; setup into a chosen place records it in the location file
- [ ] Launcher.Tests: a move lands verified files in the new place, records it and removes the old copy; an interrupted move leaves the old place recorded and intact
- [ ] Host test: prepared-data lookup prefers the command line, then the location file, then the default folder
- [ ] Headless smoke run with developer folders on the command line is unchanged
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
