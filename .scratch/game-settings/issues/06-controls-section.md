# 06: Controls section

**What to build:** A Controls section on the Settings page lets players remap the 16 pad bindings and, in keyboard-and-mouse mode, the 24 key bindings, and reset Controls to the original layout. Pad bindings carry the next-start mark from ticket 04. Remapping is not in the Launcher. Spec: `.scratch/game-settings/spec.md` (User Stories 22–24).

**Blocked by:** 04 (Next-start and launch-option marks)

**Status:** ready-for-human

- [ ] Model tests: a remap is saved; a binding already used elsewhere is handled as WWE does (copied behaviour); reset restores every default
- [ ] A run remaps a pad button on the page; after the next start the game reads the new binding
- [ ] A remap can't lock the player out of the page (its own navigation keeps working)

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in ef38a9c; review fixes in b694c42. Host or Launcher tests cover: remap saved, conflict swaps, reset, lockout-proof navigation. Needs the owner's run: remap a pad button, restart, game reads new binding; key capture in mnk mode is untested code. No game run was made (owner tests).
