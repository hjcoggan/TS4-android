# 15: Launcher: Safe start button

**What to build:** A player whose game won't start presses Safe start in the Launcher. The game starts with `--safe` (ticket 09) and leaves their Settings file untouched. Spec: `.scratch/game-settings/spec.md` (Safe start; User Stories 33–34).

**Blocked by:** 09 (Safe start), 13 (Launcher: setup and Play)

**Status:** ready-for-agent

- [ ] Launcher.Tests: the Safe start arguments
- [ ] A Safe start from the Launcher draws with the defaults, and the Settings file is unchanged afterwards
