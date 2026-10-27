# 06: Auto Resolution scale

**What to build:** An Auto choice for Resolution scale. It picks the smallest whole multiple of 1280x720 that covers the window (capped at 4, then clamped to the device). It is recomputed whenever the output size changes: on resize, when switching to fullscreen, or after moving the window to another monitor. It uses the live rebuild from ticket 04. Spec: `../spec.md`.

**Blocked by:** 04 (Resolution scale setting)

**Status:** ready-for-agent

- [x] Auto is accepted in the settings file and on the command line
- [x] Unit tests cover Auto at 720p, 1080p, 1440p, 4K and ultrawide outputs, and after a resize
- [x] A run that resizes the window changes the scale, and the log shows the new scale
- [x] Auto on a small window stays at 1x

## Comments

- 2026-10-27 (Claude): Verification of the Auto code from af95d5b; no fix to Auto was needed. Build and `pwsh scripts/build.ps1 -Action Check` pass (272 test cases, 23359 assertions; launcher 53).
- Box 1: settings file: `test_settings.cpp` round-trips `ts4_resolution_scale = "auto"`. Command line: the run below takes `--ts4_resolution_scale=auto` with no "not a value it allows" line.
- Box 2: `test_image_quality.cpp` "Auto picks ..." (1280x720 -> 1, 1920x1080 -> 2, 2560x1440 -> 2, 3840x2160 -> 3, 5120x2880 -> 4, ultrawide 3440x1440 -> 3, capped at 4, clamped to 3 on a 4096 device) and "Auto follows the window" (1600x900 -> 2, then 3840x2160 -> 3, then 1280x720 -> 1).
- Box 3, 4: `analysis/scenes/iq06/auto-resize-smoke/` (`pwsh scripts/smoke.ps1 -Seconds 85 -ScreenshotEvery 10 -InputScript analysis/scenes/iq06/auto-resize-smoke.txt -ExtraArguments --ts4_renderer=native,--ts4_resolution_scale=auto,--ts4_native_debug_max_image_size=2560`, with `scripts/resize-game-window.ps1 -Sizes 1920x1080,3900x2250,960x540,1280x720 -After 20 -Every 12` alongside; its output `resize.txt`). Alive at the deadline, no fatal, no black shots, 60 fps throughout. `runtime.log`: starts at 1280x720 and stays 1x; 369-373 swapchain 1904x1041, `Resolution scale 1x -> 2x at frame 1179, render size 2560x1440`, targets made again at 2560x1440; 396-398 swapchain 2564x1421 (Windows caps a window at the 2560x1440 screen), Auto asks 3x, `clamped to 2x` by the debug 2560-pixel limit; 417-420 swapchain 944x501 (small window), `2x -> 1x at frame 2613`, targets back to 1280x720; 442 swapchain 1264x681, stays 1x. Dumps follow the window's 16:9 area: 1850x1041 at 1200/1800, 2526x1421 at 2400, 1280x720 from 3000.
- Ticket 04 QA findings folded in: `anisotropic_override` changes now call `NoteFiltering` (a change callback registered in `Configure`), so a scripted `set anisotropic_override 5` is reported as forced filtering; `resolution_scale_clamped` in the report is now sticky (`clamped_once`); the allowed values live once in `image_quality.h` (`kResolutionScaleChoices` etc.) and a test ties them to `settings/registry.h`; the `scene_runner.py` docstring now says the headless dual frames are already 1280x720 from Native's own downsample.
- Not observed: the run above set `anisotropic_override 5` then `-1` (log 465-478) and was clamped then unclamped, but it was a smoke run without `--ts4_session_report`/`--ts4_exit_after_frame`, so no report was written; the two report fixes are untested in a run. No Oracle compare (Edge has no Auto scale; frames match ticket 04's scale runs). Fullscreen and a move to another monitor were not run (one monitor, hidden window); both reach Native through the same `OnWindowPixelSizeChanged` as a resize. A real 3x from Auto needs a window above 2560x1440, which this screen does not allow.
