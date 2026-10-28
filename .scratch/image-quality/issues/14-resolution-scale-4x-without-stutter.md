# 14: Bring back 4x Resolution scale without stutter

**What to build:** Offer 4x Resolution scale again once a match at 4x with 8x MSAA plays without stutter on a variable-refresh display. On 2026-10-28 the owner capped the setting at 3x (game-settings branch), because at 4x the heaviest frames need ~15 ms of GPU time on an RX 7900 XT and are shown late. 3x with 8x MSAA measured stutter-free. Findings, numbers and everything already tried: `docs/research/native-draw-cost.md`. Spec: `../spec.md` (Resolution scale).

**Blocked by:** None. The owner chooses the route first; one of them adds input latency.

**Status:** needs-info

## Where it stands

- Measured with visible PresentMon runs of the exhibition match input (`tests/scenes/exhibition-match.toml`), independent flip only, judged from 60 s (match play): 4x + 8x MSAA showed 1-11 display changes off the 60 Hz step per ~90 s; 3x + 8x MSAA showed 0 in three runs, with and without the OBS and Overwolf layers.
- Every remaining off-step change at 4x follows a frame needing ~15 ms of GPU time. Top Spin 4 issues each frame's draws in one burst at its swap, and Native's work, the GPU plugin's copy into the presenter's guest output and the presenter's paint share one queue, so a frame must be done ~1 ms before the next vblank (the present hold's cap, patches 0042-0044) to be shown on time.

## Routes (owner to choose)

1. **Presenter rework.** Paint on a separate queue, or reorder submissions so the paint of what a swap shows never queues behind the next frame's work, with a pacing thread that does not block while it holds a present. Lets a frame take nearly a full period, with one frame (~17 ms) more input latency. Ship it as an opt-in setting so the owner can compare the feel. Three quick variants failed (see the research note: a later hold, showing the previous frame, gating Native's submit on the paint).
2. **Cheaper heavy frames.** At 4x the heaviest frames spend ~6 ms in the main pass, ~1.9 ms in the 8x colour resolve, ~1.8 ms in the shadow map's depth resolve (4160x4096, copied depth -> buffer -> R32 texture) and ~1.1 ms in the scene depth resolve. Copying depth once (a compute pass, or a depth-format resolve texture copied with vkCmdCopyImage) would save ~2-3 ms. Capping the shadow map at 2x saved ~2 ms but changed shadow rendering (3 of 4 match frames failed the `--enhance ts4_resolution_scale=4` dual compare), so that route needs the texel-offset or resolve-scale fault found first.

## Acceptance

- [ ] The owner picks a route
- [ ] Three visible match runs at 4x + 8x MSAA, judged from 60 s, show no display change off the 60 Hz step
- [ ] The exhibition and practice-ground dual compares, plain and with `--enhance ts4_resolution_scale=4`, still pass
- [ ] 4x is offered again (registry choices, Auto's limit, `kResolutionScaleChoices`), and a Settings file migration is not needed to bring it back

## Comments
