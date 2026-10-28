# 12: Swap frame numbering: recovery and the unreached fallback (rexglue 0039)

**What to build:** Patch 0039 makes each guest swap show the Native frame with its own number. QA of 70a727b found two gaps:
- `VdSwap`'s early return for an invalid front buffer (`xboxkrnl_video.cpp` ~506) counts the swap but writes no swap packet. After that, `GuestSwapCount` and `FramesShownCount` are off by one for the rest of the session; the old rule recovered on its own. Resynchronise, or count only swaps that issue a packet.
- With the shipped lag of 1, the "frame before" fallback (`patches/rexglue/0039-swap-shows-own-frame.patch` ~96) is never reached: a swap whose own frame is missing shows the newer frame. `patches/README.md` and `tests/host/test_swap_choice.cpp` (which uses a lag of 2) describe a path the game never takes. With a lag of 2 or more, the shown frame could step backwards. Make the docs and tests match the real behaviour, or make the fallback reachable.

**Blocked by:** None

**Status:** ready-for-agent

- [ ] A test shows the numbering recovers after a swap that issues no packet
- [ ] The README and tests describe the fallback the game actually takes at lag 1
- [ ] A Native scene run shows no "frame N shows Native's frame M" line and its dual verdict passes

## Comments

- 2026-10-27 (Claude): opened from the QA of 70a727b. QA confirmed the numbering itself: in `analysis/scenes/iq06b` the shown frame went up by exactly 1 on 4988 of 4989 presents.
- 2026-10-28 (owner, via Claude): fix both gaps on this branch before the merge. Delete the unreachable "frame before" fallback rather than make it reachable. Tests in `tests/host/test_swap_choice.cpp`.
