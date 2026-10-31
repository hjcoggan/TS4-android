# 03: Live top-ups: parallel, and folded into the pack

**What to build:** The safety net for shaders the pack still lacks, as `.scratch/shader-setup/spec.md` describes ("Parallel live conversion", "Top-up folding").

- **Game:** the live top-up converter runs several conversions at once, bounded by the thread budget, instead of one at a time. Keep the queue logic pure, next to the existing live top-up code.
- **Launcher:** after a session ends, it converts what the game left in the top-up folder and adds it to the pack, so a shader is converted live at most once per install (WWE All Stars' owner decision of 2026-10-04, kept). The pipeline record from ticket 02 then sees the changed pack and rebuilds before the next Play.
- `GLOSSARY.md` gets "Shader pack" and "Top-up" entries (spec, "Glossary").

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Host tests: the live top-up queue hands out up to the budget's count at once and finishes every queued shader
- [ ] Launcher.Tests: top-up folding with a fake converter (folded shaders join the pack, the folder is emptied, a failure keeps the folder)
- [ ] A run with a pack missing about 17 shaders (remove them from a copy) converts them all within about a second of their first use, per the log's live conversion lines
- [ ] One smoke run passes and `pwsh scripts/build.ps1 -Action Check` passes

## Comments
