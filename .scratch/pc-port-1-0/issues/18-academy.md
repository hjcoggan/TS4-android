# 18: Academy

**What to build:** Academy tutorials and the practice ground play through.

**Blocked by:** 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [ ] An Academy lesson scene completes
- [ ] A practice-ground scene loads and plays
- [ ] Captures match the Oracle references

## Comments
- 2026-10-25 (Claude), status check, no new run: `tests/scenes/academy-lesson.toml` takes Game Modes, Top Spin Academy, to the first basic lesson ("How to return the ball") and captures the lesson menu, the coach's welcome, the lesson card and the lesson on court (frames 2200-4300). It passes in dual mode (Native against Classic on the same frame, worst tiles 18, 10, 0, 0, 0; `analysis/scenes/academy-lesson/result.json`). No box ticked: the scene stops inside the lesson rather than completing it, the practice ground has no scene of its own yet (it only appears on the way to the main menu), and the captures were not compared with Edge.
