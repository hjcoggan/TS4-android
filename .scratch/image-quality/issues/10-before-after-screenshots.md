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
