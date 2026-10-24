# 16: Locked 60 with even frame pacing

**What to build:** The game runs at its original 60 fps tick with even pacing on any display, and never speeds up on high-refresh screens.

**Blocked by:** 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [ ] Game speed is correct on 60, 120 and 144 Hz displays
- [ ] Frame-time variance during a match meets the WWE pacing bar
- [ ] Spin-wait and fence behaviour does not cause hitches
- [ ] Measured with the WWE frame-pacing tooling
