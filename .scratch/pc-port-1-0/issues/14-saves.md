# 14: Saves

**What to build:** Saves and options are stored per user and round-trip, in a layout that can be exchanged with Xenia Edge.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene)

**Status:** ready-for-agent

- [ ] Saves are written to the per-user directory (APPDATA on Windows, the XDG data path under Proton)
- [ ] A changed option survives save, quit and relaunch, checked by a scene
- [ ] A save written by the port loads in Xenia Edge, and a save from Edge loads in the port
- [ ] Unit tests cover save-path resolution
