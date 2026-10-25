# 23: King of the Court

**What to build:** The local party mode plays through.

**Blocked by:** 17 (Local 2P and doubles)

**Status:** ready-for-agent

- [ ] A King of the Court scene with several local players completes a round
- [x] Rotation of challengers works

## Comments
- 2026-10-25 (Claude), status check, no new run: `tests/scenes/king-of-the-court.toml` sets up four players (Federer as P1, three CPUs) at Rod Laver Arena and plays on; it passes in dual mode (`analysis/scenes/king-of-the-court/result.json`). Ticked the rotation box: frame 7000 of `run-1` shows the standings after the earlier matches (Davydenko 2 wins, Nadal 1, Federer and Simon 0) and the next match with Federer as the challenger against Davydenko as the champion, so the winner keeps the court and the next challenger comes in. Still open: the round has one local player, not several (ticket 17's second controller), and the scene stops before a round is completed.
