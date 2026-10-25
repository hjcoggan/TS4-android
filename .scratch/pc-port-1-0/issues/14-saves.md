# 14: Saves

**What to build:** Saves and options are stored per user and round-trip, in a layout that can be exchanged with Xenia Edge.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene)

**Status:** ready-for-agent

- [ ] Saves are written to the per-user directory (APPDATA on Windows, the XDG data path under Proton)
- [ ] A changed option survives save, quit and relaunch, checked by a scene
- [ ] A save written by the port loads in Xenia Edge, and a save from Edge loads in the port
- [x] Unit tests cover save-path resolution

## Comments
- 2026-10-25 (Claude), no game run (owner's request). `src/user_paths.h` resolves the per-user folder as the launcher does (`UserDirectories.cs`): `%APPDATA%\TopSpin4Recomp` on Windows; under Proton the Linux user's `$XDG_DATA_HOME` (absolute only) or `~/.local/share`, through `Z:`. `TopSpin4App::OnConfigurePaths` makes it the user data root (where the SDK's content manager keeps saves) unless `--user_data_root` is given, as the developer scripts do. The shader cache moves beside the launcher's prepared data under `%LOCALAPPDATA%\TopSpin4Recomp\cache`. Ticked the unit-test box on tests/host/test_user_paths.cpp (Windows, Proton with and without `XDG_DATA_HOME`, Wine without a Linux home). The other boxes need runs: a save landing in that folder, the option round-trip scene, and the Edge exchange.
