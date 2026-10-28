# 12: F6 developer overlay in developer builds only

**What to build:** Release builds leave out the SDK's F6 cvar overlay, so players have one settings screen. Developer builds keep it for developers and agents. Spec: `.scratch/game-settings/spec.md` (F6 developer overlay).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] In a release build, F6 does nothing (run evidence)
- [ ] In a developer build, F6 still opens the overlay
- [ ] The scripts and scene runner, which use developer builds, are unaffected
