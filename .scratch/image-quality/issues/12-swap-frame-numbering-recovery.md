# 12: Swap frame numbering: recovery and the unreached fallback (rexglue 0039)

**What to build:** Patch 0039 makes each guest swap show the Native frame with its own number. QA of 70a727b found two gaps:
- `VdSwap`'s early return for an invalid front buffer (`xboxkrnl_video.cpp` ~506) counts the swap but writes no swap packet. After that, `GuestSwapCount` and `FramesShownCount` are off by one for the rest of the session; the old rule recovered on its own. Resynchronise, or count only swaps that issue a packet.
- With the shipped lag of 1, the "frame before" fallback (`patches/rexglue/0039-swap-shows-own-frame.patch` ~96) is never reached: a swap whose own frame is missing shows the newer frame. `patches/README.md` and `tests/host/test_swap_choice.cpp` (which uses a lag of 2) describe a path the game never takes. With a lag of 2 or more, the shown frame could step backwards. Make the docs and tests match the real behaviour, or make the fallback reachable.

**Blocked by:** None

**Status:** resolved

- [x] A test shows the numbering recovers after a swap that issues no packet
- [x] The README and tests describe the fallback the game actually takes at lag 1
- [x] A Native scene run shows no "frame N shows Native's frame M" line and its dual verdict passes

## Comments

- 2026-10-27 (Claude): opened from the QA of 70a727b. QA confirmed the numbering itself: in `analysis/scenes/iq06b` the shown frame went up by exactly 1 on 4988 of 4989 presents.
- 2026-10-28 (owner, via Claude): fix both gaps on this branch before the merge. Delete the unreachable "frame before" fallback rather than make it reachable. Tests in `tests/host/test_swap_choice.cpp`.
- 2026-10-28 (Claude): implemented as `patches/rexglue/0040-swap-numbering-recovery.patch`. `VdSwap` counts a swap only after the invalid-front-buffer early return; `ChooseSwapFrame` searches only upward. `test_swap_choice.cpp` builds standalone, all 8 cases pass. QA: pass with notes (the recovery test checks a model of `VdSwap`, not the kernel code, which the host tests cannot link; no other `VdSwap` path desyncs the counts). Box 3 waits for the combined game run.
- 2026-10-28 (Claude): the 12 runs of ticket 10's shot pass (`analysis/before-after/runs/*/{dual,max}/runtime.log`, build with 0040) log no "frame N shows Native's frame M" line; all exit 0.
- 2026-10-28 (Claude): boxes ticked. Box 1: `tests/host/test_swap_choice.cpp` "numbering recovers" case passes in `build.ps1 -Action Check` (host tests, 300 cases, after 8442446/f311d9e) — at model level, per QA. Box 2: README rows 0039/0040 and the lag-1 test (QA read). Box 3: `analysis/scenes/iq09/practice-ground-default-0041/` (build with 0040 and 0041): native-1 and dual-1 exit 0, dual verdict PASS at 1300 and 1600 (worst tile 0), no "shows Native's frame" line in either `runtime.log`; same for the 12 shot runs of ticket 10.
