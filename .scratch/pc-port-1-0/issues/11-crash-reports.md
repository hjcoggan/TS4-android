# 11: Crash reports and session logs

**What to build:** A crash produces a report a player can attach to an issue, and logs from the last session are kept.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** ready-for-agent

- [ ] WWE crash reporting is copied and adapted
- [ ] A forced crash produces a report with guest and host context
- [x] The previous session's log survives the next launch
- [ ] Reports contain no game data

## Comments
- 2026-10-25 (Claude), ticket audit: ticked the log box on a run. Started without `--log_file` (as a player's launch is), the runtime writes each session to a new sequential file, `logs/topspin4_NNN.log` next to the exe (rotating, up to 20 kept). Two headless launches in a row added `topspin4_002.log` and `topspin4_003.log`, and yesterday's `topspin4_001.log` was untouched. The other boxes need the crash reporter itself; `src/fault_log.cpp` (a symbolized host stack for access violations) is only a start.
