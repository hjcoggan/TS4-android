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
