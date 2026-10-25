# 13: Full Exhibition match vs CPU

**What to build:** A scene plays an entire Exhibition singles match against the CPU on Classic, from the main menu to the results screen. This is milestone M3.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene)

**Status:** ready-for-agent

- [ ] An Exhibition scene goes from the main menu through player and venue selection to a completed match
- [ ] Havok, OpenMP worker and XPS threading problems found on the way are fixed
- [ ] Audio plays during the match
- [ ] Mid-match captures match the Oracle reference, apart from differences recorded in the Baseline
- [ ] The scene passes on several consecutive runs

## Comments

- 2026-10-25 (Claude): The port plays Exhibition on Classic. The owner played it with a real pad (`analysis/smoke-20261025-103703`): main menu, Exhibition, Singles, Federer v Nadal, AI difficulty, Rod Laver Arena, game settings, the match intro, then points with the score counting. They stopped after 119 s because the frame rate was bad. `scripts/input/boot-to-exhibition.txt` takes the same route unattended: A on every screen, a match by about frame 3300, and the A presses carry on into play. No box is ticked yet: the scene, audio, the Oracle comparison and repeat runs are still to do.
  - **Frame rate.** New `--ts4_fps_log` logs `fps: <rate> (guest swaps <n>)` once a second, and `scripts/smoke.ps1` turns it on. Measured with `smoke.ps1 -Headless -Seconds 100 -InputScript scripts/input/boot-to-exhibition.txt` (`analysis/smoke-20261025-104440`): menus 55-60 fps, the match intro 29-30 fps (the game's own halving, as on Edge), and **rallies 37-43 fps, where Edge holds 60**.
  - **Cause: the Classic render-target path (FSI) is GPU-bound** on the owner's AMD Radeon RX 7900 XT (`VK_EXT_fragment_shader_interlock`). With `REX_RENDER_TARGET_PATH_VULKAN=fbo`, the same run holds 60 in rallies (`analysis/smoke-20261025-104641`). But the fbo path still renders everything Swing draws black: dumps at frames 1400, 2400, 4000 and 4300 have max pixel 0, while the Loader's title at frame 480 renders.
  - **Edge doesn't have this bug.** It renders Swing correctly on its host render-target path (`render_target_path = "performance"`, on Vulkan too: the ticket 01 RenderDoc capture). So the fix to aim for is making the port's fbo path render Swing as Edge does, with Edge's render-target cache as the reference. Then fbo can be the fast default.
