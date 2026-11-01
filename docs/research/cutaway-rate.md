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

A finer try set each scheduler entry's repeat count to 0 around the pass on odd frames, except
the render task's. The entries are 0x40 bytes in two lists per scheduler (`*(sched+0x24)`: the
first at `sched+*sched` with `sched[1]` entries, the second through `sched+0xc`): service
pointer at +8, repeat count at +0x30, frame divisor at +0x34. Schedulers nest (an entry's
service is often another `886117e8` scheduler), and most service pointers read 0 when first
seen. The intro still took 31.3 s and the game kept presenting 60 a second, so the zeroing did
not take hold the way the decompile suggests; this was not pursued further.

## First conclusion (superseded)

The attempts above read the cutaways as authored at 30 Hz frames, so a correct-speed 60 seemed
to need interpolation between 30 Hz states. The next section shows the opposite: the cutaway
logic already runs at 60 Hz, and mode 1 shows every other step.

## Research tools for a real 60

To find every clock a cutaway advances per frame, and whether each can take a half step:

- **Guest memory snapshots:** `--ts4_memdump_frames=<frames> --ts4_dump_dir=<dir>` writes the
  committed pages of `--ts4_memdump_range` (default `0x40000000-0xc0000000`, ~770 MiB) as
  `mem-NNNNNN.bin`. The guest waits while a dump is written (the swap count is the same before
  and after), so each is a clean snapshot. `scripts/memdiff.py` takes three or more dumps and
  lists the words that change by the same step per swap, as big-endian floats and integers.
- **Watchpoints:** `scripts/lldb.cmd` (pinned LLVM 21 lldb). A guest address is host
  `virtual_membase + address`; a hardware watchpoint on it stops in the recompiled `sub_XXXXXXXX`
  that writes it.
- **Profiler:** RelWithDebInfo builds have ReXGlue's Tracy zones; `.tools/tracy-0.13.1`.
- **Lua:** `pwsh scripts/game_lua.ps1 [-Archives Core,Melbourne]` decompiles the game's Lua into
  `analysis/lua-src` (`scripts/arcdump` extracts, `scripts/lua_wide_strings.py` rewrites the
  game's wide-string constant type 9, unluac decompiles with `scripts/ts4-lua-opmap.txt`). The
  game's Lua is 5.0.2 with four opcodes added after DIV (opcodes from POW up are +4); all 421
  scripts of four archives decompile. Cutscene animation is Havok (`CutScenes.arc`: 2093 `.hkx`
  packfiles, stored, magic `57e0e057`).

First snapshot run, the match intro at 30 fps (swaps 3602, 3612, 3622): no float advances by
1/30 or 1/60 per frame. Integers do: +1 per frame at Swing globals `88d45cd4`, `88d45e70`,
`88d4633c` (mirrored at `98d4...`) and heap `7018fc94`; -1 per frame at `a77027f4`, `a78b27f4`,
`a8cbca48`, `a8cbd12c`, `a8d0dae8` (countdowns, likely shot timers in frames); +2 per frame at
`a7828cb0` and `a8d1fcb0`. These are the next watchpoint targets.

## The cutaway logic runs at 60 Hz (the gate for a real 60)

Phase 1 of a real 60: how the cutaways' motion is computed. Four snapshots one swap apart in the
intro, lldb watchpoints from a marker frame, and a counting hook on the scheduler.

**No frame-to-time conversion.** No float in guest memory (low 4 KB heap and XEX range included)
steps 1/30 per swap, evenly or unevenly. The ~170 floats that step 0.032 per swap are two
0.016 steps, written by `884841E8 <- 8846B180` (a "TtWatchDog"-marked stream timer). The
`-1/frame` countdowns are render-side (`b22d59bc`: `887213E0` from the render pre-step
`8876D868`, firing a resource callback at 0; `b8f5xxxx`: the `8875FEB8` render path), and
`7018fc94` is the main loop's counter (`889BE8C8`).

**Havok animation steps a fixed float.** `884085a0` is `hkaDefaultAnimationControl::update(dt)`
(local time `+8 += speed(+0x40) * dt`), `884055a8` is `hkaAnimatedSkeleton::stepDeltaTime`.
The game's animation manager tick `887f59d0` steps each skeleton once every 3 ticks by
0.048 s (`N * 0.016f`, `0x880d1380`, N = 3 from `887f2058`), 0.016 s a tick on average. It
does not use the world dt. A counting hook: in gameplay (mode 0) the tick runs once per swap,
in a cutaway (mode 1) twice per swap, at 30 and at 60 alike.

**Mode 1 runs the cutaway logic twice per swap.** The scheduler instance that holds the cutaway
services (the animation manager `887f59d0`, the cutscene manager `887e9ea8`, ~15 more) is
reached through a service list (`88611d90`) entered from two parent schedulers in mode 1, and
one scheduler (entry id `d40a9431`) likewise: each runs twice per swap, with entry repeat
counts all 1. In mode 0 the same list has one parent. So the game steps cutaway logic at 60 Hz
and mode 1 presents every other step; at interval 1 that logic runs at 120 Hz, which is the 2x.

**Per-swap leftovers.** Two things advance once per swap rather than per logic step: the world
step (`881e5de8`, dt 1/30, already swapped for 1/60 at 60) and the face/morph player
`8817c608`, which adds 1 to an integer frame index per call (slots at `88d45cd4` + n*0x19c,
count at `88d45cc4`) and samples keyed bytes at it (`888c1180`, linear between keys).

**Prototype: run each scheduler or list at most once per game frame in mode 1 at 60.** The
frame is the game's own counter at `*(88d45b1c)+0x270` (the one the entry divisors use); it
steps once per swap while presenting and keeps stepping through the match load, when swaps
stop (keying on the swap count froze the game exactly where the intro ends). The rule skips
the second run of four instances in the intro (the shared list, `d40a9431`'s scheduler and two
lists under it). Result, quiet intro with `ts4_cutaways_60`: **61.53 s** (original 62.16 s,
-1.0 %), 60 presents a second, no stall, the match continues at 60.

**Gate: feasible.** Every per-frame advance is either logic that already steps at 60 Hz
(halved by running it once per frame) or one of the two per-swap leftovers, which take a half
step (the world dt) or tick on every other frame (the morph index).

## 60 fps cutscenes: the setting and its checks

The prototype became the hooks in `src/cutaway_rate_hooks.cpp` (rules in
`src/cutaway_rate_rules.h`) and the Graphics setting "60 fps cutscenes" (`ts4_cutaways_60`,
off by default). The face player holds on odd game frames: before the call each playing
slot's clip frame is wound back by one, and a frame where a slot is at frame 0 or has a clip
waiting skips the call, so the player never mistakes the hold for the clip's end. Cutaway
starts and ends are logged at either rate with the swap count.

Checks, exhibition match, Classic, unattended:

| Check | Result |
|---|---|
| Quiet intro (input cut after frame 3150) | 61.53 s, original 62.16 s (-1.0 %) |
| Cutscene manager (`887e9ea8`) steps per second, every cutaway of a match | 58-60/s on, 58-60/s off (1 step per swap on, 2 off) |
| A changeover (game-1 score at the chair) | 6.9 s at 59.7 steps/s |
| Presents inside cutaways (`--present_pacing_log`) | 2346 of 2360 on the 16.7 ms step; the rest are the intro's load hitches (50-167 ms), which the run with the setting off shows too |
| Exhibition match with the setting on | no stall, no crash, the match runs on at 60 |
| Exhibition match with the setting off (new hooks built in) | no stall, the same cutaway sequence as before; the only change is the cutaway start and end lines in the log |
| A replay | not exercised: no run showed one (Y at the score banner's `R` prompt did not start one) |

A match's cutaways cannot be paired one by one across runs: gameplay differs from run to run
even at 30, and the scene's presses are counted in swaps, which a cutaway at 60 uses twice as
fast. The steps-per-second count is the like-for-like measure: a cutaway lasts as long as its
timeline, and the timeline steps at the same rate with the setting on and off. Each run with
the setting on held back the same four instances (the shared list `b1e80600`, the scheduler
`b1e7fd80` and two lists under it); no other cutaway brought new ones.

Not yet seen on screen: a replay at 60, and the face player's skipped call (once per clip
loop, a frame where the call does not run; if that call also submits the face's morph each
frame, that one frame could show the face unmorphed).

## Hair: skip the extra step, not the second run

The owner saw the hair stretch off the head (the headband not matching it) in cutaways at 60.
The once-per-frame rule kept each instance's first run in a frame. In mode 1 the main
scheduler (entry ids, in order: ..., `a6d281f9` the extra step, `75e2d0fd` the bone palette
composer `888008c0`, ..., `19aebef6` the normal pass, ...) runs the cutaway logic first in the
extra step, then composes the palette, then runs the logic again in the normal pass, which
starts with services the extra step lacks (`4638b539`, `416ec92d`). Keeping the first run moved
the logic ahead of the palette and those services, so the hair was drawn a step off the pose.

The hook now skips the extra step's scheduler (`886117e8`) whole: its entries (`7e3baa14`,
`4583a710`, `d40a9431`, `bbf63e79`, `6d4f45ae`) all run elsewhere in the frame, so each
service runs once, in the normal pass's order. It is found by those ids, which are name hashes
in the scheduler data. Quiet intro: 61.30 s (-1.4 %); three consecutive frames of the tunnel
close-up show the headband and hair on the head.
