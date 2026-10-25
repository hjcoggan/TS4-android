# 01: Oracle Baseline

**What to build:** Record how the Reference build behaves on the Oracle (Xenia Edge with TU1), so that later divergence can be classified as a port bug, an Oracle bug or an Original bug. This is milestone M0.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A full Exhibition match against the CPU completes on Edge with TU1 installed, and the Edge build and settings are recorded
- [ ] Native internal resolution and aspect ratio are measured
- [ ] Frame rate during play, between points and in menus is recorded, answering whether the 360 build drops to 30 fps between points
- [ ] Boot-to-title, title-to-menu and match load times are measured
- [ ] Visible glitches (cross-fade flicker, vertex explosions, camera problems) are listed with timestamps
- [ ] Reference captures of the title screen, the main menu and a mid-match frame are saved locally (not committed)
- [ ] Findings are written up under docs/research/

## Comments

- 2026-10-25 (Claude): The Oracle can now be run unattended. TU1 is installed in Edge (title bar `v0.0.1.3`; earlier Edge runs, including `.tools/xenia-edge/xenia.log`, were the base build `v0.0.0.3`). `scripts/edge.ps1` plays an input script on a ViGEm virtual pad, saves window screenshots at given seconds, and with `-Renderer vulkan -RenderDocAt <s>` takes a RenderDoc capture. `scripts/input/edge-boot-to-practice.txt` reaches the practice ground by about 23 s. No box is ticked: the full match, fps and load times still need doing.
