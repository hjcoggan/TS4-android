# 15: Online menus fail cleanly

**What to build:** Player Match and World Tour fail with a clean "unavailable" message straight away, and no online timeout ever blocks offline play.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene)

**Status:** ready-for-agent

- [ ] Entering Player Match shows an unavailable message without hanging
- [ ] Entering World Tour shows an unavailable message without hanging
- [ ] Backing out returns to a working menu
- [ ] No network wait happens on any offline path
- [ ] A scene covers both menus
