# 07: MSAA 4x

**What to build:** A player setting for MSAA on the Native renderer: Original (the game's own 2x on the match pass) or 4x. It raises the sample count only for targets the game creates multisampled; targets the game creates at 1x stay at 1x. A sample count the device doesn't support falls back to the highest supported count at or below the request. It changes live, or takes effect at the next start if a live change proves unstable. On Classic it is ignored and logged as unavailable. Spec: `../spec.md`.

**Blocked by:** 01 (Image-quality plan prefactor)

**Status:** ready-for-agent

- [x] The setting loads from the settings file and the command line, and an invalid value falls back to Original
- [x] At 4x, the match pass renders at 4 samples and 1x passes are unchanged (render-target log lines)
- [x] Alpha-masked surfaces (Academy trees) look right at 4x
- [x] The match frame at 4x is compared with the Original frame of the same run: only edge differences
- [x] Unit tests cover the plan's sample count for 1x and 2x targets and the unsupported-count fallback
- [x] The setting is listed in the session report's enhancements and in the scene runner's list

## Comments

- 2026-10-27 (Claude): Verification of the code from af95d5b; no source change was needed for MSAA. The live-change run uses the new `set` command in the scripted pad (ticket 02's comment). Build and `pwsh scripts/build.ps1 -Action Check` pass (270 test cases, 23342 assertions; pytest 88; launcher 53).
- Box 1: `test_settings.cpp` (registry default `original`, choices `original`/`4`/`8`; a settings-file round trip with `ts4_msaa = 4`; a typo in the file keeps the default), `test_image_quality.cpp` (`ParseSettings`: `4`, `8`, `original`; `2` gives Original). On the command line, `--ts4_msaa=4` (the dual run below) gives the 4x targets.
- Box 5: `test_image_quality.cpp`: 4x and 8x raise a 2x target and leave a 1x target at 1x; without 8x on the device, 8x falls back to 4x; on a device with only 1 and 4, Original's 2x becomes 1 and 4x stays 4.
- Box 2 and 4: `python scripts/scene_runner.py tests/scenes/exhibition-match.toml --runs 0 --native 1 --dual 1 --enhance ts4_msaa=4`, out `analysis/scenes/iq07/exhibition-match-msaa4/` (`../exhibition-match-msaa4.log`). Render-target lines, `native-1` (Original) against `dual-1` (4x): colour and depth 1280x384 and 1280x720 (the match pass, key 450000000025000 / 8400050000000300) are `2x` in `native-1` and `4x` in `dual-1`. Every other target is `1x` in both: 1280x720, 1040x1024 and 640x360 (in both), and 160x128, 320x64 and 560x128 (only in `dual-1`, which runs on to frame 4800). Dual verdicts (Native at 4x against Classic's Original frame of the same run, the edge-masked `judge_enhanced`): PASS at 2400, 3600, 4200 and 4800, worst tile 0. Mean FLIP away from edges 0.0014/0.0041/0.0042/0.0041, everywhere 0.0022/0.0103/0.0103/0.0103. The default run's (ticket 02) is 0.0022 to 0.0067 everywhere, so the extra difference is on edges. Crops at 3600 (`crop-net-3600.png`, `crop-player-3600.png`, top Classic 2x, bottom Native 4x): the net's top edge and the court lines are smoother, and no area changes. The report lists `ts4_msaa` on in `dual-1` and off in `native-1`.
- Box 3: `academy-lesson.toml`, the same flags, out `analysis/scenes/iq07/academy-lesson-msaa4/`. The same 4x/1x split of targets. PASS at all five frames, worst tile 0, mean FLIP away from edges 0.0017 to 0.0139, below the default run's 0.0047 to 0.0199. Trees at 2200 (the Academy grounds behind the lesson menu; crop x 860-1100, y 20-200):
  - `crop-trees-2200.png`: Classic 2x on the left, Native 4x on the right.
  - `crop-trees-2200-native-2x-vs-4x.png`: Native in ticket 02's default run against this run.
  - The foliage keeps its density, with a finer dither pattern, and has no holes or solid blocks. The crop's mean level moves by about the same between the two runs on both renderers (Native 123.9 -> 122.2, Classic 112.2 -> 110.4), so the foliage's coverage at 4x matches 2x's; the rest is the wind animation. Native's pre-ramp dumps are brighter than Classic's in both runs: that is the presenter's ramp, not MSAA. The 4x alpha-to-mask thresholds in patch 0006 (0.75/0.25/0.5/1.0 less offset/16) are Xenia's own (`spirv_translator_rb.cpp`).
- Live change, mid-match: `analysis/scenes/iq07/exhibition-match-msaa-live.toml` (exhibition-match.toml plus `at 3960` `set ts4_msaa 4` and `at 4560` `set ts4_msaa original`), `--runs 0 --native 1 --dual 1 --enhance ts4_msaa=original` (so the edge-masked judge is used), out `analysis/scenes/iq07/exhibition-match-msaa-live/`. `dual-1/runtime.log`:
  - 1299 `script input: frame 3960: set ts4_msaa 4`, then 1302-1305: the match pass's colour and depth targets made again at `4x` (same keys)
  - 1387 `frame 4560: set ts4_msaa original`, then 1391-1394: the same targets made again at `2x`
  - No errors, GPU failure or fallback (report `fallback: none`). Hitch lines: 548 in this run, 471 in the 4x-from-start run and 464 in ticket 02's default dual run (dual runs draw twice).
  - The dual judge passes at all four frames. Mean FLIP everywhere: 0.0067 at 3600 (2x), 0.0104 at 4200 (4x, as the 4x-from-start run), 0.0067 at 4800 (2x again, as the default). The mid-match rebuild is stable, so MSAA stays a live setting. Nothing logs the MSAA change itself apart from the target lines.
- Classic: `pwsh scripts/smoke.ps1 -Seconds 20 -ExtraArguments '--ts4_renderer=classic','--ts4_msaa=4'` (`analysis/scenes/iq07/classic-msaa4-smoke/`, alive at the deadline, no fatal lines). `runtime.log` line 1: `[warning] image quality: MSAA 4 is unavailable on the Classic renderer (Native only); drawing the game's own`.
- Not done: no comparison with Edge at 4x (whether Edge can force a sample count was not checked; the ticket compares with the Original frame of the same run). A live change is checked only in the Exhibition match. No frame-time cost was measured.
