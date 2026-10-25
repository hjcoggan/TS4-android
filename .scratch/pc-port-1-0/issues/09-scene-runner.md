# 09: Scene runner and boot-to-main-menu scene

**What to build:** Scenes (scripted input from boot plus guest frames to capture) play headless, dump frames, exit on their own, and write a session report.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** ready-for-agent

- [x] The WWE scene runner, input scripting and frame-dump / exit-after-frame support are copied and adapted
- [x] A boot-to-main-menu scene reaches the main menu and captures it
- [x] Running the scene twice gives the same captures
- [x] Runs never overlap
- [ ] The main-menu capture matches the Oracle reference

## Comments

- 2026-10-24 (Claude): Pulled forward from this ticket at the owner's request, so automated runs skip the long intro: WWE's scripted pad (`src/script_input.{h,cpp}`, `--ts4_input_script`), timed by the guest's `VdSwap` count (patch 0029) instead of a per-game Present hook, since Top Spin 4 presents from two D3D copies. Added keystroke output: the intro videos and XUI screens read `XamInputGetKeystroke`, not the pad state, so WWE's pad alone could not skip them. `scripts/smoke.ps1` now uses `scripts/input/boot-to-title.txt` by default: the title screen in ~6 s (frame ~350) instead of 13-60 s, and a real pad can no longer steer a run. Still to do here: frame dumps, exit after the last capture, the session report, and the boot-to-main-menu scene.
- 2026-10-24 (Claude): `scripts/input/boot-to-main-menu.txt` drives the smoke run to the main menu (frame ~1100 START from the practice ground). Frame dumps, exit after the last capture and the session report are still to do.
- 2026-10-25 (Claude): Frame dumps, exit after the last capture and the session report are in (`src/frame_dumps.{h,cpp}`: `--ts4_dump_frames`, `--ts4_dump_dir`, `--ts4_exit_after_frame`, `--ts4_session_report`). WWE dumped Emulated frames from the front buffer through its Native renderer's texture reader, which this port does not have yet, so a dump here is the presenter's guest output, taken on the GPU thread as each swap is processed (`patches/rexglue/0031-frame-shown-observer`). That gives exactly the frame named, on the scripted pad's clock, whichever Module's D3D drew it. `scripts/scene_runner.py` plays a scene (`tests/scenes/*.toml`, WWE's format) on Classic `runs` times (default 2) and judges every later run's captures against the first with WWE's FLIP tile rule. Native and Oracle judging come later. `tests/scenes/boot-main-menu.toml` captures the title screen (frame 480) and the main menu (frame 1400): two runs, then three more, all pixel-identical (worst tile 0, mean FLIP 0.0000), each run about 23 s. Smoke and scene runs share `analysis/game-run.lock`, and a smoke run started during a scene run was refused. Lessons: loading screens show a random tip each boot, so a capture must never land on one, and the scene's input has to match `boot-to-main-menu.txt` exactly (leaving out the START at 350 left the last intro video playing at 450). Last box open: no Oracle reference capture of the main menu yet (ticket 01).
- 2026-10-25 (Claude), after review: a run now also fails on a black capture (brightest pixel below 16, as smoke grades screenshots), and when the dumps' frame clock strays from the pad's. Dumps count the swaps the GPU shows and the pad counts `VdSwap` calls, so the session report's `guest_swaps` must be within 1 to 5 of the exit frame; both runs read exit frame + 2. Known limits: the scene's input duplicates `scripts/input/boot-to-main-menu.txt` by hand; a stalled run leaves no session report (the run is killed at its deadline and fails as "no session report"); and each dump encodes a 2.7 MB PNG on the GPU thread, which delays the next frame by the same amount every run.
