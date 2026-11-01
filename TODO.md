# TODO

Open work, one line each. The old `.scratch/` tickets these came from are in git history:
`git show 5fbcf6f:.scratch/<feature>/issues/<file>.md`. Some statuses there were stale, so check
before starting.

## Next

- **Code-sign the release.** Defender's ML verdict that deleted XenosRecomp came from the game exe
  having no version information (docs/research/defender-false-positive.md, fixed); a later build
  can still be flagged. Signing `topspin4.exe`, the Launcher and `XenosRecomp.exe` is the lasting fix.
- **Memexport on Native.** The 64 Module vertex shaders XenosRecomp patches 0010-0012 let into
  the pack are memexport shaders (GPU compute writing to memory, explicit fetches from fetch
  constant 95 with no vertex elements). Native converts them but drops the exports and reads 0
  for those fetches, so a draw with one does nothing useful. None was met in an exhibition match.
- **60 fps cutscenes need interpolation.** The cutaways advance per rendered frame across ~20
  systems, so presenting them at 60 runs them ~1.75x fast (docs/research/cutaway-rate.md);
  `ts4_cutaways_60` is a developer switch with the handles.
- The Launcher has no Repair button; `SetupFlow.Repair()` exists in the model only.

## 1.0 (from pc-port-1-0)

- Title screen on Classic with no server wait (08, needed info)
- Settings system and renderer switch (10)
- Full Exhibition match vs CPU (13)
- Saves (14)
- Online menus fail cleanly (15); the message in the game's own popup (31)
- Locked 60 with even frame pacing (16)
- Local 2P and doubles (17)
- Academy (18)
- Venue and roster sweep (19)
- Cutscenes and cross-fades (20)
- Career start (21) and complete (22)
- King of the Court (23)
- Remapping and keyboard/mouse (24)
- Agassi outfit unlock, 3D forced off (25)
- Native parity everywhere, made default (29)
- Release candidate and Steam Deck (30)
- Boot without an audio device (32)

## Owner checks waiting

- Game settings 01-09, 11-15: owner run of each
- Launcher UX 01-03, 07, 08 and Launcher look 04: owner run of the new Launcher
- Launcher redesign (rail, hero, card row, Achievements page; launcher-ux.md §6): owner review of the look, with keyboard and pad
- 4x Resolution scale without stutter (image-quality 14, needed info)
