# Image quality Enhancements

Status: ready-for-agent

Research: `docs/research/image-quality.md`. Settled in a grilling session on 2026-10-25.

## Problem Statement

Top Spin 4 renders at 1280x720 with 2x MSAA on its match pass, as the Xbox 360 did. On a 1440p or 4K monitor that frame is stretched and soft. Court lines and banners blur at grazing angles, and the edges of players, rackets and the net shimmer. The Native renderer can already render at a higher Resolution scale, sharpen with CAS and override texture filtering, but a player can't reach any of it: none of these are player settings. Two of them are also wrong once the scale goes above 1x: blur and shadow filtering sample too tightly, and textures are sharpened on top of the supersampling, which only adds shimmer.

There is also a quiet difference from the original. Both renderers force 4x anisotropic filtering today, while the Oracle (Xenia Edge) uses the game's own filtering. So the default look is not the original look, even though every Enhancement is supposed to default to original behaviour.

A player who wants the best-looking Top Spin 4 their PC can produce has no way to tune it and max it out.

## Solution

Four Enhancements, each a player setting that defaults to the original behaviour:

- **Resolution scale** (Native only): Original (1x), Auto, 2x, 3x or 4x. Auto picks the smallest multiple of 1280x720 that covers the 16:9 area of the window the frame is shown in, and follows the window while playing. A choice the GPU can't handle is clamped to the largest scale it supports, and the port says it was clamped. Higher scales render blur, bloom and shadows correctly and don't add extra texture sharpening on top of the supersampling.
- **MSAA** (Native only): Original (the game's own 2x), 4x or 8x. It raises the sample count only on the passes where the game already uses MSAA.
- **Anisotropic filtering** (both renderers): Original (the game's own filtering), 4x, 8x or 16x.
- **Sharpening (CAS)** (both renderers): Off, Low, Medium or High.

Players set them in the settings file or on the command line. They will appear in the settings page and launcher screen once those are copied in (ticket 10). Resolution scale and MSAA change while the game runs; if that proves unstable, the setting takes effect at the next start instead and says so. Resolution scale and MSAA are shown as unavailable on the Classic renderer.

With every setting at its default, both renderers look like the Oracle again, including texture filtering.

## User Stories

1. As a player, I want the game to look like the original by default, so that every image Enhancement is my own choice.
2. As a player, I want default texture filtering to match the original game, so that the default look matches the Oracle, not a 4x override nobody chose.
3. As a player on a 1440p or 4K monitor, I want to raise the Resolution scale, so that the game is drawn sharply at my monitor's resolution, not stretched from 720p.
4. As a player, I want an Auto Resolution scale, so that I get a frame at least as large as my window without working out the multiple myself.
5. As a player who resizes the window or switches to fullscreen, I want Auto to follow the new window size, so that the image stays sharp without a restart.
6. As a player who moves the game to another monitor, I want Auto to pick the scale for that monitor's size, so that it fits the display I'm on.
7. As a player, I want to choose a fixed 2x, 3x or 4x scale, so that I can trade performance for sharpness myself.
8. As a player with a GPU that can't render 4x, I want the scale clamped to what my GPU supports and to be told it was clamped, so that the game doesn't crash or lose the device.
9. As a player, I want the frame filtered down to my window when the scale renders above it, so that I get supersampled anti-aliasing, not a cropped or overflowing frame.
10. As a player at 2x or above, I want bloom, blur and shadow edges to look as soft as they do at 1x, so that a higher resolution doesn't make the effects look harsh or broken.
11. As a player at 2x or above, I want textures sharpened no more than the supersampling already does, so that the crowd, court and foliage don't shimmer more at higher scales.
12. As a player, I want shadows to get sharper as the Resolution scale goes up, so that they don't look soft next to a sharp scene.
13. As a player, I want the menus, HUD and loading screens to scale with the Resolution scale, so that text and UI are as sharp as the match.
14. As a player, I want to raise MSAA to 4x or 8x, so that the edges of players, rackets, the net and the court lines stop shimmering.
15. As a player, I want MSAA to apply only where the game already multisamples, so that raising it doesn't break effects the game never designed for MSAA.
16. As a player at 8x MSAA, I want foliage and other alpha-masked surfaces to look right, so that 8x doesn't break the trees in the Academy.
17. As a player, I want to raise anisotropic filtering to 16x, so that the court lines, baseline and stand banners stay sharp at the broadcast camera's grazing angle.
18. As a player, I want anisotropic filtering to work on both renderers, so that I can raise it even when I've fallen back to Classic.
19. As a player, I want to turn CAS sharpening on at Low, Medium or High, so that I can sharpen the image to taste.
20. As a player, I want CAS sharpening to work on both renderers, so that the choice doesn't depend on the renderer.
21. As a player, I want to change Resolution scale and MSAA while playing, so that I can tune the look without restarting.
22. As a player, if a setting can't safely change while playing, I want to be told it applies at the next start, so that I'm not left guessing whether it worked.
23. As a player on the Classic renderer, I want Resolution scale and MSAA shown as unavailable, so that I know why they do nothing there.
24. As a player, I want to max out every image setting at once, so that I get the best-looking Top Spin 4 my PC can produce.
25. As a player, I want the image settings in the settings file and on the command line, so that I can tune them before the settings page exists.
26. As a player, I want the image settings to appear in the settings page and launcher automatically once those exist, so that I don't have to edit files.
27. As a player, I want an invalid value in the settings file ignored and replaced with the default, so that a typo can't break the game.
28. As a Steam Deck player, I want the defaults to stay at the original 1x and 2x MSAA, so that the Deck keeps a steady 60 fps.
29. As a player, I want the session report to list which image Enhancements were on, so that a bug report shows the settings I played with.
30. As a developer, I want every image Enhancement listed in the session report's enhancements and in the scene runner's list, so that a parity run with one switched on fails, not silently passing as a match with the Oracle.
31. As a developer, I want each setting's decision (scale, samples, texture sharpness, filtering level, texture-offset scale) to come from one pure function, so that I can unit-test it without a GPU.
32. As a developer, I want the default look of both renderers compared with the Oracle once after the filtering default changes, so that the new default is proven to match.
33. As a developer, I want frames with a setting on compared against the 1x frame from the same run, so that I can verify Enhancements even if Edge crashes when scaled.
34. As a developer, I want to compare with Edge directly wherever Edge runs the same setting, so that I use the Oracle when it's available.
35. As a developer, I want a render target that breaks when scaled to be pinned to 1x, so that one broken effect doesn't block the whole Resolution scale.

## Implementation Decisions

- **Settings registry.** Four new player settings, all choice-type, with the original behaviour as the default: Resolution scale (`1`, `auto`, `2`, `3`, `4`), MSAA (`original`, `4`, `8`), anisotropic filtering (`original`, `4`, `8`, `16`) and sharpening (`off`, `low`, `medium`, `high`). They load from the settings file into the existing Native cvars (and, for filtering and sharpening, the Classic equivalents) like the existing settings. The command line and environment still win over the file.
- **One pure decision function (the image-quality plan).** It extends the existing output plan. Inputs: the four settings, the current output (window) size, the device limits (maximum image size, maximum sampler anisotropy, maximum LOD bias, supported sample counts) and the guest's requested sample count for a target. Outputs: the effective scale and whether it was clamped, the sample count for a target, the global texture sharpness bias, the anisotropy level and the texture-offset scale. All the rules below live there; the renderer only applies its outputs.
- **Auto scale.** The smallest whole multiple N for which 720·N is at least the height and 1280·N is at least the width of the output's 16:9 area (where the presenter shows the frame; owner decision 2026-10-27), capped at 4, then clamped to the device. It's recomputed whenever the output size changes.
- **Clamping.** A scale is clamped down until every render target the game creates fits the device's maximum image size (scale 4 needs 5120-wide targets; Vulkan only guarantees 4096). The clamp is logged and reported, never a failure.
- **Texture sharpness bias rule.** It replaces today's bias, which depended on the scale alone. The global bias is 0 whenever the Resolution scale is 1x, in any window (owner decision 2026-10-28: the default look is the original's, ADR-0001). Above 1x it is 0 when the render resolution is at or above the output, and log2(render height / output height) when the presenter upscales. The game's own per-fetch biases are still added, and the total is still clamped to the device's `maxSamplerLodBias`. So a default session in a 1080p or 4K window draws textures as the Oracle does, and sharpening applies only at 2x and above.
- **Texture-offset fix.** Texel offsets in converted shaders are measured in guest texels for resolved (scaled) textures, so blur, bloom and shadow filtering cover the same guest area at every scale. This needs a shader-converter patch plus a per-texture scale the shaders can read. It is part of Resolution scale and has no setting. Edge's `draw_resolution_scaled_texture_offsets` is the reference behaviour.
- **Scaled render targets.** Every render target scales with the Resolution scale, including the likely shadow map. A target shown to break when scaled is pinned to 1x by a per-target rule, as Edge does (`image_quality::kPinnedTargets`, empty today).
- **MSAA.** It raises the sample count only for targets the game creates multisampled (the match pass's 2x). Targets the game creates at 1x stay at 1x. The sample count is already part of the target and pipeline keys. 8x extends the game's alpha-to-mask shader patch to an 8-sample case. A count the device doesn't support falls back to the highest supported count at or below the request.
- **Anisotropic filtering.** "Original" means the game's own filtering on both renderers. That replaces today's forced 4x on Classic (the SDK default) and Native (which copies Classic). A forced level applies only to linear, mipmapped fetches, as today, clamped to the device maximum. A `--anisotropic_override` given directly wins over the setting on both renderers.
- **Sharpening.** Low, Medium and High map to fixed CAS strengths, about 0.25, 0.5 and 0.8, tuned by eye during implementation. It runs through the shared presenter, so it works on both renderers.
- **Live changes.** AF and CAS already change live. Resolution scale and MSAA rebuild render targets and pipelines between frames when they or the output size change. If a mid-match rebuild proves unstable, that setting becomes next-start only and the log says so.
- **Classic.** Resolution scale and MSAA are ignored on Classic and logged as unavailable there. The settings UI marks them unavailable once it exists.
- **Reporting.** Each of the four is listed in the Native session report's enhancements and in the scene runner's list of Enhancements, so a parity run with any of them on fails.
- **Docs.** The Enhancements line in `docs/goals.md` grows to resolution scaling, MSAA, anisotropic filtering and CAS sharpening. Ticket 28 points to this spec, and the spec's own tickets replace its checklist.

## Testing Decisions

- A good test checks behaviour the player or the renderer sees (the plan's outputs for given settings, window and device), not how the plan computes them.
- **Seam 1: the image-quality plan (unit tests, no GPU).** Extend the existing tests of the output plan, sampler choice and settings registry. Cases:
  - every default gives the original plan (1x, the game's samples, bias 0, the game's own filtering, CAS off);
  - Auto at 720p, 1080p, 1440p, 4K and ultrawide windows, and after a resize;
  - clamping on devices with 4096 and 8192 image limits;
  - the bias rule when supersampling, at equal size and when upscaling;
  - MSAA on 1x and 2x targets, and fallback when a count is unsupported;
  - the anisotropy level and its clamp;
  - the texture-offset scale for resolved and ordinary textures;
  - settings-file round trips and rejection of invalid values.
- **Seam 2: scene runs.**
  - All defaults: the existing dual-mode scenes (title, menus, Exhibition, Career, Academy, King of the Court) must still pass against the Oracle. The filtering default changes, so both renderers' default frames are compared with the Oracle once.
  - Settings on: dump frames, shrink them to 1280x720 and compare with the 1x frame of the same run at a looser threshold. A difference over an area (a missing glow, a shifted layer, a hard shadow) is a bug; a difference only along edges is expected.
  - Wherever Edge runs the same setting (AF via `anisotropic_override`, CAS via its post-process options, scale via `draw_resolution_scale` if it survives), also compare with Edge directly.
  - Check that the session report's enhancements list is correct.
- Per-pass checks at scale 1 and 2 (bloom and shadow pixels, resolve targets) use the RenderDoc tools in the Native debugging playbook.
- Prior art: the existing tests of the output plan, sampler choice and settings, the scene runner tests, and the dual-mode scene runs used for tickets 26 and 27.

## Out of Scope

- A separate shadow-resolution setting.
- SMAA or FXAA. Both need the point where the HUD starts drawing, which hasn't been found.
- FSR 1 upscaling.
- Temporal upscalers and TAA (DLSS, FSR 2/3, XeSS): there are no motion vectors.
- HDR output.
- Ultrawide and frame rates above 60.
- Texture replacement packs (they belong with mods, after 1.0).
- Presets. "Remastered" is the goal, not a setting; players tune each Enhancement.
- Resolution scale and MSAA on the Classic renderer.
- The settings page and launcher screen (ticket 10).
- Steam Deck performance tuning (ticket 30).

## Further Notes

- Most of the Native code exists already, copied from WWE All Stars: the scale of 1-4x, the filter down to the window, CAS through the presenter and the anisotropy override. This work exposes it, fixes the two problems at scale and adds MSAA levels.
- Open facts the tickets must settle:
  - whether Edge survives `draw_resolution_scale` 2 in this game (Canary crashes);
  - which render target is the shadow map (1040x1024 is the candidate);
  - whether any small target breaks when scaled and must be pinned to 1x;
  - the cost of the maximum settings on a desktop GPU.
- The new texture sharpness bias rule deliberately departs from WWE All Stars, which kept its bias unchanged (ADR-0004 allows divergence).
