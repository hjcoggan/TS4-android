# 01: Settings file version and migrations

**What to build:** The Settings file carries a version, and settings are kept across updates: an ordered list of migrations runs on load before any value is applied, so a renamed or removed setting won't misread an old file. The list starts empty. Spec: `.scratch/game-settings/spec.md` (Settings file schema).

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] A new Settings file is written with `version = 1`
- [ ] A file with no version loads as version 1, and its values are kept
- [ ] A test-only migration renames a key and keeps its value
- [ ] A file whose version is newer than the build is read as well as possible and never rewritten
- [ ] Saving one key still rewrites only that line, atomically
- [ ] Existing settings tests (defaults, invalid values, corrupt file) still pass
- [ ] One player-style run (no `--user_data_root`) with an old unversioned file shows the values applied and the file versioned

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in 891f4a6; review fixes in b694c42. Host or Launcher tests cover: versioned new file, unversioned file loads as 1, test migration rename, newer file not rewritten. Needs the owner's run: a player-style start with an old unversioned file. No game run was made (owner tests).
