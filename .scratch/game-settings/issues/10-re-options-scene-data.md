# 10: RE: OPTIONS scene data and the Xbox 3D row

**What to build:** Find out how to add the PC SETTINGS row to the game's OPTIONS menu. Top Spin 4's menus are PAM's Lua-driven RGui system (see the addendum to `docs/research/game-settings.md`).

Decompress the OPTIONS scenes from the player's own archives (PAM-LZX) and read how their rows are laid out and bound. Confirm whether OPTIONS has an Xbox 3D row gated by `Is3DAccessible`. Recommend one route, either reusing the 3D row or adding a new row, and give the hook points (scene load, string-table lookup, activation) with addresses. Use the re-investigator agent. The answer goes into the research note; no code goes into the repo.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The OPTIONS scenes' row layout and data binding are described, with evidence
- [ ] Whether a 3D row exists and how it is gated is answered with evidence
- [ ] A route is recommended, with the hook addresses confirmed in Ghidra
- [ ] Whether `.actions` files are Lua source or bytecode is answered
- [ ] No game data or decompressed scenes are committed (ADR-0006)
