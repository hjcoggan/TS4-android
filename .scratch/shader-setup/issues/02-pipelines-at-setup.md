# 02: Pipelines prepared at setup

**What to build:** After the shader pack, setup runs the game's existing headless `--ts4_prepare_pipelines` mode so the pipeline store and the driver cache are built before the first Play, as `.scratch/shader-setup/spec.md` ("Prepare pipelines") describes.

- Copy WWE All Stars' `PipelinePreparation`, `PipelineOutput` and the process-runner interface (ADR-0004); read Top Spin 4's prepare output (`src/native/prepare_output.h`: header, `progress` lines, one `result` line; exit 0 ok, 3 partial, 2 failed, anything else a crash). Rename WWE's header word to the game's.
- A "Preparing pipelines" band in setup's overall progress after the shaders band; cancel and resume. A failure is shown and Play stays available (pipelines then build during play as today).
- After a finished prepare the Launcher records what it was built from (pack, top-ups, update folder). A change to any of them, a missing store, or the game's "pipelines stale" marker rebuilds before the next Play without a click. Repair always rebuilds. The space check counts the pipeline cache. Launcher.Cli runs the step with text progress.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Launcher.Tests: prepare output read from recorded lines (progress, ok, partial, failed, crash) and the rebuild decision (stale marker, missing store, changed pack, Repair)
- [ ] Launcher.Tests: setup flow with the pipelines band (resume at it, failure at it)
- [ ] End-to-end: fresh install folder → `topspin4-setup` → the store and driver cache exist under `cache/`, and a Native scene run boot to a match logs no pipeline compiled on the render thread for a planned pipeline
- [ ] One smoke run passes and `pwsh scripts/build.ps1 -Action Check` passes

## Comments
