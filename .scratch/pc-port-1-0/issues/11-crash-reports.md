# 11: Crash reports and session logs

**What to build:** A crash produces a report a player can attach to an issue, and logs from the last session are kept.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** ready-for-agent

- [x] WWE crash reporting is copied and adapted
- [ ] A forced crash produces a report with guest and host context
- [x] The previous session's log survives the next launch
- [ ] Reports contain no game data

## Comments
- 2026-10-25 (Claude), ticket audit: ticked the log box on a run. Started without `--log_file` (as a player's launch is), the runtime writes each session to a new sequential file, `logs/topspin4_NNN.log` next to the exe (rotating, up to 20 kept). Two headless launches in a row added `topspin4_002.log` and `topspin4_003.log`, and yesterday's `topspin4_001.log` was untouched. The other boxes need the crash reporter itself; `src/fault_log.cpp` (a symbolized host stack for access violations) is only a start.
- 2026-10-25 (Claude), no game run (owner's request). Copied WWE's crash reporting and adapted it: `src/crash_report{.h,.cpp}`, `crash_report_plan.h`, `crash_zip.h`, `crash_dump_request.h`, under `topspin4::crash`. An unhandled exception, abort, a pure virtual call or a bad CRT parameter (codes `0xE05453xx`) writes `crash-<stamp>.zip` holding:
  - a minidump, written by a second copy of the exe (`--ts4-write-crash-dump`) so a held heap lock can't stall it, with both 4 GB guest memory views removed;
  - the session log (`--log_file`, or the newest `logs/topspin4_NNN.log`) and `logs/launcher.log` if there is one, each capped at 4 MB;
  - `topspin4.settings.toml` and a manifest (version, commit, renderer, GPU, driver, reason).
  The player gets a message box offering to open the folder. Dropped from WWE: the Native GPU-failure report. Wired into `TopSpin4App`: `Install` first in `Create`, the settings path at settings load, and the GPU, guest memory views and window in `OnPostSetup`. `--ts4_debug_force_crash` / `--ts4_debug_force_abort` crash right after setup. Ticked the copy box on `build.ps1 -Action Test` (tests/host/test_crash_report.cpp, 13 cases: plan, manifest, log capping, UTF-8 names, newest session log, the zip, the dump-helper command line), and the game builds. Still unobserved, as it needs a game run: `--ts4_debug_force_crash` producing a zip with a usable dump (host stack through recompiled guest functions), and a check that the zip holds no game data.
