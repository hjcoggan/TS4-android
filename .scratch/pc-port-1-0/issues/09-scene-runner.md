# 09: Scene runner and boot-to-main-menu scene

**What to build:** Scenes (scripted input from boot plus guest frames to capture) play headless, dump frames, exit on their own, and write a session report.

**Blocked by:** 08 (Title screen on Classic, no server wait)

**Status:** ready-for-agent

- [ ] The WWE scene runner, input scripting and frame-dump / exit-after-frame support are copied and adapted
- [ ] A boot-to-main-menu scene reaches the main menu and captures it
- [ ] Running the scene twice gives the same captures
- [ ] Runs never overlap
- [ ] The main-menu capture matches the Oracle reference

## Comments

- 2026-10-24 (Claude): Pulled forward from this ticket at the owner's request, so automated runs skip the long intro: WWE's scripted pad (`src/script_input.{h,cpp}`, `--ts4_input_script`), timed by the guest's `VdSwap` count (patch 0029) instead of a per-game Present hook, since Top Spin 4 presents from two D3D copies. Added keystroke output: the intro videos and XUI screens read `XamInputGetKeystroke`, not the pad state, so WWE's pad alone could not skip them. `scripts/smoke.ps1` now uses `scripts/input/boot-to-title.txt` by default: the title screen in ~6 s (frame ~350) instead of 13-60 s, and a real pad can no longer steer a run. Still to do here: frame dumps, exit after the last capture, the session report, and the boot-to-main-menu scene.
- 2026-10-24 (Claude): `scripts/input/boot-to-main-menu.txt` drives the smoke run to the main menu (frame ~1100 START from the practice ground). Frame dumps, exit after the last capture and the session report are still to do.
