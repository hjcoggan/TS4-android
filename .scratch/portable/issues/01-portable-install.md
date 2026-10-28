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
