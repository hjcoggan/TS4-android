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
- 2026-10-25 (Claude): first measurement, `analysis/smoke-20261025-114304/frame_times.csv`.
  - `frame_pacing.py --from 66 --to 73` (rallies): 0 hitches, but 103 of 420 intervals more than 1 ms off 16.7 (worst 18.95 ms, 1 % low 18.5 ms).
  - `--from 60` (the whole match): the cutaways' 30 Hz frames spread from 31.8 to 35.1 ms. So they fail the ±1 ms rule, aren't recognised as an even cutaway run, and count as hitches.
  - Not a verdict yet. The times are taken when the GPU thread processes each swap, and the presenter paces the actual present later, so this jitter may never reach the screen.
  - Next: timestamp at present (the presenter's vblank-paced present, patches `0016-present-wait` and `0024-guest-vblank-rate`), then judge again. Only if the present times jitter too is the spread real.
- 2026-10-25 (Claude): measured at present. `frame_pacing.py` now also reads the presenter's swap log (`--present_pacing_log`, written by `smoke.ps1` as `present_pacing.csv`): one row per guest frame at its paced present, plus the frames replaced unshown.
  - Over 60-100 s of the match in `analysis/smoke-20261025-114924`: 2,155 frames, 241 at the cutaways' 30 Hz step, 1 % low 16.86 ms (the bar is 16.7), 10 uneven, 6 hitches, 0 replaced. The GPU-thread log of the same window reads 1 % low 34.95 ms and 191 hitches, so that jitter never reached the screen.
  - The hitches, from the swap-log rows:
    - 67.40 s and 98.86 s: the presenter waited 9.7 and 18.5 ms for the previous present to be shown (`wait_us`, `unshown 1`) while the guest frame was on time. Display or driver side.
    - 74.19-74.25 s: inside a 30 Hz cutaway, one present waited 3.9 ms, giving 37.5 and 29.2 ms intervals and breaking the even run. Display side.
    - 91.27 s: one vblank with no new guest frame (`repeated_vblanks 1`, no wait), a single held frame in 60 Hz play. Game or emulation side.
  - Caveat: this was a headless run with a hidden window, so the display-side waits are suspect until measured in a visible window. Next: a visible-window run judged the same way; then, if they persist, the present-wait stalls (ReXGlue #521, AMD fence waits) and the held guest frame (the guest-scheduler and spin leads above).
- 2026-10-25 (Claude): visible-window runs (the owner agreed), `boot-to-exhibition`, judged on `present_pacing.csv`:
  - `analysis/smoke-20261025-115433` (defaults), 60-100 s: 2,168 frames, 232 at the cutaways' 30 Hz step, 0 hitches, 0 uneven, 0 replaced. The 60 Hz intervals have a median of 16.665 ms, p99 16.861 ms, and a range of 16.422-16.885 ms. So the headless run's display-side waits came from the hidden window.
  - The only "FAIL" is the 1 % low of 16.86 ms against WWE's 16.7 ms: 0.2 ms of present-call jitter. WWE's bar reads PresentMon's DisplayedTime, which is scan-out quantised, so the two numbers are not directly comparable. The bar was left as it is.
  - Over 60-125 s there are 3 hitch pairs (37/29 ms at 110.14, 110.54 and 111.88 s), all inside a 30 Hz cutaway. In each, a new frame waited 3.5-4.4 ms on present-wait because the held vblank's repeat present was not yet shown.
  - `analysis/smoke-20261025-115712` with `--present_repeat_held_vblanks=false`, 60-125 s: 0 hitches, 0 uneven, 0 replaced. The prediction held at present time.
  - The default stays on. The SDK comment (present_pacing.h, from WWE ticket 23) says that without the repeats, a variable-refresh panel holds a skipped vblank for 33 ms and shows the next frames at 22.2, 12.6 and 15.0 ms. That is visible only in scan-out timing (PresentMon), which this log cannot see.
  - Deciding needs display-side numbers: PresentMon's DisplayedTime in an elevated run, with repeats on and off, across a cutaway. The owner's display may be variable-refresh, which is why it matters here.
