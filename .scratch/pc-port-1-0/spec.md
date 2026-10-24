# Top Spin 4 PC port 1.0

Status: ready-for-agent

## Problem Statement

Top Spin 4 has only ever been on consoles, and Xbox 360 hardware is ageing. Today the only way to play it on a PC is Xenia. Under Xenia the game reaches a full match, but upscaling crashes it, the frame rate is tied to game speed, the video cross-fades flicker, and every boot waits on a title server that died years ago. Someone who owns the disc has no native, stable way to play the offline game on Windows or Steam Deck at modern resolutions.

## Solution

A native PC build of the Reference build (the 360 disc, media `60819732`, with TU1), made by recompiling all three Modules (Boot, Loader, Swing) to C++ with a pinned ReXGlue. The player points it at their own disc image and TU1, and the port prepares everything else locally. It plays every offline mode (Exhibition, Career, Academy, King of the Court) faithfully at a locked 60 fps on the Native renderer. Resolution scaling, MSAA and button remapping are Enhancements, and dead online features fail cleanly and immediately. Original behaviour is the default, and every Enhancement is opt-in.

Development is set up for the fastest possible iteration loop: a scripted smoke run first, then scripted scene runs, with comparison against the Oracle (Xenia Edge) kept at milestone boundaries.

## User Stories

### Getting the game running

1. As a player, I want to point the port at my own Top Spin 4 disc image, so that I don't have to extract files by hand.
2. As a player, I want to point the port at my own TU1 package, so that the port can apply the update I own.
3. As a player, I want the port to tell me clearly when my disc isn't the Reference build, so that I don't chase crashes caused by the wrong input.
4. As a player, I want the port to refuse to start without TU1 and say why, so that I never run an unsupported build by accident.
5. As a player, I want disc preparation to happen once and be reused on later launches, so that starting the game is fast.
6. As a player, I want the port to tolerate a region-free or security-sector-stripped image with the same game partition, so that common legitimate dumps work.
7. As a player, I want no game data ever bundled with the download, so that the project stays legally distributable.

### Booting

8. As a player, I want the game to reach the title screen without waiting on the dead title server, so that boot is as quick as the console offline.
9. As a player, I want the "checking for DLC" step to finish immediately, so that I'm not shown a stale server error at boot.
10. As a player, I want the boot and loading screens drawn by Loader to look and behave as on the Oracle, so that the start of the game is faithful.
11. As a player, I want the hand-off from Loader to Swing to work every time, so that the game never hangs between boot and the menus.

### Offline play

12. As a player, I want to play a full Exhibition match against the CPU, so that I can play the game's core.
13. As two local players, we want to play an Exhibition match against each other with two controllers, so that couch play works.
14. As a player, I want doubles Exhibition matches to work, so that every match format on the disc is playable.
15. As a player, I want to create a player in the Player Creator, so that I can start a Career.
16. As a player, I want to play a Career start to finish, through calendar, tournaments, XP and rankings, so that the single-player mode is complete.
17. As a player, I want Career ranking points to behave as TU1 fixed them, so that the career isn't broken by the base-build bug.
18. As a player, I want to play the Academy tutorials and practice ground, so that I can learn the controls.
19. As local players, we want to play King of the Court, so that the party mode works.
20. As a player, I want every court and venue on the disc to load and render, so that no tournament crashes on its venue.
21. As a player, I want every licensed player's model, face and animations to render correctly, so that the roster looks right.
22. As a player, I want opponents' faces to render correctly, so that the readback-dependent face rendering isn't broken.
23. As a player, I want cutscenes and video cross-fades to play without flicker, so that presentation matches the console.
24. As a player, I want audio (crowd, umpire, effects, music) to play correctly and in sync, so that matches sound right.
25. As a player, I want D-pad movement to work as TU1 enabled it, so that I can use the D-pad.
26. As a player, I want the game simulation to run at its original speed, so that timing-based shots feel right.

### Saves and settings

27. As a player, I want my Career progress, created players and options to save and load, so that I can continue later.
28. As a player, I want camera settings to persist, as TU1 fixed, so that I don't reset them each session.
29. As a player, I want saves stored in a per-user directory, so that they survive reinstalling the port.
30. As a player, I want my Career save to be copyable between Xenia Edge and the port in either direction, so that I can move existing progress over.
31. As a Steam Deck player, I want saves and settings in the expected path under Proton, so that Steam Cloud and backups pick them up.

### Online

32. As a player, I want Player Match and World Tour menus to fail with a clean "unavailable" message, so that nothing hangs or crashes when I open them.
33. As a player, I want no online timeout ever to block an offline mode, so that offline play is never slowed by dead servers.

### Rendering and Enhancements

34. As a player, I want the Native renderer used by default, so that I get scaling and MSAA without extra setup.
35. As a player, I want to switch to the Classic renderer in settings, so that I have a fallback if Native misbehaves on my machine.
36. As a player, I want to raise the internal resolution beyond the original, so that the game is sharp on modern displays.
37. As a player, I want to enable MSAA, so that edges are clean.
38. As a player, I want the default settings to look like the original, so that Enhancements are always my choice.
39. As a player, I want a steady, locked 60 fps with even frame pacing, so that play is smooth.
40. As a player, I want the game to stay at 60 fps on a high-refresh display rather than speeding up, so that game speed is never wrong.

### Input

41. As a player, I want any XInput-compatible controller to work out of the box, so that I can play straight away.
42. As a player, I want to remap every game action on a controller, so that I can make the shoulder-modifier controls comfortable.
43. As a player, I want to play with keyboard and mouse, with remappable bindings, so that I can play without a controller.
44. As a player, I want my bindings saved per user, so that I set them once.

### Extras

45. As a player, I want an option to unlock the Agassi preorder outfit, so that content on my disc isn't locked away.
46. As a player, I want the 360's stereoscopic 3D mode left out cleanly, so that it never activates by accident.

### Steam Deck

47. As a Steam Deck player, I want the game to run at 60 fps under Proton, so that it's playable handheld.
48. As a Steam Deck player, I want the Deck's controls to work with default bindings, so that no configuration is needed.

### Stability and diagnostics

49. As a player, I want a crash to produce a report I can attach to an issue, so that bugs can be fixed.
50. As a player, I want the port to keep logs from the last session, so that problems can be diagnosed.

### Development

51. As the developer, I want a smoke run that launches the build for a bounded time and reports crashes, unregistered call targets and missing jump tables, so that codegen gaps are found in seconds.
52. As the developer, I want each Module built as its own unit, so that changing a Swing seed or hook doesn't rebuild Loader and Boot.
53. As the developer, I want scene runs (scripted input from boot plus guest frames to capture, exiting on their own), so that I can reach any game state reproducibly without playing by hand.
54. As the developer, I want Native captures judged automatically against Classic captures of the same scene, so that renderer regressions are caught without eyeballing.
55. As the developer, I want Classic captures cached per scene, so that iterating on Native costs only Native runs.
56. As the developer, I want reference captures from the Oracle for each milestone scene, so that milestone acceptance compares against real game behaviour.
57. As the developer, I want a recorded Oracle Baseline before codegen, so that I know which glitches are Original bugs or Oracle bugs rather than port bugs.
58. As the developer, I want our ReXGlue and XenosRecomp changes kept as numbered patches applied by bootstrap, so that a fresh clone rebuilds the exact toolchain.
59. As the developer, I want topspin4-decomp's symbols imported into the Ghidra project, so that hooks and seeds use readable names.
60. As the developer, I want the repo to reject game data, decrypted executables and generated code, so that nothing illegal is ever committed.
61. As the owner, I want each milestone to come with a play-test build and a short report, so that I can accept or reject it.

## Implementation Decisions

- **Reference build only (ADR-0002).** All analysis, seeds, hooks and manifests target the TU1 images (all Modules v0.0.1.3). Addresses from the base build are never used. A Reference build check, using the media ID, module versions and TU1 package identity, guards both preparation and launch.
- **Hybrid recompilation (ADR-0003).** ReXGlue v0.10.0 is pinned. Boot is the entry point, and Loader and Swing are module entries, each compiled to its own host library. Neither DLL imports from another title module, so all cross-module calls go through the runtime's module loading and procedure lookup. The runtime must handle Boot loading Loader, Loader loading Swing and then unloading itself while handing off its thread (`XexUnloadImageAndExitThread`), and Swing's own load and unload calls. Expect our own ReXGlue patches for this path.
- **No guest code from outside the Reference build.** `DLC_RestartGameNewDLL` and any other attempt to load a module not in the build is refused cleanly.
- **Code copied from WWE All Stars (ADR-0004).** The project skeleton, applicable runtime patches, Native renderer, launcher, crash reporting, settings system and test harness are copied in and adapted. No shared library is extracted.
- **Two renderers, Vulkan only (ADR-0005).** The Classic renderer is built first and remains the reference. The Native renderer is the 1.0 default and must handle: two statically linked D3D9 copies (Loader and Swing each have their own, so D3D hooks are generated per Module), resolve-to-texture readback (faces), XPS CPU-generated geometry, and render targets it owns, for scaling and MSAA. D3D12 is not built.
- **Hooks replace these guest systems:** title-server login and patch request (short-circuited to the offline result), the DLC check, online session entry points (a clean "unavailable"), the frame and tick clock (locked 60), settings and Enhancements, input (remapping, keyboard and mouse), the preorder-outfit unlock, and the stereoscopic 3D switch (forced off).
- **Enhancements default to original behaviour (ADR-0001).** Resolution scale and MSAA work only on the Native renderer. The settings UI shows them as unavailable on Classic.
- **Saves:** a per-user directory (APPDATA on Windows, the XDG data path under Proton) holds saves, settings and bindings. Save content stays in the game's own format, laid out so it can be exchanged with Xenia Edge's content directory.
- **Disc preparation:** the launcher takes a disc image and a TU1 package, validates them, extracts the game data, applies TU1, and caches the result per user. Nothing derived from the game is shipped or committed (ADR-0006).
- **Toolchain reproducibility:** bootstrap fetches pinned upstream tools and applies numbered patches from the repo. Generated C++ is produced locally and git-ignored.
- **Milestones gate the work:** M0 Oracle Baseline, M1 all Modules generate and compile, M2 title screen on Classic, M3 full Exhibition match, M4 every 1.0 mode with saves, M5 Native parity, M6 Enhancements, launcher and Steam Deck pass.

## Testing Decisions

- **A good test checks behaviour the player or developer can see** (a screen is reached, a frame matches, a save round-trips, a setting persists), never the internals of generated code or hook implementations.
- **The test seams are staged so each stays the fastest available loop for its milestone:**
  - **Smoke run (from M1):** the built executable runs for a bounded time and the log is parsed for crashes, unregistered call targets, missing jump-table entries and stalls. This drives codegen and seed fixing through M2. Prior art: WWE All Stars' smoke script.
  - **Scene run (from M3):** the built executable is driven by a scene (scripted input from boot plus guest frame numbers to capture). It dumps those frames, exits on its own after the last one, and writes a session report. Planned scenes: boot to title, title to main menu, a full Exhibition match, a Career day, an Academy lesson, a King of the Court round, and save, quit and reload. Prior art: WWE All Stars' scene runner and its input scripts.
  - **Native against Classic (from M5):** each Native capture is judged per pixel against the envelope of several Classic runs using FLIP, with tile-based failure thresholds. Any renderer fallback or skipped draw in the session report fails the scene. Classic captures are cached per scene. Prior art: WWE All Stars' scene runner.
  - **Oracle comparison (at milestones):** reference captures of each milestone scene are taken from Xenia Edge (semi-manually, because Edge can't be driven by the port's flags). Classic captures are compared with them when a milestone is accepted. Divergence counts as a port bug unless it is shown to be an Oracle bug or an Original bug recorded in the Baseline.
  - **Host unit tests (as needed):** only for host-only logic that's easy to get wrong: Reference build validation, settings persistence and defaults, save-path resolution, input-binding resolution. Prior art: WWE All Stars' core unit tests.
- **Runs never overlap**, because the game animates by elapsed time and a slowed run reaches a different state.
- **The owner play-tests at every milestone.** That is the acceptance step; automated runs are the per-change gate.

## Out of Scope

- Frame rates above 60 (needs a decoupled tick or interpolation): after 1.0.
- Mod loading (the `UPDATE:` / patch-path overlay, VFS overlay, script-command hooks, code mods): designed for, shipped after 1.0.
- Reviving Player Match (LAN, system link, relay): after 1.0.
- Skipping intro videos, and an XP / career-pace slider: after 1.0.
- A native Linux build: after 1.0, once ReXGlue's Linux module loading works.
- World Tour and 2K Open servers, and recovering the lost server-delivered balance patches.
- The base (pre-TU1) build, PS3, Wii, macOS and D3D12.
- Loading new guest code from DLC packages.
- Stereoscopic 3D output.
- Ultrawide aspect ratios (needs HUD and camera work).

## Further Notes

- Background research: the feasibility study and module inspection notes under `docs/research/`. Terms: `GLOSSARY.md`. Decisions: ADR-0001 to ADR-0006.
- Open facts that M0 must establish: native resolution and aspect ratio, load times, whether 360 drops to 30 fps between points, cross-fade flicker on Edge, and whether the Swing export ordinals are data tables.
- Highest schedule risk: ReXGlue's multi-module path (known open issues for secondary-module procedure lookup, module image writability, and reload after unload). Budget a patch series for it during M1 and M2.
- The game is script-driven (named script commands registered by Swing). Script-command registration is a natural hook point for the online stubs now and for mods later.
- OpenMP (VCOMP) worker threads and Havok mean spin-wait and fence work similar to WWE All Stars' should be expected.
