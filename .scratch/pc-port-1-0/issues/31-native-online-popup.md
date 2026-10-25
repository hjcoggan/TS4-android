# 31: Online-unavailable message in the game's own popup

**What to build:** Selecting Xbox LIVE shows "Online features are not available yet. They are planned for future versions." in the game's own yellow WARNING popup (as "Would you like to go to the Title Screen?" is drawn), not in the system message box, and returns to the main menu.

**Blocked by:** 15 (Online menus fail cleanly)

**Status:** needs-triage

- [ ] The popup shows the message in the game's style, with one button, from Xbox LIVE on the main menu
- [ ] Its button returns to a working main menu, and no sign-in UI opens
- [ ] The text comes from our own string, so it reads the same in every game language (or each language gets its own)
- [ ] A scene covers it, capturing the popup in a frame dump

## Comments
- 2026-10-25 (Claude), from ticket 15's RE (Swing, TU1):
  - The yellow WARNING popups are UI-script screens. `0x887BF148` is the script-callable push: for `SPopupSave` (`0x880D1ED8`), `S4PopupIngameMessage` (`0x880EAD30`), `S4PopupIngameSave` (`0x880EAD48`) and two names at `0x880EAD10` / `0x880EAD1C`, it reads the popup's settings from script variables under hashed names (`0x4FFB33A7`, `0x21F5F328`, `0xF8AEC349`, `0x5BA33382`, `0x7519C486`, `0xB4636118`, `0x8CDF0229`, `0x0AA49E9C`, `0xCBA23822`, `0x11C8EAA1`) and resets them after.
  - The texts are localization IDs resolved from the data: neither "Would you like to go to the Title Screen?" nor the LIVE prompt is a string in the executable.
  - Xbox LIVE today goes through the system message box: `0x88886940` (generic job) calls `XamShowMessageBoxUI`, and its callback `0x888C6928` opens sign-in on YES. Ticket 15 rewrites that box's text (`src/online_unavailable.h`, patch 0036).
  - To find: how a text ID becomes a string (to add or intercept one), which hash is title, text and buttons, how the button's result comes back, and where to push the popup in place of the system box.
