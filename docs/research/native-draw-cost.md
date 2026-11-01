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

## Later the same day: presentation on a VRR display

Measured with visible PresentMon runs of the exhibition match at 4x + 8x MSAA on a 360 Hz VRR
panel (independent flip only), judged on 60 Hz gameplay stretches (30 fps cutaways excluded).

| State | Gameplay display changes off the 60 Hz step |
|---|---|
| Start of the day | 11 % |
| Present hold (patches 0042, 0043) | 4.2-4.8 % |
| + depth staging reuse, fast clears (d370944, bbba5b9) | 3.8-4.5 % |
| Same build, 4x + 4x MSAA / 3x + 8x MSAA | 2.1 % / 0.3 % |

83 of the remaining 95 off-step changes follow a frame needing more than ~11.5 ms of GPU time.

What the game and the queue impose:

- Top Spin 4 issues each frame's draws in one burst at its swap (median span 0.6 ms), so a
  frame's GPU work cannot start earlier.
- Native's work, the GPU plugin's copy into the presenter's guest output and the presenter's
  paint share one queue. A paint submitted after the next frame's burst waits for that frame.

Tried and reverted, with the measurement that ruled each out:

- Submitting Native's work in chunks during the frame: no change (nothing to submit early).
- Presenting on the next vblank (offset 0): worse, the paint queued behind the next burst.
- A hold past the next vblank: the pacing thread holds, presents and paints in turn, so it
  delayed every later frame (44 % off step).
- A high-water hold capped before the vblank: no better than the percentile in gameplay.
- Held repeats of 30 fps frames: worse in cutaways (10.7-14.7 %); repeats stay off.
- Showing the previous frame (one frame of latency): no change while the next frame's work was
  submitted first. With that work also held until the presenter's paint (a submit thread and an
  external_frame paint signal): completion moved to ~5.4 ms after the vblank for 80 % of frames,
  but the tail and the hold rules interacted badly (5.3-21 % off step). Doing this properly needs
  the presenter's copy and paint on their own queue, or a pacing thread that does not hold.

## Final state: 3x cap, and overlays

The owner capped Resolution scale at 3x (8c43471; bringing 4x back is
`.scratch/image-quality/issues/14-resolution-scale-4x-without-stutter.md`). Match play at 3x + 8x
MSAA, judged from 60 s over ~90 s per run:

| Injected Vulkan layers | Display changes off the 60 Hz step |
|---|---|
| OBS, Overwolf, RTSS (the owner's desktop) | 0, 1 frame, 1 frame per run |
| RTSS only | 0, 0 + 1 frame at a scene change |
| none | 0 of 4,226 and 0 of 4,435 |

The single late frames came at scene changes (a replay handing back to play, a skip), ~2.7 ms late
although the game presented them on time and well within the hold; no GPU allocation was near them.
With no injected layers they did not occur, so they come from the overlay hooks (RTSS's present
hook among them), not the game. `VK_LOADER_LAYERS_DISABLE` and `DISABLE_VULKAN_OBS_CAPTURE=1`
remove the layers for one run.
