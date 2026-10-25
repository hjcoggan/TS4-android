# 01: Oracle Baseline

**What to build:** Record how the Reference build behaves on the Oracle (Xenia Edge with TU1), so that later divergence can be classified as a port bug, an Oracle bug or an Original bug. This is milestone M0.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] A full Exhibition match against the CPU completes on Edge with TU1 installed, and the Edge build and settings are recorded
- [x] Native internal resolution and aspect ratio are measured
- [ ] Frame rate during play, between points and in menus is recorded, answering whether the 360 build drops to 30 fps between points
- [x] Boot-to-title, title-to-menu and match load times are measured
- [ ] Visible glitches (cross-fade flicker, vertex explosions, camera problems) are listed with timestamps
- [x] Reference captures of the title screen, the main menu and a mid-match frame are saved locally (not committed)
- [ ] Findings are written up under docs/research/

## Comments

- 2026-10-25 (Claude): The Oracle can now be run unattended. TU1 is installed in Edge (title bar `v0.0.1.3`; earlier Edge runs, including `.tools/xenia-edge/xenia.log`, were the base build `v0.0.0.3`). `scripts/edge.ps1` plays an input script on a ViGEm virtual pad, saves window screenshots at given seconds, and with `-Renderer vulkan -RenderDocAt <s>` takes a RenderDoc capture. `scripts/input/edge-boot-to-practice.txt` reaches the practice ground by about 23 s. No box is ticked: the full match, fps and load times still need doing.
- 2026-10-25 (Claude), ticket audit: ticked the resolution box. Edge on TU1, Vulkan, RenderDoc capture of the practice ground (`scripts/edge.ps1 -Renderer vulkan -RenderDocAt 40`, `analysis/edge-tooltest`): Swing renders at 1280x720 (16:9), viewport 1280x720 in two predicated tiles (scissor 1280x384 at row 0, then 1280x336 at row 384), into a 1280x1024 EDRAM-shaped 2x MSAA target; the port's front buffer is 1280x720 too. Partial for the rest: reference captures of the title screen and main menu are saved locally (`analysis/edge-menu/`; no mid-match frame yet), and rough timings under scripted START presses are boot to title 8-9 s and title to practice ground about 15 s. The full match, frame rate, glitch list, match load time and the write-up are not done.
- 2026-10-25 (Claude): the owner played Exhibition on Edge with their own controller while `scripts/edge.ps1 -NoPad -ShotEvery 1` took a window screenshot every second (`analysis/edge-baseline-1/`, 685 of 691 s; 2-7 s missed while the window was minimized). Edge build `edge@4d0720457` (Oct 24 2026), title bar `v0.0.1.3`, D3D12 with RTV/DSV render targets, XAudio2, the shipped config: `draw_resolution_scale` 1, `framerate_limit` 0, `guest_display_refresh_cap` on, `async_shader_compilation` on. Exhibition, singles, Federer v Nadal (CPU, Normal), Rod Laver Arena, settings 1 set, games per set TIE-BREAK, no super tie-break. Ticked three boxes:
  - Full match: the first tie-break went from the first serve at 75 s to Nadal winning it 7-0 at about 179 s, then the best-point replay (181-186 s), the celebration and the End of Match menu (Statistics / Rematch / Exit) at 189 s. A rematch was then played 197-688 s (Nadal 7-1) and left from Edge's menu before its End of Match screen.
  - Load times, at 1 s resolution from launch: title screen (Press START) up by 10 s; START at 11 s to the practice ground at 22 s (the "Discovering downloadable content" dialog at 20 s); main menu at 24 s; continue on game settings at 58 s to the match intro at 66 s; Rematch from End of Match (190 s) to play at 197 s.
  - Reference captures, kept locally: title `analysis/edge-baseline-1/ref-010.png`, mid-match `ref-300.png`, main menu `analysis/edge-menu/shot-043s.png`.
  Still open: frame rate (PresentMon records nothing unelevated; Edge's log frame counter reached 38798 at exit, about 56 a second over the whole session, but carries no wall-clock times), the glitch list (no glitch seen in a 6 s sampling and in the second-by-second frames around both intros and the match end; the intro name card fades in from an empty box, 66 s and 192 s, which is the animation, not a glitch), and the write-up.
