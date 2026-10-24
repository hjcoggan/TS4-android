# 08: Title screen on Classic, no server wait

**What to build:** The game reaches its title screen on the Classic renderer without waiting on the dead title server or the DLC check. This is milestone M2.

**Blocked by:** 07 (Hand-off to Swing), 04 (TU1 symbol map), 01 (Oracle Baseline)

**Status:** ready-for-agent

- [ ] Title-server login and patch request are short-circuited to the offline result through hooks
- [ ] The DLC check finishes immediately
- [ ] Boot and loading screens drawn by Loader display
- [ ] The title screen is reached reliably on several consecutive smoke runs
- [ ] A title-screen screenshot matches the Oracle reference capture, apart from differences recorded in the Baseline
- [ ] Boot-to-title time is no worse than the Baseline

## Comments

- 2026-10-24 (Claude): After START on Loader's title screen, Swing loaded and the screen stayed black. The game was running (frames presenting, menus loading, `d:\Baked_Federer0.arc` opened as on Edge), but every Swing frame rendered black on the host render-target path. Swing renders 2x MSAA frames in two predicated tiles (1280×384 + 1280×336). The FSI path draws them correctly, and WWE's Classic renderer was already FSI (its ADR-0003), so `TopSpin4App` now defaults `render_target_path_vulkan` to `fsi` (the command line still wins). With that, START reaches the practice ground about 4 s after the press, and START again shows the main menu (`scripts/input/boot-to-main-menu.txt`). No title-server or DLC wait shows up on this path: both DLC workers exit straight away, as on Edge. So the first two boxes may need no hooks; check what `Script_TitleServer_Login` does when Xbox LIVE is entered (ticket 15) before deciding. Not ticked yet: no Oracle reference capture to compare with (ticket 01), no timing against the Baseline, and no run of several consecutive smoke runs.
- Tried along the way and left out because the game reaches the menu without them: resetting the command processor's write pointer on a second `VdInitializeRingBuffer` (Swing sets up its own ring; the runtime keeps Loader's write pointer, which looks wrong but caused nothing visible), a writable `cache:` device (Edge has one, and the game writes `cache:\<gamertag>.txt`; the port gets "device not found" and carries on), and a real `XNetLogonGetTitleID` (the stub leaves `r3` as the caller's argument). Revisit them if a later screen depends on them.
