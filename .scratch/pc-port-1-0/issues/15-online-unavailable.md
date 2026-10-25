# 15: Online menus fail cleanly

**What to build:** Player Match and World Tour fail with a clean "unavailable" message straight away, and no online timeout ever blocks offline play.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene)

**Status:** ready-for-agent

- [ ] Entering Player Match shows an unavailable message without hanging
- [ ] Entering World Tour shows an unavailable message without hanging
- [ ] Backing out returns to a working menu
- [ ] No network wait happens on any offline path
- [ ] A scene covers both menus

## Comments
- 2026-10-25 (Claude), ticket audit: selecting Xbox LIVE on the main menu (headless run, `analysis/xboxlive-input.txt`: DOWN, A at frame 1500) takes the highlight off the menu and then nothing happens through frame 2450: no message, no submenu, and B at 2300 does not bring the highlight back. Nothing is logged after the press, so the game waits silently, most likely on sign-in or the title-server login. That is the hang this ticket removes; no box is met yet. The boot, title, practice-ground and main-menu paths show no network wait.
- 2026-10-25 (Claude), reverse engineering only, no game run (owner's request). Leads in Swing_DLL (TU1), for whoever builds the hooks:
  - The main menu's Xbox LIVE item carries the action string `ConnectToLive` (`0x880D58F4`). `0x889363E8` walks the menu's data tree (hashed keys `0x542B527C`, `0xEE3FCD0D`, `0xFE11D138`, `0xDCB67730`) and, for that item, sets a flag in the menu object (key `0x38E27355`) from the item's own data node through the generic getter `0x881647A8`.
  - That pass is skipped entirely while the byte at `[0x88D45B1C] + 0x276` is non-zero. That byte is a candidate "online available" switch: setting or clearing it may grey out the item instead of hanging. Unconfirmed.
  - World Tour's lobby screen is looked up by name, `S4WorldTourLobby` (`0x880D90DC`), through the screen manager at `[0x88D41FC4] + 0x6C` (`0x88906D40`). `CWorldTourManager` and `CLiveAware_Swing` are the class names to look for next.
  - The ticket audit's hang, where selecting Xbox LIVE waits silently, is most likely sign-in or session creation. The string "User is not logged on to Live but attempted to create a session using Live features." (`0x880D2948`) has no code reference, so it is a leftover or reached indirectly.
  - Next step, which needs a run: break or log at `0x889363E8` and at the `ConnectToLive` action's handler while selecting Xbox LIVE, to see which wait the game sits in.
