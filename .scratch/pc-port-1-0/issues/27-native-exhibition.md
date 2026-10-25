# 27: Native Exhibition parity

**What to build:** The full Exhibition scene passes on Native, including readback-dependent faces and XPS geometry.

**Blocked by:** 26 (Native renderer boots to title), 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [ ] The Exhibition scene passes FLIP against the Classic envelope
- [ ] Opponent faces render correctly (resolve-to-texture readback)
- [ ] XPS CPU-generated geometry draws correctly
- [ ] No draw skips or fallbacks are reported

## Comments

- 2026-10-25 (Claude), ahead of ticket 13's scene: `tests/scenes/exhibition-match.toml` takes `scripts/input/boot-to-exhibition.txt` into a match and captures player select (frame 2400) and frames 3600, 4200 and 4800. Native fixes from it are in `docs/research/native-renderer.md`, "In the match": the match shaders added to the pack; DEC3N normals (`patches/xenosrecomp/0003`); 16-bit skin weights (`0004`); and constants set from buffers mirrored. Latest run (`python scripts/scene_runner.py tests/scenes/exhibition-match.toml --native 1`): player select Native against Classic worst tile 0, mean FLIP 0.0072. Frame 4800 passes (worst tile 50). Frames 3600 and 4200 fail at worst tiles 63 and 107, on the CPU player, the umpire and the score. In-match mean FLIP is 0.042, against 0.54 before the fixes. The two Classic runs fail against each other on every match frame (one was still in the walk-out cutscene), so match captures are not deterministic and the scene can't pass as it stands. Native's report: active, fallback `none`, no draw skips. No box ticked: the scene doesn't pass, and the full match comes with ticket 13. Faces at player select render (frame 2400 matches). XPS geometry is not identified yet.
- 2026-10-25 (Claude): Walk-out and match intro, frames 3000 to 3600 every 100 (`analysis/scenes/walkout`, scene from `exhibition-match.toml` with denser captures; not committed). Loading screens (3000, 3100): Native against Classic worst tile 0, mean FLIP 0.0017. At 3200 they fail at worst tile 68 (the loading tip text). Arena flyover with the intro card (3300, 3400): Native matches Classic run-2 by eye. Crowd, stands, court light and both faces are the same, and the remaining FLIP is a text fade a few frames apart. In the match (3500, 3600): same scene, with Federer a step off. The two Classic runs fail against each other on 3000, 3100 and 3300 to 3600, so these frames can't be judged pixel by pixel. The crowd draws on Native, which is likely the XPS CPU geometry, but that is not confirmed; the box stays open until the XPS draws are identified. No draw skips.
