# 09: Safe start

**What to build:** `--safe` starts one session on all default settings. The Settings file is neither applied nor written, and the log and session report say it is a Safe start. Changes made on the Settings page during a Safe start last only until the game closes, and their rows say so. Nothing offers Safe start automatically. Spec: `.scratch/game-settings/spec.md` (Safe start).

**Blocked by:** 04 (Next-start and launch-option marks)

**Status:** ready-for-human

- [ ] Model tests: under Safe start every row shows its default, changes aren't saved, and the rows are marked session-only
- [ ] A run with `--safe` and a Settings file holding non-default values draws with the defaults, and the file is byte-identical afterwards
- [ ] The log and session report record Safe start

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in ef38a9c; review fixes in b694c42. Host or Launcher tests cover: defaults shown, nothing saved, session-only marks, next-start rows locked. Needs the owner's run: --safe with a non-default file draws defaults, file byte-identical, log and session report say Safe start. No game run was made (owner tests).
