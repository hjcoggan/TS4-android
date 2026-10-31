# Movie cadence: why the 30 fps boot movies juddered

Date: 2026-10-28. The owner reported the boot logo movies "choppy, a different choppy than
30 fps". Diagnosed with a loop and fixed in `src/movie_clock.h` and `src/movie_clock_hook.cpp`.

## Symptom and loop

The 2K logo movie (`Media\XUI\2KSPORTS_MOVIE_1280x720.wmv`, WMV3, exactly 30 fps) plays while
the game swaps at a steady 60 Hz. Each picture should last two swaps. `--ts4_frame_hash_csv`
(a sampled hash of every shown frame) and `scripts/movie_cadence.py` measure how many swaps
each picture lasts. Before the fix 17-28 % of pictures (31-51 of 182 per run) lasted one or
three swaps (16 or 50 ms) in pairs (`31`, `13`). Presents were even: every guest frame was shown
once, 16.7 ms apart, so the judder is in which movie frame the game picks, not in presentation.

## How the player picks a frame (Loader_DLL, TU1)

- The XUI video element (`8321c1b8`) is driven by an XUI element timer (`8311dad8`, SetTimer,
  id `0xaabbccdd`). XUI checks timers once per UI pass against `KeTimeStampBundle` tick count.
  The player arms the timer with the milliseconds until its next frame is due.
- On the timer, `83231b38` reads the media clock (`83224e38`: `media += (now - last) * rate`,
  `now` from `83224fe8` = timebase in ms) and takes the next frame when `pts - media <= 38`.
- About every 166 ms `83225040` re-syncs the media clock to a value at `this+0x130`.

Movie frames are due every 33.3 ms, so each is due right at a swap boundary. One or two
milliseconds of jitter in when the timer fired, when the clock was read (after a WMV decode of
varying length) and in the re-sync moved frames to the swap before or after.

## What was ruled out, with evidence

- **Audio clock**: the port's audio callbacks arrive in 10 ms bursts (Windows' engine period),
  and pacing them evenly (5.3 ms) did not help (25-51 off). The player does not use the audio
  clock for picking frames.
- **Timer resolution**: `timeBeginPeriod(1)` changed nothing (27-43 off); `KeTimeStampBundle`
  is already updated every 1 ms.
- **Guest Sleep**: the player's `Sleep(delta - 5)` branch is never reached.
- **Presenter**: present intervals were 16 ms with no repeated or replaced frames.
- **Locking the clock to the swap count**: worse (69-83 off); the player thread reads close to
  the swap, so the count raced.

## Fix

- The media clock's `now` is rounded to a 60 Hz grid whose boundaries sit half a period from
  where the player's reads fall (learnt from the reads; they cluster within +-2 ms).
- The video element's timer fires on every UI pass, so the player decides each frame on that
  clock.
- A re-sync within 20 ms of the running media clock keeps the media clock (on another thread
  the grid could be a slot off); a seek or real drift still takes the target.

After the fix four of five runs had every picture last exactly two swaps. One run showed two
isolated pictures lasting one swap each (`AABCDD`), not yet explained.

## The replay wipe on Native (2026-10-31)

The owner saw the 2K replay wipe stutter on Native only (Classic and Edge smooth), with one
green frame as it starts. The wipe is a Bink movie (`2KLogoFootage.bik`) that the game engine
plays, not the XUI player above: two alternating buffers, each four single-channel 8-bit
textures (Y and alpha 512x288, U and V 256x144) the CPU rewrites every other frame.

Native read a texture whose content changed on its worker threads and drew the old content
until the read was applied, a frame or more later; a new texture drew the transparent-black
default. A probe in `textures::Bind` over a match showed, for the movie's planes, 732 draws
with content still being read and 8 with the default: pictures came late and held 1-4 frames,
the Y plane often a movie frame behind U, V and alpha, and the first frames converted
Y = U = V = 0 to green.

Fix (`src/native/textures.cpp`): a changed texture that was already uploaded is read before
the draw, as Classic does, and so is a new single-channel 8-bit texture. Over two wipes the
planes then drew current content every time (477 changes, none stale or default). Render-thread
time on textures was unchanged (0.11 vs 0.12 ms a frame).
