# 16: Locked 60 with even frame pacing

**What to build:** The game runs at its original 60 fps tick with even pacing on any display, and never speeds up on high-refresh screens.

**Blocked by:** 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [ ] Game speed is correct on 60, 120 and 144 Hz displays
- [ ] Frame-time variance during a match meets the WWE pacing bar
- [ ] Spin-wait and fence behaviour does not cause hitches
- [ ] Measured with the WWE frame-pacing tooling

## Comments
- 2026-10-25 (Claude), no game run (owner's request). Pacing tooling, adapted from WWE's `scripts/analyze_hitches.py` because that one needs PresentMon (which records nothing unelevated on this PC) and WWE's own logs.
  - `--ts4_frame_times_csv=<file>` (src/frame_dumps.h) writes the host time of every frame shown, measured on the GPU thread. `scripts/smoke.ps1` now writes `frame_times.csv` on every run.
  - `scripts/frame_pacing.py <frame_times.csv>` judges it against WWE's bar: no hitch (an interval over 25 ms), every frame within 1 ms of a step, and a 1 % low within 16.7 ms.
  - Top Spin 4's own 30 fps cutaways (ticket 01) count as a step (33.3 ms ± 1), and the 1 % low is taken over the 60 Hz frames only. Its limit: times are taken when a frame reaches the presenter, so judder on the display side is not seen.
  - Unit-tested (tests/test_frame_pacing.py: steady 60, a hitch, an even 30 Hz cutaway, a frame between steps, off-step frames, CSV loading). No box ticked: nothing has been measured yet. Next run: `smoke.ps1 -InputScript scripts/input/boot-to-exhibition.txt`, then `frame_pacing.py <run>/frame_times.csv`.
  - Leads from docs/research/recomp-community-help.md for the spin-wait and hitch boxes: the Oracle runs a cooperative guest scheduler (`guest_scheduler = true`), and the port does not; ReXGlue #521 (AMD fence waits); `db16cyc` spin hints dropped as no-ops.
- 2026-10-25 (Claude), after review: `frame_pacing.py` counts a 33.3 ms frame as a cutaway step only inside a run of at least 6 such frames, as WWE allows 30 Hz only in flagged windows. A lone dropped frame, or a run flipping between 60 and 30, is a hitch. `--from`/`--to` judge a window (the match) instead of the whole run with its loads.
