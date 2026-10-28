# 02: Settings export

**What to build:** The game describes its own settings, so the Settings page and the Launcher read one list. Each registry setting gains a section (Graphics, Controls, Gameplay and so on), a display label and an order. It also says whether it applies now or at next start, taken from its cvar lifecycle and never written by hand. A command-line flag prints the whole list in a stable machine-readable form and exits without needing game data. Spec: `.scratch/game-settings/spec.md` (registry, settings export).

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] Every player setting has a section, label and order
- [ ] "Applies now or next start" comes from the cvar lifecycle; a test fails if the two disagree
- [ ] The export flag prints every setting with key, kind, choices, default, applies-when, renderers, section, label and help, then exits
- [ ] It runs with no game data present
- [ ] A test reads the export back and gets the same registry

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in 891f4a6; review fixes in b694c42. Host or Launcher tests cover: section/label/order on every setting, applies-when vs cvar lifecycle, export round-trip; export carries settings_file_version. Needs the owner's run: run topspin4.exe --print-settings-schema with no game data and check it exits. No game run was made (owner tests).
