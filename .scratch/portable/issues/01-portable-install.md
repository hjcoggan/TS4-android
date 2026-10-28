# 01: Portable install

**What to build:** Everything the game and the Launcher write by default goes beside `topspin4.exe`, as `.scratch/portable/spec.md` describes: `gamedata`, `updatedata`, `cache`, `userdata` (settings, saves, achievements, art, location file). The game's `src/user_paths.*` and `src/topspin4_app.h`, the Launcher's `UserDirectories` and its callers (`MainWindow`, Launcher.Cli), and every comment, doc and glossary line that names the per-user folders (`docs/goals.md` line on saves and settings, `GLOSSARY.md`, the README's help section if it names them) follow ADR-0009. A build folder that can't be written to is refused on the setup page. Spec: `.scratch/portable/spec.md`; decision: `docs/adr/0009-portable-install.md`.

**Blocked by:** None

**Status:** resolved

- [x] Host tests: default roots resolve from the exe folder (also under Wine); a location file in `userdata` moves the prepared data and cache; the command line wins
- [x] Launcher.Tests: the same roots from the Launcher's folder; an unwritable folder gives the refusal with setup disabled; the art folder is `userdata\art`
- [x] The game builds, and one smoke run passes (`scripts/smoke.ps1`)
- [x] `pwsh scripts/build.ps1 -Action Check` passes

## Comments

Evidence (2026-10-28):
- Host tests in `tests/host/test_user_paths.cpp` (exe-folder roots incl. a Z: path, `IsWritableFolder`, location file moves prepared data and cache, command line wins): `-Action Check`, 405 cases / 54834 assertions passed.
- Launcher.Tests `UserDirectoriesTests` (roots from the Launcher folder incl. Z:, `userdata\art`, refusal wording) and `SetupFlowTests` (`SetupPhase.Refused`: message shown, `CanPrepare` false, setup page even when the files exist): 389 passed, 1 skipped.
- `-Action Build` (game + Launcher republish, topspin4-launcher.exe placed beside topspin4.exe) and `-Action Check` passed.
- Smoke: `scripts/smoke.ps1 -Seconds 30`, `analysis/smoke-20261028-231745/result.json`: alive_at_deadline, no fatals, no unregistered targets, 241 frames. The smoke passes every folder on the command line, so the default resolution is covered by the host tests only.
- The game refuses an unwritable exe folder (`kNotSetUpUnwritable`, opens the Launcher) only when `--user_data_root` is not given; this path has no automated test beyond `IsWritableFolder`.

Not verified (owner run): the published Launcher in a fresh folder with no `gamedata` (set up, play, nothing new under `%APPDATA%` / `%LOCALAPPDATA%`); the refusal on the setup page in a real read-only folder (tests use a path under a file); the game's own start from such a folder.

Real runs on the published build (2026-10-28, 23:19–23:23), in a fresh copy of the build folder at `analysis/portable-run/` (git-ignored):
- `topspin4-setup.exe --disc … --tu …` from that folder prepared `gamedata\` and `updatedata\` beside the exe (7 s).
- `topspin4.exe --headless --window_hidden=true` with no folder flags: did not report "not set up", ran 30 s alive, wrote `cache\shader-topup\…` and `logs\` beside the exe.
- `topspin4-launcher.exe` from that folder: opened on Home, "Ready to play.", "Game files are in …\portable-run", and wrote the disc art to `userdata\art\` (index `read=complete`).
- `%APPDATA%\TopSpin4Recomp` and `%LOCALAPPDATA%\TopSpin4Recomp`: 717 files listed with size and write time before the runs, identical after them (0 differences).
- Write-denied copy (folder ACL deny create files/folders for the user): `topspin4.exe --headless` printed "Top Spin 4 is not set up (folder-not-writable) and no game folders were given." and exited 2; without `--headless` it exited 0 and started the Launcher from that folder, whose setup page (read through UI Automation) showed "This folder can't be written to: …\portable-readonly. Move the game folder somewhere you can write to, such as your Documents or a games drive, and start the Launcher again." with "SET UP THE GAME" disabled.
- Not observed: a save written to `userdata\` (needs a played match), and the refusal line sits below the first screen of the setup page, so a player has to scroll to see it.
