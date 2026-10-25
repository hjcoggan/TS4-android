# 11: Crash reports and session logs

**What to build:** A crash produces a report a player can attach to an issue, and logs from the last session are kept.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** resolved

- [x] WWE crash reporting is copied and adapted
- [x] A forced crash produces a report with guest and host context
- [x] The previous session's log survives the next launch
- [x] Reports contain no game data

## Comments
- 2026-10-25 (Claude), ticket audit: ticked the log box on a run. Started without `--log_file` (as a player's launch is), the runtime writes each session to a new sequential file, `logs/topspin4_NNN.log` next to the exe (rotating, up to 20 kept). Two headless launches in a row added `topspin4_002.log` and `topspin4_003.log`, and yesterday's `topspin4_001.log` was untouched. The other boxes need the crash reporter itself; `src/fault_log.cpp` (a symbolized host stack for access violations) is only a start.
- 2026-10-25 (Claude), no game run (owner's request). Copied WWE's crash reporting and adapted it: `src/crash_report{.h,.cpp}`, `crash_report_plan.h`, `crash_zip.h`, `crash_dump_request.h`, under `topspin4::crash`. An unhandled exception, abort, a pure virtual call or a bad CRT parameter (codes `0xE05453xx`) writes `crash-<stamp>.zip` holding:
  - a minidump, written by a second copy of the exe (`--ts4-write-crash-dump`) so a held heap lock can't stall it, with both 4 GB guest memory views removed;
  - the session log (`--log_file`, or the newest `logs/topspin4_NNN.log`) and `logs/launcher.log` if there is one, each capped at 4 MB;
  - `topspin4.settings.toml` and a manifest (version, commit, renderer, GPU, driver, reason).
  The player gets a message box offering to open the folder. Dropped from WWE: the Native GPU-failure report. Wired into `TopSpin4App`: `Install` first in `Create`, the settings path at settings load, and the GPU, guest memory views and window in `OnPostSetup`. `--ts4_debug_force_crash` / `--ts4_debug_force_abort` crash right after setup. Ticked the copy box on `build.ps1 -Action Test` (tests/host/test_crash_report.cpp, 13 cases: plan, manifest, log capping, UTF-8 names, newest session log, the zip, the dump-helper command line), and the game builds. Still unobserved, as it needs a game run: `--ts4_debug_force_crash` producing a zip with a usable dump (host stack through recompiled guest functions), and a check that the zip holds no game data.
- 2026-10-25 (Claude), forced-crash runs; ticked the guest/host context and no-game-data boxes; resolved.
  - First run, `smoke.ps1 -Headless --ts4_debug_force_crash=true` (`analysis/smoke-20261025-122414`): the zip was written (dump, session log, manifest), but in cdb and lldb every thread's stack stopped after one frame. The crashed thread's stack was at `0x3'6EBD'F580`, inside the physical view's 4 GB window (`0x3'0000'0000`), and the helper removed the whole 4 GB window behind each view, host stacks placed in its unreserved gaps included.
  - Fix: the helper walks each window with `VirtualQueryEx` and removes only `MEM_MAPPED` regions (the guest views are the SDK's file mapping; host stacks and heaps are private). `GuestPieces` in `crash_dump_request.h` makes the split pure; two new cases in tests/host/test_crash_report.cpp (a stack and a heap inside the windows are kept, mappings clipped at the window's end, nothing removed without views).
  - New `--ts4_debug_force_crash_at_frame=N` crashes on the GPU thread once guest frame N is shown, so the guest's threads are running; `--ts4_debug_force_crash` fires before any guest code runs.
  - `--ts4_debug_force_crash_at_frame=1300` with `boot-to-main-menu.txt` (`analysis/smoke-20261025-122815`, zip 906 KB, dump 843 KB): cdb shows the crashing thread from `CrashIfAskedAtFrame < OnFrameShown < CommandProcessor::ExecutePacketType3_XE_SWAP < ... WorkerThreadMain`, and the guest threads unwind through recompiled functions of both modules into the kernel imports, e.g. `topspin4_Swing_DLL!sub_88292718 < ... sub_885F5218 < topspin4!sub_82028260 < sub_82029418 < xstart < XThread::Execute`, and `NtWaitForSingleObjectEx < Swing sub_88582F30 < ... sub_8858B590`.
  - No game data: of the dump's 425 memory ranges (567 KB), none lies in either guest window. Every 16-byte run (taken every 16 bytes, at least 8 distinct bytes) of `gamedata/`, `updatedata/`, `assets/` and `analysis/loader-data/` (5.7 GB) was looked for in the dump: the 60 found are file paths the game opened (`\Device\Harddisk0\Partition1\...\Crowd_*.fsb`, `PlayerCreator_*.arc`, `xe:\xboxkrnl.exe`), UTF-16 thread names also present in the XUI packages, and debugger notes stored under `loader-data`; no asset content.
  - Seen on the way: the manifest's commit is stamped at configure time (CMakeLists.txt `execute_process`), so it read `107cf8a` with HEAD at `b64e018`. A fresh release configure is right; a developer build can name an older commit.
