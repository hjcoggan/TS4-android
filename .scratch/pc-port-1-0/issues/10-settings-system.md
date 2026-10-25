# 10: Settings system and renderer switch

**What to build:** Settings are saved per user with original-behaviour defaults, and the renderer can be switched between Classic and Native.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** ready-for-agent

- [ ] The WWE settings system is copied and adapted
- [x] Settings live in the per-user directory
- [x] Every setting defaults to original behaviour
- [x] A renderer setting selects Classic or Native (Native may still be unavailable at this point)
- [x] Unit tests cover defaults, persistence, and recovery from a corrupt settings file

## Comments
- 2026-10-25 (Claude), built and unit-tested without running the game (the owner asked for no runs). Adapted from WWE: `src/settings/config_file.h` (line-preserving TOML edits, atomic writes; no migrations). New: `src/settings/registry.h` (one list of player settings with their original-behaviour defaults: `ts4_renderer` = classic, `ts4_unlock_agassi_outfit` = false), `settings_file.{h,cpp}` (loads `topspin4.settings.toml` from the per-user folder; skips values a setting does not allow and keys no setting has; sets a file that is not TOML aside as `.corrupt` and keeps the defaults), `renderer_choice.h` (Native asked for but not built: Classic draws and the log says so), and `settings_cvars.cpp`. `TopSpin4App::OnPostInitLogging` loads the file into the cvars; a cvar given on the command line keeps that value. Ticked on `build.ps1 -Action Test` (tests/host/test_settings.cpp): defaults, save and load back, invalid values, corrupt-file recovery, renderer choice. Still open: the WWE in-game settings page and the launcher's settings screen are not copied (UI, verified only by running). The per-user folder and the renderer setting are wired into the game and build, but unobserved until a run.
- 2026-10-25 (Claude), after review:
  - `ts4_unlock_agassi_outfit` is removed: nothing read it, and ticket 25 has not settled whether the outfit is on the disc at all.
  - The settings file now also yields to a `REX_<NAME>` environment variable, not only to the command line.
  - At start the game logs an error when a setting's cvar default differs from the registry's, so the two lists can't drift silently.
  - Paths are no longer passed through `path::string()`, which throws for a user name outside the ANSI code page.
- 2026-10-25 (Claude), player-style runs; ticked the per-user and renderer boxes. The game was started without `--user_data_root` (as a player's launch is), with `%APPDATA%\TopSpin4Recomp\topspin4.settings.toml` holding `ts4_renderer = "native"` and a START/RS swap (ticket 24); `analysis/remap-20261025-123709`. The log reads `Settings: C:\Users\Martin\AppData\Roaming\TopSpin4Recomp\topspin4.settings.toml`, then `ts4_renderer is native, but this build has no Native renderer yet; drawing with Classic`, and the frames drew. The game also made its profile folders there (`54540859\profile\User\63E83FFD..FF`). The settings file was removed after the runs. Still open: the copied WWE settings page and launcher screen (UI).
