# 04: Next-start and launch-option marks

**What to build:** Every row on the Settings page tells the player when its change takes effect. A next-start setting, such as the renderer, is saved, marked "Applies the next time the game starts" and shown as pending; it isn't applied this session. A setting given as a Launch option (command line or `REX_*`) shows "Set by launch options: a change here lasts until the game closes" and is never saved. Spec: `.scratch/game-settings/spec.md` (User Stories 14, 15, 25; Settings page model; precedence).

**Blocked by:** 03 (Settings page tracer bullet)

**Status:** ready-for-agent

- [ ] Model tests: a next-start change is saved, pending and not applied; a pinned setting is marked and not saved when changed
- [ ] A run changes the renderer on the page: the file holds it, this session keeps its renderer, and the next start uses the new one
- [ ] A run started with `--ts4_anisotropic_filtering=16` shows that row as set by launch options, and the Settings file is unchanged afterwards
