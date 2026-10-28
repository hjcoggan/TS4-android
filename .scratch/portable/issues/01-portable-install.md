# 01: Portable install

**What to build:** Everything the game and the Launcher write by default goes beside `topspin4.exe`, as `.scratch/portable/spec.md` describes: `gamedata`, `updatedata`, `cache`, `userdata` (settings, saves, achievements, art, location file). The game's `src/user_paths.*` and `src/topspin4_app.h`, the Launcher's `UserDirectories` and its callers (`MainWindow`, Launcher.Cli), and every comment, doc and glossary line that names the per-user folders (`docs/goals.md` line on saves and settings, `GLOSSARY.md`, the README's help section if it names them) follow ADR-0009. A build folder that can't be written to is refused on the setup page. Spec: `.scratch/portable/spec.md`; decision: `docs/adr/0009-portable-install.md`.

**Blocked by:** None

**Status:** ready-for-agent

- [ ] Host tests: default roots resolve from the exe folder (also under Wine); a location file in `userdata` moves the prepared data and cache; the command line wins
- [ ] Launcher.Tests: the same roots from the Launcher's folder; an unwritable folder gives the refusal with setup disabled; the art folder is `userdata\art`
- [ ] The game builds, and one smoke run passes (`scripts/smoke.ps1`)
- [ ] `pwsh scripts/build.ps1 -Action Check` passes
