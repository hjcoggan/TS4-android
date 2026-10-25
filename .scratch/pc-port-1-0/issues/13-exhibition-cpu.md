# 13: Full Exhibition match vs CPU

**What to build:** A scene plays an entire Exhibition singles match against the CPU on Classic, from the main menu to the results screen. This is milestone M3.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene)

**Status:** ready-for-agent

- [ ] An Exhibition scene goes from the main menu through player and venue selection to a completed match
- [ ] Havok, OpenMP worker and XPS threading problems found on the way are fixed
- [ ] Audio plays during the match
- [x] Mid-match captures match the Oracle reference, apart from differences recorded in the Baseline
- [ ] The scene passes on several consecutive runs

## Comments

- 2026-10-25 (Claude): The port plays Exhibition on Classic. The owner played it with a real pad (`analysis/smoke-20261025-103703`): main menu, Exhibition, Singles, Federer v Nadal, AI difficulty, Rod Laver Arena, game settings, the match intro, then points with the score counting. They stopped after 119 s because the frame rate was bad. `scripts/input/boot-to-exhibition.txt` takes the same route unattended: A on every screen, a match by about frame 3300, and the A presses carry on into play. No box is ticked yet: the scene, audio, the Oracle comparison and repeat runs are still to do.
  - **Frame rate.** New `--ts4_fps_log` logs `fps: <rate> (guest swaps <n>)` once a second, and `scripts/smoke.ps1` turns it on. Measured with `smoke.ps1 -Headless -Seconds 100 -InputScript scripts/input/boot-to-exhibition.txt` (`analysis/smoke-20261025-104440`): menus 55-60 fps, the match intro 29-30 fps (the game's own halving, as on Edge), and **rallies 37-43 fps, where Edge holds 60**.
  - **Cause: the Classic render-target path (FSI) is GPU-bound** on the owner's AMD Radeon RX 7900 XT (`VK_EXT_fragment_shader_interlock`). With `REX_RENDER_TARGET_PATH_VULKAN=fbo`, the same run holds 60 in rallies (`analysis/smoke-20261025-104641`). But the fbo path still renders everything Swing draws black: dumps at frames 1400, 2400, 4000 and 4300 have max pixel 0, while the Loader's title at frame 480 renders.
  - **Edge doesn't have this bug.** It renders Swing correctly on its host render-target path (`render_target_path = "performance"`, on Vulkan too: the ticket 01 RenderDoc capture). So the fix to aim for is making the port's fbo path render Swing as Edge does, with Edge's render-target cache as the reference. Then fbo can be the fast default.
- 2026-10-25 (Claude): Frame rate fixed; Classic now runs host render targets (fbo), and Swing renders on them. Cause of the black frames on fbo: Swing ends each frame with a depth-only pass of unclipped point draws (occlusion queries) into a depth target at EDRAM base 720, 4x MSAA. With `execute_unclipped_draw_vs_on_cpu` off (rexglue's default; Edge runs it on), that pass is estimated at full render-target height. It wraps around the 2048-tile EDRAM and takes ownership of tiles 0-719 from the colour target, so the front-buffer resolve dumped the depth buffer. Found with a RenderDoc capture (`analysis/rdoc-fbo-1000`): the colour target held the frame, and the resolve's dump dispatch read a D32S8 image. Tagged resolve logs then named the owner. `src/topspin4_app.h` now sets `fbo` and `execute_unclipped_draw_vs_on_cpu=true`; both can be overridden from the command line or the environment.
  - `scripts/input/boot-to-exhibition.txt` on the new default (`analysis/smoke-20261025-110104`): rallies 60 fps, close-ups 30 (as on Edge), menus 55-60. Every dump renders. The mid-match frames match Edge's `ref-300.png` by eye.
  - The regression guard is the `boot-main-menu` scene: it passes on the default, and with `REX_EXECUTE_UNCLIPPED_DRAW_VS_ON_CPU=false` it fails, since frame 1400 is black.
  - Still in the log: `Failed to request vertex buffer at 0x-3FEFFF00` with a failed draw, 384 times in the owner's match. Not looked at yet.
- 2026-10-25 (Claude), no game run (owner's request): the `Failed to request vertex buffer at 0x-3FEFFF00` draws. Research (`docs/research/recomp-community-help.md`, section 1) traced them:
  - The address is `0xC0100100` (printed signed): a mirror-window address that ReXGlue hands to shared memory unmasked, so the request fails and the draw is dropped.
  - Edge masks vertex-fetch, index-base and memexport addresses to physical, on the CPU side and in the shaders (`2fce2d8`), and its log shows no such failure.
  - Canary labels Top Spin 4 `gpu-xps`, and both Modules import `KeLockL2`, so the geometry is probably XPS.
  Ported Edge's fix as patch `0034-mask-fetch-addresses`. The game builds; whether the dropped geometry now draws is unobserved. To check on the next allowed run: the error count in the log during a match, and the frames against Edge.
  Two follow-ups if geometry comes out stale: ReXGlue doesn't write-watch the `0x7F000000` XPS window (Canary `2270830` makes it a physical heap), and its vertex-buffer residency cache never re-uploads a buffer rewritten in place (rexglue #510; Edge dropped the cache in `c2674b1`).
- 2026-10-25 (Claude): patch `0034-mask-fetch-addresses` observed. The owner's run of `scripts/input/boot-to-exhibition.txt` (`analysis/smoke-20261025-114304`, 100 s, alive at the deadline) logged 0 `Failed to request vertex buffer` and 0 `Failed in backend` lines through the match, against 384 of each in the owner's earlier match (`analysis/smoke-20261025-103703`). The fps log reads as before: rallies at 60, cutaways at 30. Mid-match captures were not compared in this run, so whether the geometry that used to be dropped now draws correctly is still to be checked against Edge.
- 2026-10-25 (Claude): ticked the mid-match capture box, by eye. A headless run of `scripts/input/boot-to-exhibition.txt` (`analysis/smoke-20261025-114924`, 130 s) dumped frames 5600, 5900, 6200 and 6500. Compared with Edge's `analysis/edge-baseline-1/ref-300.png` and `analysis/edge-20261025-101451/shot-120s.png`:
  - Rally frames 5600 and 6500, at full size against `ref-300` scaled to 720p: the HUD (logo, score bug, speed readout), the Australian Open boards, the Melbourne lettering, net, net-post sign, line shadows, ball kids, line judges, camera operator and court colour all match.
  - Frame 5900 is the player close-up with the name card, and 6200 the 2K replay wipe, both as on Edge.
  - Nothing missing or corrupt, including the geometry patch 0034 restored; 0 vertex-buffer failures again.
  - Caveat: these are different moments of different matches, so it is a by-eye match, not a frame-for-frame one. A scene that reaches the same moment on both is the stronger check, for the scene box.
- 2026-10-25 (Claude), status check: `tests/scenes/exhibition-match.toml` goes from boot through player and venue selection into the match and captures the first points (frames 2400-4800). It is Native's parity scene (ticket 27, resolved), but it stops mid-match, so the first box stays open until the scene reaches the results screen. Its two Classic runs differ from each other on every match frame (the CPU's play and load times vary), so the repeat-run box needs a deterministic check (a log check or the dual verdict), not pixels. For the threading box: the session report's `xps_window_draws` is 0 over the match (ticket 27), so the match draws nothing from the XPS window. Havok and OpenMP have not been looked at.
