# TODO

Open work, one line each. The old `.scratch/` tickets these came from are in git history:
`git show 5fbcf6f:.scratch/<feature>/issues/<file>.md`. Some statuses there were stale, so check
before starting.

## Next

- No `pipeline-keys.txt` is shipped, so setup's prepare plans no pipelines (it still builds every
  shader module and library).
- **Defender flags XenosRecomp.** Windows Defender's ML heuristic quarantines a freshly built
  `XenosRecomp.exe` (`Trojan:Win32/Bearfoos.B!ml`, 2026-10-31); then setup's shader step and live
  top-ups fail ("did not start", error 225). Dev machines exclude `out/build/xenosrecomp`. Before
  release, submit the shipped binary as a false positive (microsoft.com/wdsi/filesubmission) and
  consider code-signing the release.
- **Memexport on Native.** The 64 Module vertex shaders XenosRecomp patches 0010-0012 let into
  the pack are memexport shaders (GPU compute writing to memory, explicit fetches from fetch
  constant 95 with no vertex elements). Native converts them but drops the exports and reads 0
  for those fetches, so a draw with one does nothing useful. None was met in an exhibition match.
- The Launcher has no Repair button; `SetupFlow.Repair()` exists in the model only.
- No packaging script puts XenosRecomp in the release's `xenosrecomp/` folder yet.

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
