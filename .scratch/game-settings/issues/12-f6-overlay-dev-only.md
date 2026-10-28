# 12: F6 developer overlay in developer builds only

**What to build:** Release builds leave out the SDK's F6 cvar overlay, so players have one settings screen. Developer builds keep it for developers and agents. Spec: `.scratch/game-settings/spec.md` (F6 developer overlay).

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] In a release build, F6 does nothing (run evidence)
- [ ] In a developer build, F6 still opens the overlay
- [ ] The scripts and scene runner, which use developer builds, are unaffected

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in 891f4a6; review fixes in b694c42. Host or Launcher tests cover: none (build option TOPSPIN4_DEVELOPER_BUILD). Needs the owner's run: release build (-CMakeArgs '-DTOPSPIN4_DEVELOPER_BUILD=OFF'): F6 does nothing; developer build: F6 opens the overlay. No game run was made (owner tests).
