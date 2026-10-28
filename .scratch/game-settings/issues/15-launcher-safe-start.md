# 15: Launcher: Safe start button

**What to build:** A player whose game won't start presses Safe start in the Launcher. The game starts with `--safe` (ticket 09) and leaves their Settings file untouched. Spec: `.scratch/game-settings/spec.md` (Safe start; User Stories 33–34).

**Blocked by:** 09 (Safe start), 13 (Launcher: setup and Play)

**Status:** ready-for-human

- [ ] Launcher.Tests: the Safe start arguments
- [ ] A Safe start from the Launcher draws with the defaults, and the Settings file is unchanged afterwards

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in f23ab08; review fixes in b694c42. Host or Launcher tests cover: Safe start arguments. Needs the owner's run: Safe start from the Launcher draws defaults and leaves the file unchanged. No game run was made (owner tests).

**2026-10-28, superseded:** ADR-0008 and `.scratch/launcher-ux/spec.md` replace this ticket's Launcher design where they differ (home screen, every setting, no automatic search). Kept as history; new work is in `.scratch/launcher-ux/issues/`.
