# Cutaway rate: smooth 30, and why 60 is not a toggle

Date: 2026-11-01. The owner saw the 30 fps cutaways (stadium and crowd shots, the match intro)
less smooth than in Xenia Edge, and asked whether they could run at 60. Everything here is
Swing_DLL (TU1), measured on Classic with the exhibition match input
(`tests/scenes/exhibition-match.toml`).

## How the game drops to 30

- `887330b8` picks a render mode from a scene hash and writes it to the render state's
  `+0x218` (render state = `*(*(88d45ad4)+0x20)`). Hashes `0x83199eb2` and `0xdd3795ad` write 0
  (60 fps); `0x04c901d9`, `0x32499dff` and `0xde9bf1c4` write 1 (30 fps). It keeps the hash at
  renderer `+0x1562c`.
- Every frame `88739a40` turns mode 0 into present interval 1 and mode 1 into 2, at D3D device
  (`*(88d45ad4)+0x15720`) `+0x35f0`. The swap waits for that many vblanks (`88295848`,
  `88295698`, `88295578`).
- It is not a frame cap: the QPC cap in `8875f498` is off (-1.0f).
- The world step `887c6b98` passes dt 0.03333f (`0x880d1088`) in mode 1 and 0.01666f
  (`0x880d108c`) otherwise, in f1, to `881e5de8`, which feeds it to the world's Lua.

## Smooth 30 (fixed, patch 0045)

Presents were already even: in a match, 139 of 139 presents of a cutaway were 2 vblanks apart,
none replaced, while the game's own swaps jittered 31-36 ms. The difference from Edge was on
the display. The port showed each 30 fps picture once on a variable-refresh panel, because the
repeat present on the vblank between went out at the plain present offset, before the present
hold's point (~8 ms) where new frames go, and the panel showed 10 ms / 28 ms pairs; repeats were
turned off for that. Patch 0045 holds repeats to the same point, and the app keeps the SDK's
default of repeating. PresentMon, exhibition match after boot, cutaways included: 3565 of 3565
presents within 1 ms of the 16.7 ms step.

## 60: the cutaways advance per rendered frame

The match intro with no input, from mode 1 to mode 0 (`ts4_cutaways_60` logs both ends):

| Run | Intro |
|---|---|
| Original, interval 2 | 62.16 s |
| Interval 1 | 31.35 s |
| Interval 1 + the world step's dt at 1/60 | 35.6 s |
| + cutscene manager tick (`887e9ea8`) every other frame | 40.6 s |

So nearly the whole timeline is counted in rendered frames. Skipping every other call, with the
dt fix, changed nothing (35.6-35.9 s) for: `887bab80`, `887c5898`, `88794620`, `887af080` (the
world tick's callees), `881e5de8` at 30 with its own 1/30 (the world's Lua), `887bb658` (the
world's `+0x320` watcher) and `887c64a0` (the timer task, which uses wall time). Skipping the
whole world tick (`887c6d20`) left the intro running past 97 s.

The scheduler (`886117e8`, called once per frame for each of ~20 service groups) runs every
service once per frame in both modes: one pass, every frame, nothing mode-dependent to halve.
`8865fac0`, a fixed-step catch-up loop, is not on the match path (a hook never fired).
`FUN_8872d0c8` and `FUN_88723ed0`, called by the mode switch, are layer and visibility toggles.

Half-rate simulation, rendered every frame (the first half of an interpolation design), was
tried by skipping, on every other frame of a cutaway, every scheduler instance except the one
that runs the main render task (`8876d978`). The intro still took 31.4 s: the cutaway timeline
runs in that same scheduler instance as the render. Halving it needs the instance's service
loop re-implemented with the render task kept every frame. Two traps on the way: skipping
`8876d868` (the render task's pre-step) stalled the game, and so did a frame parity that froze
during the match load, when schedulers run without the main render task.

## Conclusion

The cutaways are authored at 30 Hz frames throughout. Running every service every other frame
at interval 1 would give the right speed but the same picture twice, which is what smooth 30
already shows. A correct-speed 60 needs interpolation between the 30 Hz states (animation
poses, camera), a large piece of engine work; driver frame generation (AMD Fluid Motion Frames,
Lossless Scaling) is the player-side option. `ts4_cutaways_60` stays as a developer switch with
the interval and dt hooks (`src/cutaway_rate.h`), as handles for that work.
