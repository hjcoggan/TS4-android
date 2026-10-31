# 02: Pipelines prepared at setup

**What to build:** After the shader pack, setup runs the game's existing headless `--ts4_prepare_pipelines` mode so the pipeline store and the driver cache are built before the first Play, as `.scratch/shader-setup/spec.md` ("Prepare pipelines") describes.

- Copy WWE All Stars' `PipelinePreparation`, `PipelineOutput` and the process-runner interface (ADR-0004); read Top Spin 4's prepare output (`src/native/prepare_output.h`: header, `progress` lines, one `result` line; exit 0 ok, 3 partial, 2 failed, anything else a crash). Rename WWE's header word to the game's.
- A "Preparing pipelines" band in setup's overall progress after the shaders band; cancel and resume. A failure is shown and Play stays available (pipelines then build during play as today).
- After a finished prepare the Launcher records what it was built from (pack, top-ups, update folder). A change to any of them, a missing store, or the game's "pipelines stale" marker rebuilds before the next Play without a click. Repair always rebuilds. The space check counts the pipeline cache. Launcher.Cli runs the step with text progress.

**Blocked by:** 01

**Status:** resolved

- [x] Launcher.Tests: prepare output read from recorded lines (progress, ok, partial, failed, crash) and the rebuild decision (stale marker, missing store, changed pack, Repair)
- [x] Launcher.Tests: setup flow with the pipelines band (resume at it, failure at it)
- [x] End-to-end: fresh install folder → `topspin4-setup` → the store and driver cache exist under `cache/`, and a Native scene run boot to a match logs no pipeline compiled on the render thread for a planned pipeline
- [ ] One smoke run passes and `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-31 (done in the main session, no QA pass; owner: no Oracle comparison for this spec).
- Game: `--ts4_prepare_pipelines` is now wired (`src/topspin4_app.h`: OnPreSetup forces Native and a hidden window, OnPostSetup runs `PreparePipelinesAndExit` after `native::Configure`). Header word renamed to `topspin4-prepare-pipelines` (`src/native/prepare_output.cpp`, host test updated).
- Launcher: `PipelineOutput` and `PipelinePreparation` copied from WWE (ADR-0004); a pipelines band after the shaders band (`SetupStep.PreparingPipelines`, `OverallProgress`); `SetupFlow.WantsRebuild` (renamed from `WantsShaderRebuild`) also covers a pipeline rebuild; a failed step ends Ready with `PipelineFailure`; Repair forgets the record. `topspin4-setup` runs the step (exit 41 on failure). Space: `Preparation.SpareBytes` adds `PipelinePreparation.StoreBytes`.
- Tests: `PipelineOutputTests`, `PipelinePreparationTests`, and the pipelines-band tests in `SetupFlowShadersTests`; Check passes (`analysis/shader-setup-check.log`).
- End to end: on `analysis/shader-setup-e2e/install` with the store removed, `topspin4-setup` exit 0 in 32 s, "Pipelines ready." (`analysis/shader-setup-e2e/setup-02.out.txt`); `cache/native-pipelines` holds `keys.bin`, `driver-cache.bin` (87 MB), `launcher-inputs`. The game's line: `status=ok modules=1035 pipelines=0 failed=0 libraries=8035`. Native scene (`analysis/shader-setup-e2e/scene-02`): `draw_skips: {}`, render thread totals at frame 4802 "pipelines compiled 2, shader modules created 0, pipeline waits 0".
- Note: this repo ships no `pipeline-keys.txt`, so a fresh install plans no pipelines; the prepare builds the modules and every shader library, and the 2 render-thread compiles above were unplanned pipelines (fast-linked from those libraries). A shipped key list is separate work.
- Smoke run not made for this ticket (owner: fewer runs); the scene run above covers boot to a match.
