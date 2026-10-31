# TODO

Open work, one line each. The old `.scratch/` tickets these came from are in git history:
`git show 5fbcf6f:.scratch/<feature>/issues/<file>.md`. Some statuses there were stale, so check
before starting.

## Next

- **Shader pack: XenosRecomp can't convert 64 Module vertex shaders.** Setup leaves them out of
  the pack (1035 of 1099), so a Native draw using one skips with `no_shader` (none was met in an
  exhibition match). Causes seen: registers past r31 (`printedRegisters[32]`, the `i < 32`
  register declaration in `shader_recompiler.cpp`) and `tfetchPosition(..., iPosition0, ...)`
  fetching an input the vertex declaration leaves undeclared. Raising the register count to 128
  alone cut failures to 55. Fix as patches in `patches/xenosrecomp/` (changes the converter id,
  so every pack rebuilds). Done when `topspin4-setup` on a fresh install reports none left out.
- No `pipeline-keys.txt` is shipped, so setup's prepare plans no pipelines (it still builds every
  shader module and library).
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
- 4x Resolution scale without stutter (image-quality 14, needed info)
