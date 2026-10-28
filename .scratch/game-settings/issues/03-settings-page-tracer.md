# 03: Settings page tracer bullet

**What to build:** A player presses F4 in game and gets the Settings page, with a Graphics section listing the image Enhancements. They move with the D-pad or left stick, change a value with left and right, and see a description of the highlighted row. A live change applies at once behind the page and is saved to the Settings file. While the page is open the game reads an idle pad; it reads the real pad again when the page closes.

Copy WWE All Stars' ImGui page and page model (ADR-0004, ADR-0007), with the rows coming from a pure model over the registry from ticket 02. Scripts can open, move through and change the page through the existing script input, so scene runs test the real page. Spec: `.scratch/game-settings/spec.md` (Settings page model, Settings page UI).

**Blocked by:** 02 (Settings export)

**Status:** ready-for-human

- [ ] Model unit tests: rows, values, choices and descriptions for given registry and file values; a change yields the cvar to set and the value to save
- [ ] F4 opens and closes the page on Classic and on Native
- [ ] A scripted run opens the page and changes anisotropic filtering; the change shows in the frame and in the run's Settings file
- [ ] Pad input sent while the page is open doesn't reach the game (scene run)
- [ ] Native-only rows show as unavailable on Classic, with the reason
- [ ] All-default scenes still match the Oracle

## Comments

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in cfcbcb7; review fixes in b694c42. Host or Launcher tests cover: model rows, values, choices, help, change result. Needs the owner's run: F4 opens/closes the page on Classic and Native; live change applies and is saved; pad held while open (script: set settings_page open, then press UP/DOWN/LEFT/RIGHT/A/B). No game run was made (owner tests).
