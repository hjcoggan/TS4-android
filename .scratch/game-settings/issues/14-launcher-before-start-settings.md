# 14: Launcher: before-start settings

**What to build:** The Launcher shows the settings that only make sense before start (renderer, GPU), read from the game's settings export (ticket 02). It writes them to the same Settings file, using the same line-preserving rules and the file version (ticket 01). When the game exits with the GPU failure code, the Launcher tells the player to switch renderer. Spec: `.scratch/game-settings/spec.md` (Launcher; User Stories 30–32).

**Blocked by:** 01 (Settings file version and migrations), 02 (Settings export), 13 (Launcher: setup and Play)

**Status:** ready-for-agent

- [ ] Launcher.Tests: reading the export; writing a setting into an existing file leaves the other lines untouched; the GPU failure exit code maps to the switch-renderer message
- [ ] The Launcher keeps no list of settings of its own
- [ ] After the renderer is changed in the Launcher, Play starts on that renderer, and the Settings page shows the same value
