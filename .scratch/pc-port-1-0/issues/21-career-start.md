# 21: Career start

**What to build:** A player is created in the Player Creator and plays the first Career tournament, and the save round-trips.

**Blocked by:** 13 (Full Exhibition match vs CPU), 14 (Saves)

**Status:** ready-for-agent

- [ ] A Player Creator scene creates a male and a female player
- [ ] A Career scene plays the first tournament match
- [ ] Career progress survives save, quit and reload

## Comments
- 2026-10-25 (Claude), status check, no new run: `tests/scenes/career-start.toml` creates a male player with the default look in the Player Creator, saves him to the first slot, loads him, and goes through the Career tutorials to the Career main screen and the season calendar (frames 2880-7200). It passes in dual mode, worst tile 0 on every capture (`analysis/scenes/career-start/result.json`). The save at the end of the Player Creator needed 94 vcall thunks seeded (`86197b2`). No box ticked: no female player is created, no tournament match is played, and no quit and reload of the career has been run (and ticket 14's per-user save path is unobserved).
