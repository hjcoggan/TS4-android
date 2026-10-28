# 10: Before-and-after screenshots

**What to build:** A set of matched screenshots that shows the owner what the image Enhancements buy: the original look (every setting at its default) beside the maxed-out look (Auto or 4x scale, 8x MSAA, 16x anisotropic filtering, High CAS), at the same frame of each scene. Each pair comes from the same scripted scene run, so camera, players and timing match exactly. Close-up crops make the difference obvious: court lines and the far baseline, stand banners, player and racket edges, the net, the crowd, shadows, Academy foliage and the HUD text. A local comparison page shows each pair side by side, with a slider or toggle between them. Screenshots are derived from game data, so they stay local and are never committed (ADR-0006). Spec: `../spec.md`.

**Blocked by:** 09 (Maxed-out pass and docs)

**Status:** ready-for-agent

- [ ] Every 1.0 scene (title and menus, Exhibition, Career, Academy, King of the Court) has at least one matched pair, captured at the same guest frame
- [ ] Each pair is shown at the output resolution (e.g. 1440p or 4K): the original upscaled by the presenter as a player would see it, the maxed-out frame rendered at that size
- [ ] Each pair has crops of court lines and banners, player and racket edges, the net, shadows, and foliage or crowd where the scene has them
- [ ] One Oracle frame from the same scene point is included as the reference for the original look
- [ ] A local comparison page (side by side, with a slider or toggle) opens from the run folder, and its path is noted in this ticket's comments
- [ ] No screenshot or page with game imagery is committed

## Comments

- 2026-10-28 (Claude), run prepared, not yet run. One command, from the repo root:

  ```
  python scripts/scene_runner_shots.py
  ```

  Options: `--size 2560x1440` (16:9 output, default), `--scenes boot-main-menu,exhibition-match,...`, `--out analysis/before-after`, `--oracle SCENE:FRAME=PNG[@X,Y,W,H]`, `--shader-pack FILE`. Default scenes: boot-main-menu, exhibition-match, career-start, academy-lesson, king-of-the-court, practice-ground (the Oracle frame: Edge's `shot-040s.png` from ticket 02, cropped to the game view, matched to frame 1300; skipped if the file is gone). It takes the game-run lock, runs headless and hidden, and needs an audio device (pc-port 32).
  - Per scene two runs from boot. `dual`: both renderers draw every frame; Classic presents, and the presenter's dump at the output size (`--ts4_dump_presented`, `src/frame_dumps.h`) is the original as a player sees it; Native at Resolution scale 4x and 8x MSAA writes its own image of the same frame (put through the presenter's ramp). Same run, same frame, so a match or King of the Court pair is exactly matched. `max`: Native alone with all four Enhancements at their maximum (also 16x AF, High CAS); the presenter's dump is the final look, CAS included. It is a separate run, so in a live scene the moment can differ a little from the dual run's. AF and CAS apply to both renderers and CAS to whatever the presenter presents, so only the `max` run shows them; the `dual` original stays pure.
  - Output: the page at `analysis/before-after/page/index.html` (slider per variant per frame, the crops from each scene's `[[capture]]` `crops`, the Oracle frame where given); runs, dumps and the page spec in `analysis/before-after/runs/<scene>/` and `page-spec.json`; `result.json` lists failures. Exit 0 clean, 1 a failure or a stand-in image (the page is still built), 2 could not start.
  - Needs from the rexglue owner before it is clean: `rex::ui::Presenter::CapturePresentedOutput(RawImage&, uint32_t width, uint32_t height)` (see `src/frame_dumps.h`). Until it exists the dumps have no presented image, the command says so, and the page falls back to PIL's upscale of the guest output (flagged on the page and in the exit code).
  - Not yet seen: the hidden window coming up at `--window_width/--window_height` with `--fullscreen=false` (the command fails the `max` run if the guest output is not the output size).
- 2026-10-28 (Claude): ran `python scripts/scene_runner_shots.py` once (07:20-07:39): all six scenes, dual and max, every capture taken, exit 0. Presenter capture (rexglue 0041) worked in every run: `frame dumps: frame N presented (2560x1440, ...)`, no PIL fallback on the page. Page: `analysis/before-after/page/index.html`; `analysis/before-after/result.json`. Waiting for the owner's visual review before ticking boxes.
