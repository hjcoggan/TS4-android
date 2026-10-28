# 13: Launcher: setup and Play

**What to build:** A first-time player opens the Launcher, is walked through preparing the disc and TU1, and presses Play. Copy WWE All Stars' Avalonia launcher (ADR-0004), cut down to setup and Play, on top of the existing Launcher.Core preparation logic that the console tool uses. It is fully usable with a controller. After setup, starting the game goes straight into the game without the Launcher. Spec: `.scratch/game-settings/spec.md` (Launcher).

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] Launcher.Tests cover the setup flow's states and the Play arguments
- [ ] On a clean user folder, the first run prepares the disc and TU1 and starts the game
- [ ] Every screen can be used with a controller alone
- [ ] After setup, starting the game exe doesn't show the Launcher
- [ ] The console tool still works for scripts

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in 73fad16; review fixes in b694c42. Host or Launcher tests cover: setup states, Play arguments, pad mapping. Needs the owner's run: open the Launcher window, first-run setup on a clean user folder, controller-only use; starting topspin4.exe directly finds the prepared data (default roots added in ef38a9c). No game run was made (owner tests).
