# 30: 1.0 release candidate and Steam Deck

**What to build:** A packaged 1.0 build that a player can install, point at their disc and TU1, and play on Windows and Steam Deck. This is milestone M6.

**Blocked by:** 11 (Crash reports and session logs), 12 (Disc and TU1 preparation), 15 (Online menus fail cleanly), 16 (Locked 60 with even frame pacing), 24 (Remapping and keyboard/mouse), 25 (Agassi outfit unlock and 3D forced off), 28 (Resolution scale and MSAA), 29 (Native parity everywhere, made default)

**Status:** ready-for-agent

- [ ] A packaged build contains no game data
- [ ] First run takes the player through disc and TU1 preparation
- [ ] Steam Deck under Proton holds 60 fps in a match, with default bindings working
- [ ] Saves and settings land in the expected Proton path
- [ ] Every 1.0 scene passes on the packaged build
- [ ] The owner play-tests and accepts

## Comments
- 2026-10-28 (Claude): the launcher UI (first-run disc and TU1 preparation, Play, the before-start settings, Safe start) is specified in `.scratch/game-settings/spec.md` and tracked in its tickets. Those tickets cover the launcher; the release-candidate boxes here stay as they are.
