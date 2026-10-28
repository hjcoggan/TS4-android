# 07: Pad combo opens the Settings page

**What to build:** A controller player opens the Settings page mid-match with a pad combo the game never reads. First confirm which inputs Top Spin 4 reads. Clicking both sticks is ruled out, because the game uses them. Then pick a combo, such as holding Back and Start, and record the evidence. Spec: `.scratch/game-settings/spec.md` (Opening the page).

**Blocked by:** 03 (Settings page tracer bullet)

**Status:** ready-for-agent

- [ ] Evidence, cited in Comments, that the chosen combo triggers nothing in game (menus, match, pause)
- [ ] A scene run opens the page with the combo mid-match and closes it again
- [ ] The combo doesn't also reach the game as input when it opens the page
