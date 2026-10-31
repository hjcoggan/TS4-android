# 03: Live top-ups: parallel, and folded into the pack

**What to build:** The safety net for shaders the pack still lacks, as `.scratch/shader-setup/spec.md` describes ("Parallel live conversion", "Top-up folding").

- **Game:** the live top-up converter runs several conversions at once, bounded by the thread budget, instead of one at a time. Keep the queue logic pure, next to the existing live top-up code.
- **Launcher:** after a session ends, it converts what the game left in the top-up folder and adds it to the pack, so a shader is converted live at most once per install (WWE All Stars' owner decision of 2026-10-04, kept). The pipeline record from ticket 02 then sees the changed pack and rebuilds before the next Play.
- `GLOSSARY.md` gets "Shader pack" and "Top-up" entries (spec, "Glossary").

**Blocked by:** 01

**Status:** resolved

- [x] Host tests: the live top-up queue hands out up to the budget's count at once and finishes every queued shader
- [x] Launcher.Tests: top-up folding with a fake converter (folded shaders join the pack, the folder is emptied, a failure keeps the folder)
- [x] A run with a pack missing about 17 shaders (remove them from a copy) converts them all within about a second of their first use, per the log's live conversion lines
- [ ] One smoke run passes and `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-31 (done in the main session, no QA pass; owner: no Oracle comparison for this spec).
- Game: `live_topup::Queue(max_converting)` hands out up to its budget at once; `topup_converter.cpp` starts `thread_budget::LiveConversions(cpus)` workers (2 on 8 or fewer CPUs, else the pipeline worker count, 2 to 4). Host tests: "the queue hands out up to its budget at once and finishes every queued shader" and the `LiveConversions` case (`analysis/shader-setup-check.log`, 415 test cases pass).
- Launcher: `ShaderPreparation.Run` now empties the top-up folder of every container the new pack holds (with any live `.shaderpack` beside it); `SetupFlow.AddTopUpsAsync` runs after a session when `AddsAfterSession` (the shaders band, then pipelines since the pack changed), called by the window after the game exits. Tests: `Folding_after_a_session_adds_the_top_ups_to_the_pack_and_empties_the_folder`, `A_failed_fold_keeps_the_top_ups_and_the_old_pack`, `After_a_session_the_shaders_the_game_met_join_the_pack_and_the_pipelines_follow`.
- Run: the e2e pack with every 30th entry removed (35 shaders, `analysis/shader-setup-e2e/topup03/dropped.txt`), Native scene `analysis/shader-setup-e2e/topup03/scene/native-1`: "live conversion with ..., 4 at once"; 12 of the 35 were met, each converted in 40-117 ms, four at a time on four threads (15:03:14.197-.253); `draw_skips: {}`.
- Glossary entries were added with ticket 01.
- Smoke run not made (owner: fewer runs); the scene run covers boot to a match.
