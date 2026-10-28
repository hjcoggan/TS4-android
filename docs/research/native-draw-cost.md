# Native draw cost: why crowd shots went over the frame budget

Date: 2026-10-28. The owner felt microstutter in matches at Resolution scale 4x with 8x MSAA
(5120x2880) on an RX 7900 XT and a 360 Hz variable-refresh display.

## Measurements

The loop: the exhibition match scene (`tests/scenes/exhibition-match.toml` input) headless to
frame 3600 with `--ts4_native_gpu_timer_csv`, judged on the 150 heaviest match frames.

- Steady frames scale with pixels: 1.0 ms at 1x, 2.4 ms at 2x, 7.9 ms at 4x (8x MSAA).
- One camera of the match (crowd in view, ~3000 indexed draws a frame) cost ~9.5 ms in the main
  pass at **every** scale, 1x included: a fixed GPU cost of ~3 us per draw.
- MSAA hardly mattered (main pass 7.1 / 8.1 / 8.1 ms at 1x / 4x / 8x samples), nor did
  anisotropic filtering, nor pipeline libraries against monolithic pipelines (run-to-run noise).
- RenderDoc (`scripts/rdoc.ps1 query <capture> 'calls EID'`): every draw was preceded by
  vkCmdSetViewport, SetScissor, SetBlendConstants and six stencil reference / mask calls, all
  with the values the previous draw had set. The draws themselves were tiny (55 triangles).

## Cause and fix

Setting core dynamic state before every draw, unchanged or not, made each small draw cost ~3 us
of GPU time on AMD whatever its size. `src/native/core_dynamic_state.h` sets viewport, scissor,
blend constants and stencil values only when they change on the command buffer (every Native
pipeline leaves them dynamic, so a pipeline bind never invalidates them; a new command buffer
does).

| | main pass, heaviest frames | heaviest 150 frames |
|---|---|---|
| 1x, before / after | 9.51 / 1.16 ms | 10.93 / 2.04 ms |
| 4x + 8x MSAA, before / after | ~10.5 / ~5.0 ms | 15.5-17.4 / 10.7-13.4 ms |

Match frames over 16.7 ms at 4x + 8x MSAA: over 200 before, 3 of 899 after. The scene runner's
dual comparisons (practice ground, exhibition match) still match Classic with 0 hot pixels.

## Not fixed here

On a variable-refresh display the frame is shown when its GPU work completes, and the presenter
presents at guest vblank + 5 ms (`present_pacing_offset_us`), usually before that. Display times
then follow GPU completion and vary by several milliseconds: 7.8-14 % of display changes were
off the 60 Hz step in visible PresentMon runs before and after this fix. Next step: present each
frame once its GPU work is done, at a fixed time after the guest vblank.
