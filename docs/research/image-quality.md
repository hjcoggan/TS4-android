# Image quality: how close can the port get to a "Top Spin 4 Remastered"?

Research date: 2026-10-25. The owner wants the best image quality the port can realistically
reach: internal resolution, anti-aliasing, texture filtering, and the other standard upgrades.
This note lists the options with their evidence and ranks them. It decides nothing. No code
was changed and the game was not run.

Heads read:

- this repo at `6046ee7`;
- Xenia Edge `edge` at [`82a9462`](https://github.com/has207/xenia-edge/tree/82a946231b2356901afe9e94c7ceacfffc936193)
  (2026-10-25; the Oracle binary is the older `4d07204`, oracle-baseline.md);
- UnleashedRecomp `main` at [`cf829a9`](https://github.com/hedge-dev/UnleashedRecomp/tree/cf829a9eca8fb680fba4b0409ddeb6ca92f22e3c);
- the vendored ReXGlue `.tools/rexglue-source` (`f5337cd` plus `patches/rexglue/`);
- XenosRecomp `.tools/xenosrecomp` (plus `patches/xenosrecomp/`);
- WWE All Stars at `D:\Projects\WWE_ALLSTARS-RECOMP`, whose renderer we copied (ADR-0004).

Edge paths below are relative to its repo (`src/xenia/gpu/...`), Unleashed paths to its
`UnleashedRecomp/` folder, and `SDK:` paths to `.tools/rexglue-source`. Upstream files were
fetched raw at those heads to get line numbers. **[inferred]** marks my own reasoning.
**[unverified]** marks a claim I could not check against a primary source or a run. Every
other claim has a URL or a `file:line` beside it.

## Summary

- **Most of the infrastructure is already in place, copied from WWE All Stars.** Native has
  an internal resolution scale of 1-4x, which scales every render target, viewport, scissor,
  clear and resolve. It filters a frame larger than the window down to the window, has CAS
  through the presenter, and has an anisotropy override
  (`src/native/native_renderer.cpp:128-145`, `src/native/guest_decode.h:574-628`). None of it
  is a player setting yet: `src/settings/registry.h` holds only the renderer and pad
  bindings, and ticket 28 is open.
- **Top Spin 4 differs from WWE in one way that matters: it already renders its match at 2x
  MSAA.** That is 1280x720 in two predicated tiles (oracle-baseline.md, "Resolution"), and
  Native already draws it as a real 2x target (`native_renderer.cpp:355-361, 1216-1240`).
  Raising that sample count is the same mechanism Unleashed Recompiled uses ("MSAA levels
  beyond the 2x used by the original game", [README:113](https://github.com/hedge-dev/UnleashedRecomp/blob/cf829a9eca8fb680fba4b0409ddeb6ca92f22e3c/README.md#L113)).
  It is a small change, because the sample count is already part of the target and
  pipeline keys.
- **Two correctness gaps should be fixed before resolution scale ships:**
  1. Texture-fetch offsets shrink at scale. XenosRecomp divides a fetch's texel offset by the
     host texture size (`.tools/xenosrecomp/XenosRecomp/shader_common.h:241`). A scaled
     resolve target (bloom, shadow PCF, blur) is therefore sampled N times too tightly. Edge
     corrects exactly this with `draw_resolution_scaled_texture_offsets`
     (`render_target_cache.cc:156-161`, `spirv_shader_translator_fetch.cc:1252-1290`).
  2. The LOD bias rule sharpens textures that supersampling already sharpens.
     `LodBiasFor` gives -0.5 at 2x and -1 at 3x+ (`guest_decode.h:595-597`), even when the
     frame is filtered down to a smaller window. The vendors' formulas give 0 or more there
     (§4).
- **Temporal upscalers and TAA (DLSS, FSR 2/3, XeSS) are not realistic.** Each needs
  per-pixel motion vectors and a jittered projection
  ([DLSS guide](https://github.com/NVIDIA/DLSS/blob/main/doc/DLSS_Programming_Guide_Release.pdf)
  §3.6-3.7; [FSR docs](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-upscaler/);
  [XeSS guide](https://github.com/intel/xess/blob/main/doc/xess_sr_developer_guide_english.md)).
  Every colour target the game creates in every scene is RGBA8 (run logs, §0), so there is
  no velocity buffer to feed them. The SDK's `fsr2`/`fsr3` presets synthesize those inputs
  and reset every frame (`SDK:src/ui/vulkan/vulkan_presenter.cpp:377-414`), so they are FSR 1
  with extra cost.
- **There is no HDR to unlock.** The scene is LDR RGBA8 end to end (§0, §12). HDR output
  would mean inventing highlights, which is out of character for an Enhancement that defaults
  to the original (ADR-0001).
- **One finding affects the default look today.** The Oracle filters textures at the game's
  own setting (`anisotropic_override = -1`, `.tools/xenia-edge/xenia-edge.config.toml:224`;
  Edge default `gpu_flags.cc:254`). Our Classic inherits ReXGlue's default of 4x
  (`SDK:src/graphics/pipeline/texture/cache.cpp:52`), and Native copies Classic
  (`ts4_native_anisotropy` -2). Both renderers are therefore already one Enhancement away from
  the Oracle at default settings (§3).

### Recommendations, ranked by visual payoff per unit of effort

| # | What | Renderer | Payoff | Effort | Where |
|---|---|---|---|---|---|
| 1 | **Ship resolution scale as a player setting.** Pick the scale automatically as ceil(display height / 720), clamped to device limits, so the frame is always rendered at or above the display and filtered down to it | Native | Very high on 1440p/4K | S (code exists) | `ts4_native_resolution_scale`, `PlanOutput`, `BlitToOutput`; `src/settings/registry.h`; ticket 28 |
| 2 | **Fix texel-offset scaling** in converted shaders: offsets in guest texels for resolved textures | Native | High (prerequisite for #1 at 2x and up) | S-M (XenosRecomp patch + a per-slot scale) | `shader_common.h:237-242`; push constants; `textures::GetResolveTarget` |
| 3 | **Fix the LOD bias rule:** 0 when render ≥ display, log2(render/display) when the presenter upscales | Native | Medium (less shimmer) | S | `LodBiasFor` (`guest_decode.h:595`) |
| 4 | **Anisotropic filtering at 16x** as a setting; decide the default (Oracle = game's own) | Both | Medium-high (court lines, stands at grazing angles) | S | `sampler_choice.h`; `anisotropic_override` on Classic |
| 5 | **MSAA 4x/8x where the game asks for 2x** (main match pass), with the game's alpha-to-mask extended to 8x | Native | High (players, net, rackets, court-line edges) | S-M | `GuestSamples`, `CurrentColorTarget`/`CurrentDepthTarget`, `patches/xenosrecomp/0006-alpha-to-mask` |
| 6 | **Per-target scale policy:** scene and its post chain at N; small off-screen targets (radar gun, bakes) at 1 or N by a threshold, as Edge does | Native | Medium (avoids broken effects) | S-M | `GetRenderTarget` (`native_renderer.cpp:1099`) |
| 7 | **CAS sharpening** exposed as a setting (exists); FSR 1 only when render < display | Both (presenter) | Low-medium | S | `ts4_native_cas`; `present_effect` |
| 8 | **Shadow-map resolution** as its own setting, once the shadow target is confirmed | Native | Medium-high in matches | M (RenderDoc + maybe RE) | `GetRenderTarget` scale per key |
| 9 | **SMAA (1x, or S2x on the game's 2x samples)** before the HUD | Native | Medium (for low-end; redundant with #1+#5 on desktop) | M-L (needs a pre-UI point) | new pass between the 3D composite and HUD draws |
| 10 | **Texture replacement by hash** (dump, upscale, load) | Native | High with community packs | M-L, post-1.0 (mods) | `textures.cpp:476-479` XXH3 keys, `ts4_native_dump_texture` |
| 11 | **Ultrawide** | Native + guest hooks | Medium for some players | L, post-1.0 | camera/projection RE in Swing, HUD layout |
| — | Temporal upscalers/TAA, HDR output, unlocked frame rate | — | — | not recommended for now | §7, §11, §12 |

A sensible first Enhancement set (ticket 28 plus):

- **Desktop "High":** auto scale, 4x MSAA on the match pass, 16x AF, CAS 0.25.
- **Steam Deck:** scale 1 (the Deck's 1280x800 panel shows the 1280x720 frame letterboxed
  1:1), the game's 2x MSAA, 16x AF.

The Deck numbers are **[unverified]**: no Deck measurement exists yet (ticket 30).

---

## 0. What Top Spin 4 actually renders

| Fact | Evidence |
|---|---|
| Swing renders at 1280x720 (16:9). The match pass is 2x MSAA in two predicated tiles: scissor 1280x384, then 1280x336 at row 384, into a 1280x1024 EDRAM-shaped surface | oracle-baseline.md "Resolution" (RenderDoc capture on Edge) |
| Why two tiles: EDRAM is 2048 tiles of 80x16 samples x 4 bytes = 10 MiB (Edge `xenos.h:481-485`). 1280x720 at 2x with 32-bit colour plus 32-bit depth needs 1280·720·2·8 B ≈ 14.1 MiB **[inferred arithmetic]** | — |
| Native has no EDRAM. It draws each tiled call once into a target sized to the viewport | `native_renderer.cpp:1223-1232`; native-renderer.md "Draw census" |
| Render targets the game creates (all colour RGBA8, `format 37`; all depth D32S8, `130`), unioned over the six scene runs: 1280x720 (1x and 2x), 1280x384 2x, 1040x1024, 640x360, 560x512, 560x128, 320x256, 320x64, 160x128 | `analysis/scenes/*/*/runtime.log`, lines "native renderer: colour/depth render target" (local, not committed) |
| The 1040x1024 target is the shadow map: one 512x1024 half per player, resolved to the 1024x1024 `ShadowDepthMapPlayer` | native-renderer.md "Shadow map and texel offsets at scale" (verified in ticket 05) |
| The 256x64 radar-gun display is rendered and resolved one mip level at a time | native-renderer.md "Same-frame differences" |
| The game uses Xenos alpha to mask (Academy trees) and per-fetch LOD bias (-1, -2 on foliage) | native-renderer.md "Other modes"; `patches/xenosrecomp/0006-alpha-to-mask.patch` |
| Drops to 30 fps in its cutaways; 60 in rallies and menus | oracle-baseline.md "Frame rate" |
| No CPU reader of resolved images found; readback off by default | `ts4_native_resolve_readback` (`native_renderer.cpp:149-152`) |

Consequences **[inferred]**:

- The game already anti-aliases its 3D at 2x, so the gain from more AA is real but smaller
  than in a 1x game such as WWE All Stars.
- With no float target, there is no HDR scene data and no velocity buffer.
- Resolve-to-texture is common (mip chains, post), so whatever scales render targets has to
  keep resolve targets, fetch offsets and mip levels consistent.

## 1. Internal resolution (supersampling to the display)

**What it is.** Render every target at N times the guest size so the game "thinks" it draws
720p. Then either show that frame at a display of the same size, or filter it down to a
smaller display (ordered-grid supersampling).

**How others did it.**

- **Xenia (opaque scaling).** `draw_resolution_scale_x/y`, 1-7, require sparse binding or
  tiled resources. The help text warns that "various effects and parts of game rendering
  pipelines may work incorrectly as pixels become ambiguous from the game's perspective and
  because half-pixel offset ... becomes full-pixel" (Edge `texture_cache.cc:30-42`).
- **Edge's mitigations:**
  - `draw_resolution_scaled_texture_offsets` (`render_target_cache.cc:156-161`; SPIR-V at
    `spirv_shader_translator_fetch.cc:1252-1290`) for fetch offsets into resolved textures;
  - `draw_resolution_scale_threshold`, which keeps targets with a pitch at or below N pixels
    at 1x because "small offscreen surfaces like bloom or depth of field buffers often break
    when upscaled" (`render_target_cache.cc:162-170, 692-708`);
  - `resolve_resolution_scale_fill_half_pixel_offset`, which stretches the first covered host
    pixel at resolve edges (`draw_util.cc:33-39`).
- **Top Spin 4 under Xenia:** upscaling crashes Canary, either as a host crash on the title
  screen or as "graphics device lost" ([game-compatibility#358](https://github.com/xenia-canary/game-compatibility/issues/358),
  via feasibility.md). That is why ADR-0005 puts scaling on Native.
- **Unleashed Recompiled (game-aware scaling).** It does not scale behind the game's back.
  It writes the window size times `ResolutionScale` into the game's device back-buffer size,
  so the game allocates larger targets itself (`gpu/video.cpp:5897-5904`). It then
  re-derives effects that depend on resolution, for example a DoF blur that grows from 3x3 to
  9x9 taps with output height (`gpu/video.cpp:5205-5228`;
  [README:114](https://github.com/hedge-dev/UnleashedRecomp/blob/cf829a9eca8fb680fba4b0409ddeb6ca92f22e3c/README.md#L114):
  DoF "commonly breaks in emulators when increasing the game's resolution").
- **WWE All Stars (our source):** the same Native scale, with a 2x2 supersample mode; the
  owner judged it "equal to Classic" (`WWE_ALLSTARS-RECOMP/.scratch/image-quality/map.md`).
  Its later remaster note found the default showed a soft 720p on 1440p and 4K monitors,
  because the supersampled frame was filtered to 720p and then upscaled. Its first
  recommendation was to filter to the display instead
  (`WWE_ALLSTARS-RECOMP/docs/research/graphics-remaster.md:26-35`). Our copy already does
  that: `PlanOutput` downsamples to the window's 16:9 area (`guest_decode.h:598-628`).

**Mapping onto our code.**

- The scale is read once at startup (`native_renderer.cpp:1737-1744`). It is applied in:
  - target creation (`GetRenderTarget`, `:1099-1104`);
  - viewport (`:1404-1418`) and scissor (`:1438-1442`);
  - clears (`:1995-2004`);
  - resolve targets (`textures::GetResolveTarget(..., source->scale)`, `:2141-2143`);
  - CPU readback downsampling (`:702-709`).
- Output: `PlanOutput` either publishes the scaled frame for the presenter to fit, or
  box-halves and then bilinear-filters it to the window (`BlitToOutput`, `:3169-3190`).
- **Missing:**
  1. A player setting, through `src/settings/registry.h` (today only `ts4_renderer` and pad
     keys).
  2. An "Auto" choice of ceil(display height / 720).
  3. A clamp to device limits. The Vulkan core minimum for `maxImageDimension2D` and
     `maxFramebufferWidth` is 4096 ([limits](https://docs.vulkan.org/spec/latest/chapters/limits.html)).
     Scale 4 makes 5120-wide targets, and 1040x1024 becomes 4160x4096. Desktop GPUs allow
     16384 **[unverified per device]**.
  4. Live changes: the scale is fixed for the session; `GetRenderTarget` already recreates a
     target whose scale changed.
- The downsample is a 2:1 box chain plus one bilinear step. For ratios that are not powers
  of two (scale 3 to 1440p is 1.5:1), a wider filter (Catmull-Rom/Lanczos in a compute pass)
  is sharper than one bilinear blit **[inferred]**.

**Risks and known artifacts.**

- Fetch offsets (§8) and the half-pixel offset in post passes (§9).
- Effects tuned in guest pixels (bloom, DoF, blur kernels) sample the same UV footprint at N
  times the density. They can look grainier or ringed, but they stay in place
  **[inferred]**.
- Every target is multiplied, so VRAM grows with N². The 2x match target at scale 3 is
  3840x2160x2 samples.
- The scene runner already fails a run with `ts4_native_resolution_scale != 1`
  (`native_renderer.cpp:1855-1861`), so scaling cannot leak into parity runs.

**Verify against the Oracle.**

1. Enhancement off: the existing dual scenes must stay green.
2. Enhancement on: dump scale-2 frames, box-downsample them to 1280x720, and compare them with
   the 1x dual frame of the same run at a looser FLIP threshold. A whole-layer shift or a
   missing bloom or DoF shows up as a structured hot region; edge noise does not.
3. Edge at `draw_resolution_scale_x/y = 2`, with `draw_resolution_scale_threshold` if needed,
   is a second opinion on how the effects look scaled. **[unverified]**: whether Edge
   `4d07204` survives scaling in Top Spin 4; Canary does not (#358).

## 2. MSAA beyond the game's 2x

**What it is.** Raise the sample count of the targets the game already multisamples. Only
geometry edges gain. Alpha-tested edges need alpha to coverage or sample shading.

**How others did it.**

- **Unleashed:**
  - Its `AntiAliasing` setting defaults to `MSAA4x` (`user/config_def.h:67`), offering what
    both colour and depth support (`gpu/video.cpp:1823-1836`).
  - Surfaces the game creates as multisampled get the configured count instead
    (`gpu/video.cpp:3193`).
  - `TransparencyAntiAliasing` turns alpha-tested draws into alpha to coverage when the target
    is multisampled (`gpu/video.cpp:1022-1040`, `config_def.h:68`).
  - It ships its own MSAA colour and depth resolve shaders for 2x, 4x and 8x
    (`gpu/shader/resolve_msaa_{color,depth}_{2,4,8}x.hlsl`).
  - The low-end preset drops to 2x and turns transparency AA off (`gpu/video.cpp:1652-1654`).
- **Xenia:** emulates the guest's 2x and 4x (`native_2x_msaa`, `SDK:src/graphics/flags.cpp:18`)
  and never raises them. `xenos_sample_positions` rasterizes at the Xenos positions where the
  host can (Edge `gpu_flags.cc:289-295`). The Oracle runs with it off
  (`xenia-edge.config.toml:378`).

**Mapping onto our code.**

- The guest count comes from `GuestSamples(RB_SURFACE_INFO)` (`native_renderer.cpp:355-361`)
  and is part of the target key (`SamplesKeyBits`, `:1206-1208`) and the pipeline key
  (`key.multisample`, `:2443`; `pipelines.cpp:356-358`).
- An override, "when the guest asks for 2x, use N", touches `CurrentColorTarget` and
  `CurrentDepthTarget` (`:1216-1245`). The colour and depth of one pass must agree.
- Resolves already go through `SingleSample`, a dynamic-rendering resolve that averages colour
  and takes depth from sample 0 (`:1247-1300`). Blits then read the single-sample twin, as
  Vulkan requires (VUID-vkCmdBlitImage-srcImage-00233,
  [vkCmdBlitImage](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdBlitImage.html)).
  No new resolve code is needed for colour.
- Alpha to mask: the converted shaders compute the Xenos per-sample thresholds for 1, 2 and 4
  samples (`patches/xenosrecomp/0006-alpha-to-mask.patch`, `alphaToMask`). 8x has no branch
  and would need one (for example two 4x patterns interleaved) **[inferred]**.
- Plain alpha-tested draws (net, hair, crowd cards **[unverified]** which ones are) could
  write `SV_Coverage` from alpha when the forced count is above the guest's, as Unleashed does.
  The alternative is sample shading (`sampleShadingEnable`/`minSampleShading`): the fragment
  shader runs at least ⌈minSampleShading × samples⌉ times per pixel, which costs up to N times
  the shading, and it needs the `sampleRateShading` feature
  ([VkPipelineMultisampleStateCreateInfo](https://docs.vulkan.org/refpages/latest/refpages/source/VkPipelineMultisampleStateCreateInfo.html),
  [primsrast](https://docs.vulkan.org/spec/latest/chapters/primsrast.html)).
- Vulkan guarantees 1x and 4x for colour and depth, nothing else
  (`framebufferColorSampleCounts`/`framebufferDepthSampleCounts` minimum `1|4`,
  [limits](https://docs.vulkan.org/spec/latest/chapters/limits.html)). 8x must be queried.

**Risks.**

- Depth resolves take sample 0, so any pass that reads the resolved depth of a 4x target sees
  one sample, as it does at 2x today. No change.
- The dither in alpha to mask is per host pixel (`pixel & 1`), so at scale > 1 the dither
  pattern gets finer **[inferred]**. Harmless after downsampling, but it is a difference from
  the Oracle.
- Vulkan's standard sample positions differ from Xenos'. At 4x the edge gradients will not
  match Edge pixel for pixel even at 1x scale (Edge `gpu_flags.cc:289-295`).

**Verify.**

- Same-frame RenderDoc capture with `--ts4_native_dual_compare`: check that the match pass is
  4x (`'draw EID'` prints raster state) and that the resolved front buffer still lines up with
  Classic's 2x frame. Expect differences only on edges, and on alpha-to-mask foliage in the
  Academy scene.

## 3. Anisotropic filtering

**What it is.** Filters along the axis of a texture's footprint, which keeps oblique surfaces
sharp. A tennis court seen from the broadcast camera, with its lines and the stand banners,
is the textbook case **[inferred]**.

**How others did it.**

- Xenia's `anisotropic_override`: -1 is the game's own setting; 0 to 5 is off to 16x
  (Edge `gpu_flags.cc:254-266`, default -1).
- ReXGlue copies it with default 3, which is 4x (`SDK:src/graphics/pipeline/texture/cache.cpp:52-64`).
- Unleashed forces `AnisotropicFiltering` 16 on every linear-mipmapped sampler and makes
  min/mag linear (`config_def.h:69`, `gpu/video.cpp:4379-4396`).
- Vulkan: needs the `samplerAnisotropy` feature, and `maxAnisotropy` is clamped to
  `maxSamplerAnisotropy`, which is at least 16 when the feature exists
  ([VkSamplerCreateInfo](https://docs.vulkan.org/refpages/latest/refpages/source/VkSamplerCreateInfo.html),
  [limits](https://docs.vulkan.org/spec/latest/chapters/limits.html)).

**Mapping onto our code.**

- `ChooseSampler` (`src/native/sampler_choice.h:21-48`) applies `ts4_native_anisotropy` to
  linear, mipmapped fetches. It is already live (`kHotReload`) and part of the sampler key.
- Classic uses the SDK's `anisotropic_override`.
- Both are one settings-registry entry away from a player option.

**Decision for the owner.**

- The Oracle runs at -1 (`xenia-edge.config.toml:224`). Our two renderers default to 4x
  (native-renderer.md "Same-frame differences" made Native match Classic).
- By ADR-0001, 4x is an Enhancement on by default. Either:
  - set `anisotropic_override = -1` for Classic and `ts4_native_anisotropy = -1` for Native as
    the original look, and offer 16x as the Enhancement; or
  - record 4x as an accepted deviation.
- 16x costs bandwidth but is cheap on desktop GPUs (Edge's help text: "most GPUs handle up to
  16x fine", `gpu_flags.cc:256-257`).

**Risks.** "In rare cases, forcing AF can introduce visual artifacts" (Edge `gpu_flags.cc:258`).
Typical cases are point-sampled UI or lookup textures. `ChooseSampler` already limits the
override to linear, mipmapped fetches.

**Verify.** This is the easiest Oracle match there is. Run Edge with `anisotropic_override = 5`
and Native with `ts4_native_anisotropy = 5` at the same scene point (the court from the
gameplay camera) and compare crops of the far baseline and banners. At -1, Native against
Edge at -1 must match within the dual-run tolerance.

## 4. Texture LOD (mip) bias

**What it is.** A bias added to the hardware's mip selection. Negative is sharper and shimmers
more.

**Primary guidance.**

- All three vendors recommend a negative bias when the render resolution is *below* the
  display:
  - AMD FSR 2/3: `mipBias = log2(renderResolution / displayResolution) - 1.0`
    ([FSR 2](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-temporal/),
    [FSR 3 upscaler](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-upscaler/));
  - NVIDIA DLSS: `NativeBias + log2(RenderX / DisplayX) - 1.0 + epsilon`
    ([DLSS guide](https://github.com/NVIDIA/DLSS/blob/main/doc/DLSS_Programming_Guide_Release.pdf) §3.5);
  - Intel XeSS: a formula of the same kind
    ([XeSS SR guide](https://github.com/intel/xess/blob/main/doc/xess_sr_developer_guide_english.md), lines 88-94).
- The -1 term is for *temporal* accumulation, which recovers detail over frames. For the
  spatial FSR 1, AMD only says the input "should be generated using negative MIP bias"
  ([FSR 1](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-spatial/)).
- Vulkan clamps the total bias to `maxSamplerLodBias`, at least 2
  ([VkPhysicalDeviceLimits](https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceLimits.html),
  [limits](https://docs.vulkan.org/spec/latest/chapters/limits.html)).

**Our code.**

- `LodBiasFor(scale)` returns 0, -0.5 or -1 by scale alone (`guest_decode.h:595-597`).
- It is added to the fetch constant's own bias and clamped to ±2 (`src/native/textures.cpp:371-376`).
- `ts4_native_lod_bias` overrides it (`textures.cpp:45-48, 685-687`).

**Assessment [inferred].**

- When Native renders above the display and filters down, hardware mip selection already
  picks log2(N) finer mips. That is the point of supersampling. An extra -1 at 3x adds
  aliasing that the downsample then has to hide, and it doubles texture cache pressure.
- When the frame is shown at its own size (3x on a 4K panel), the "right" bias by the formula
  above (without the temporal term) is 0.
- Only when the presenter *upscales* (scale 1 on a 1440p panel, or FSR 1) does a negative bias
  of log2(render/display) make sense.
- Suggested rule: `bias = min(0, log2(render_height / display_height))`, computed in
  `PlanOutput`, which already knows both.
- WWE's image-quality grilling chose "LOD bias unchanged"
  (`WWE_ALLSTARS-RECOMP/.scratch/image-quality/map.md`), so this is a deliberate change.

**Risks.** Swing's own fetch biases (-1, -2 on foliage) must keep being added. The rule
changes only the global term.

**Verify.** At scale 2 or 3, Native with the old rule against the new rule on a moving camera
over the court and crowd, compared for temporal shimmer. The 1x look against the Oracle is
unchanged, because the bias is 0 at scale 1 under both rules.

## 5. Post-process AA: SMAA and FXAA

**What it is.** Image-space edge detection and blending on the final colour.

- [FXAA](https://developer.download.nvidia.com/assets/gamedev/files/sdk/11/FXAA_WhitePaper.pdf)
  (Lottes) "is engineered to be applied towards the end of engine post processing after
  conversion to low dynamic range and conversion to the sRGB color space", and "should be
  applied prior to drawing the HUD or UI elements".
- [SMAA](https://github.com/iryoku/smaa) (MIT-style licence, `LICENSE.txt`) is sharper. It
  also has an **S2x mode for a scene "rendered using MSAA 2x"**, which runs SMAA on each
  subsample and blends the results (`SMAA.hlsl:229-258`). That is exactly what Top Spin 4's
  match pass already is.

**How others did it.**

- Xenia: `postprocess_antialiasing = fxaa | fxaa_extreme` on the final output (Edge
  `app/emulator_window.cc:196-212`). It is "heavily recommended when ... CAS or ... FSR 1.0 is
  active".
- ReXGlue: the same as `swap_post_effect` (`SDK:src/graphics/graphics_system.cpp:52`).
- WWE dropped FXAA because on the final frame it "warps text"
  (`WWE_ALLSTARS-RECOMP/.scratch/image-quality/map.md`).

**Mapping onto our code.**

- The cheap path is `swap_post_effect=fxaa` on the final frame. It works on both renderers
  today, but it smears the HUD and menu text, against the FXAA paper's own guidance.
- A proper pass needs a **pre-UI insertion point**: the draw after which only the HUD is drawn
  into the 1280x720 target. Native sees every draw's shader hash and target, so a pass can be
  keyed on the composite shader's hash, found once with `--ts4_native_trace_frame`
  **[unverified]**: no such point has been identified for Top Spin 4.
- SMAA S2x would read the 2x match target before `SingleSample` resolves it.

**Payoff [inferred].** Low on desktop once #1 and #5 exist, because supersampling plus MSAA
covers edges better. Worth it only as the Deck's AA if supersampling is too expensive there.

**Verify.** Pass off: no change. Pass on: the HUD and menus must be bit-identical to "off" (the
pass ran before the UI), and 3D edges differ.

## 6. Sharpening and spatial upscaling: CAS and FSR 1

- [CAS](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/contrast-adaptive-sharpening/)
  is "a low overhead adaptive sharpening algorithm with optional up-sampling".
- [FSR 1](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-spatial/)
  expects input that is already "well anti-aliased", in "perceptual color space",
  "noise free", and placed "before ... User Interface rendering".
- In the SDK presenter both run on the final frame, UI included
  (`SDK:src/ui/presenter.cpp:50-75`; Xenia's equivalent `postprocess_scaling_and_sharpening`,
  Edge `emulator_window.cc:213-229`).

**Our code.** `ts4_native_cas` (`native_renderer.cpp:128-131`) drives the presenter's CAS,
live (`ApplyLiveSharpening`, `:1674-1690`). It is listed as an Enhancement (`:1855-1861`). FSR 1
is selectable through `present_effect=fsr`, and the build has the FidelityFX headers
(`scripts/build.ps1:92-94`).

**Recommendation.**

- Expose CAS as a player slider; WWE shipped 0.5 as its default
  (`WWE_ALLSTARS-RECOMP/.scratch/image-quality/map.md`).
- Offer FSR 1 only when render < display, that is, Classic on a big screen, or Native at
  scale 1 on the Deck docked. It applies to Classic too, which is the only meaningful
  upgrade Classic can get besides AF.
- Do not expose `fsr2`/`fsr3` (§7).

**Verify.** Edge has the same effects (`postprocess_scaling_and_sharpening = "cas"`,
`postprocess_ffx_cas_additional_sharpness`), so a matched pair of screenshots is possible.

## 7. Temporal upscalers and TAA (DLSS, FSR 2/3, XeSS)

**What they need.**

- Jittered rendering plus per-pixel motion vectors. DLSS: "per-pixel motion vectors as a key
  component of its core algorithm", in RG16F/RG32F (§3.6), with sub-pixel jitter (§3.7). FSR:
  "all inputs that are provided at Render Resolution, except for motion vectors, should be
  rendered with jitter".
- XeSS likewise "accepts rendered low-resolution aliased jittered color as input, as well as
  motion vectors and depth" (guide lines 60-64).

**Does Top Spin 4 provide them?**

- No velocity target exists in any scene run. Every colour target is RGBA8 (§0).
- Motion vectors could in principle be synthesized from depth plus the previous frame's
  view-projection, read from VS constants. That covers the camera only. Skinned players, the
  ball and the racket would ghost, and they are exactly what a tennis game is about
  **[inferred]**.
- Jitter would have to be injected into every converted vertex shader's `oPos` (the
  half-pixel offset path in XenosRecomp, `shader_recompiler.cpp:2644`, is the natural place)
  **[inferred]**.

**What the SDK's "fsr2/fsr3" do.** They pass the colour image as depth and motion, with zero
jitter and `reset = true` on every dispatch (`SDK:src/ui/vulkan/vulkan_presenter.cpp:377-414`).
The SDK warns that it is "an experimental temporal upscaler path with synthesized depth/motion
inputs" (`SDK:src/ui/presenter.cpp:283-284`). In practice it is a spatial upscaler.

**Verdict.** Not realistic for this game. #1 + #5 deliver a cleaner image without ghosting.

## 8. Off-screen targets, shadows, and texel offsets under scaling

**Texel offsets.**

- Xenos texture fetches can carry immediate texel offsets. XenosRecomp emits them as
  `offset * 0.5` (`shader_recompiler.cpp:496-497`), and `tfetch2D` adds
  `offset / getTexture2DDimensions(texture)` (`shader_common.h:237-242`). That divides by the
  *host* size.
- For a resolve target at scale N, the offset shrinks N times. Blur kernels and shadow PCF
  taps collapse towards the centre: shadows get harder and more aliased, bloom gets tighter.
- Edge fixes this per fetch slot, from a bitmask of "textures resolved" in its system
  constants (`spirv_shader_translator_fetch.cc:1252-1290`, enabled by default,
  `render_target_cache.cc:156-161`). Its help text names "shadow filtering, bloom".
- **Fix [inferred]:** a XenosRecomp patch that divides by the guest size, that is the host
  size divided by a per-slot scale. Native knows each bound texture's `scale`
  (`textures.h:58`) and can pass a 32-bit mask in the shared constants, as `g_AlphaToMask`
  does. It changes the converter id, so the pack is rebuilt (`docs/agents/native-debugging.md`,
  "After a fix").

**A per-target scale policy.**

- Today every target is multiplied (`GetRenderTarget`).
- Edge's experience is that small post-process surfaces often break when scaled
  (`render_target_cache.cc:162-170`).
- Top Spin 4 has three kinds of target:
  - the scene and its half-resolution post chain (640x360 is exactly half of 720p);
  - the probable shadow map (1040x1024);
  - small special targets: the radar gun's 256x64 mip chain, and 560x128, 320x64, 160x128.
- Suggested policy [inferred]:
  - the scene and anything 1/2 or 1/4 of it scale with N;
  - the shadow map scales with its own "Shadow quality" factor;
  - targets under a pitch threshold stay at 1x.
- `RenderTarget::scale` and the resolve path already carry a per-target scale, so this is a
  key-based choice inside `GetRenderTarget`.

**Shadow resolution as an Enhancement.**

- Unleashed hooks the game's shadow-size choice and exposes `ShadowResolution` up to 4096
  (`config_def.h:70`, `gpu/video.cpp:5888-5893`).
- Native could do the opaque version (scale the shadow target) with no guest hook. That only
  sharpens the shadows if the PCF offsets are fixed as above. Otherwise the kernel shrinks
  with the texels and the effect is the same as a smaller kernel **[inferred]**.
- First confirm which target holds the shadow depth: `pwsh scripts/rdoc.ps1 capture` on a
  match frame, then `query … 'draw EID'` on a shadow-receiving court draw.

**Verify.** Same-frame captures at scale 1 and 2: the shadow penumbra width, measured in guest
pixels, should match after the offset fix.

**Found (ticket 05).** Top Spin 4's shaders use no fetch-instruction offsets. Bloom and shadow
taps are UV offsets from constants and shader literals, so they already cover the same guest area
at any scale. The offset fix (`0007`) is in place but changes no sample. Evidence:
native-renderer.md, "Shadow map and texel offsets at scale".

## 9. Half-pixel offset at scale

- Native shifts geometry by half a *guest* pixel when `PA_SU_VTX_CNTL` asks for D3D9 centres
  (`DrawClipTransform`, `native_renderer.cpp:1386-1393`; `ComputeClipTransform`,
  `guest_decode.h:482-511`).
- That matches Xenia's choice to "treat guest pixels as a whole" for the half-pixel offset under
  scaling (Edge `draw_util.cc:562-575`, `476-480`).
- Edge still needs `resolve_resolution_scale_fill_half_pixel_offset` for a gap at the top and
  left edges of resolves (`draw_util.cc:33-39`).

**Risk [inferred].**

- A full-screen post pass that samples a scaled resolve texture with linear filtering lands
  its UVs (N-1)/2 host texels off each host pixel centre. At N=2 every such pass blends two
  host texels, a half-texel blur. At N=3 it is a one-host-pixel shift.
- Long post chains (bloom down and up) accumulate this. It is invisible after the 2:1
  downsample at N=2, but it can show as softness or a slight offset of the bloom at native-res
  output.
- Unleashed avoids the question by rendering game-aware: its half-pixel offset is 1/width of
  the host framebuffer (`gpu/video.cpp:3655-3656`).
- A per-draw choice would be possible: guest-pixel offset for geometry, host-pixel offset for
  draws with `PA_CL_VTE_CNTL` scales off, which are the game's full-screen passes (the
  non-clip branch of `ComputeClipTransform`). Whether that is better needs a test.

**Verify.** At scale 3 with output at native size, compare the bloom halo position against a
1x frame upscaled 3x (nearest). A consistent 1-pixel offset of the glow is this artifact. Also
check the top and left edges of the frame and of the radar-gun texture for an unfilled row.

## 10. Aspect ratio and ultrawide

- Top Spin 4 renders 16:9 at 1280x720 (§0). The presenter letterboxes other window shapes
  (`present_letterbox`, `SDK:src/ui/presenter.cpp:38`).
- Native's `PlanOutput` fits the window's 16:9 area (`guest_decode.h:606-612`).
- **How Unleashed did ultrawide:**
  - a camera hook sets the horizontal aspect to the window's (`patches/camera_patches.cpp`,
    `CameraAspectRatioMidAsmHook`), with Vert+ FOV for narrow ratios (`AdjustFieldOfView`);
  - a 1,688-line `patches/aspect_ratio_patches.cpp` repositions every UI scene (CSD casts) by
    a per-element table;
  - the device back-buffer size follows the window (`gpu/video.cpp:5897-5904`).
  - Cutscenes stay at their original aspect by default because of "visual oddities ... as the
    game was not designed for wider formats"
    ([README:139-144, 268-270](https://github.com/hedge-dev/UnleashedRecomp/blob/cf829a9eca8fb680fba4b0409ddeb6ca92f22e3c/README.md#L139)).
- **For Top Spin 4 [inferred]:** this needs RE in Swing for the projection, the broadcast and
  replay cameras, the HUD layout (scoreboard, radar gun, call labels), and possibly culling of
  the stands at wider FOV. All 1x targets would also need a wider guest size, which the game
  allocates itself. It is the largest item on the list.
- `docs/goals.md` lists only resolution scaling, MSAA and remapping for 1.0, so ultrawide
  belongs after 1.0.

**Verify.** It cannot be checked against the Oracle (Edge renders 16:9). The check is internal:
the 16:9 crop of an ultrawide frame equals the 16:9 frame.

## 11. Frame rate and high-refresh displays

- 1.0 is locked to the game's 60 fps tick, because the simulation is tied to frame rate
  (`docs/goals.md`; Canary #358 `vsync-off-speedup`).
- The 30 fps cutaways are original behaviour and must stay (oracle-baseline.md "Frame rate").
- Unleashed shows the cost of going higher: "a vast amount of glitches that usually occur at
  higher frame rates have been fixed", and some remain (README:132-137, 258-266).
- On 120/144 Hz panels the image-quality question is pacing: a steady 60 on VRR, or an even
  divisor. The presentation patches (`patches/rexglue/0004-frame-pacing`, `0013-smooth-presentation`,
  `0024-guest-vblank-rate`) cover that already.
- Above 60 belongs after 1.0, with a decoupled tick or interpolation.

## 12. HDR, tonemapping, colour and banding

- **No HDR source.** Every scene target is RGBA8 (§0), so the game tonemaps into 8 bits
  itself. A host "HDR output" would mean inverse tonemapping, which changes the art and fails
  ADR-0001's "original by default" without a strong payoff **[inferred]**.
- **Colour:** `ts4_native_color_mode` already offers Original (Xenos PWL gamma, as Xenia) and
  Natural (sRGB), with Natural listed as an Enhancement (`native_renderer.cpp:137-140, 1855-1861`).
- **Banding:** Edge dithers its final output to 8 bpc by default (`postprocess_dither = true`,
  `emulator_window.cc:263-271`; also the Oracle's config, line 193). The SDK has
  `present_dither`, default **off** (`SDK:src/ui/presenter.cpp:86`). Dithering changes nothing
  for an unresampled 8 bpc image, by Edge's own comment (`emulator_window.cc:260-262`). When
  Native downsamples or the presenter scales, though, it smooths gradients in the sky and the
  stadium shadows **[inferred]**. Cheap to try; it is a presenter flag on both renderers.

## 13. Assets: textures and LOD

- **Texture replacement:**
  - WWE All Stars took texture packs into scope: Native dumps by key, loads replacements, and
    the community makes the packs; the project never ships them
    (`WWE_ALLSTARS-RECOMP/docs/adr/0028-texture-packs-in-scope.md`). Its pipeline is dump →
    ESRGAN-family upscale → BC7/BC5 → load by key
    (`WWE_ALLSTARS-RECOMP/docs/research/graphics-remaster.md:316-343`).
  - Our copy already hashes every texture with XXH3 (`src/native/textures.cpp:476-479`) and can
    dump DDS (`ts4_native_dump_texture`, `textures.cpp:49-52`).
  - Unleashed instead relies on file replacement through Hedge Mod Manager. It does not support
    replacing `default.xex` or shaders (README:207-211).
  - Top Spin 4 has a patch-archive path the game itself reads (`Media\Patch\*`, feasibility.md
    §5), which could carry replaced textures without a host loader. That needs the patch FAT
    format reversed **[unverified]**.
  - Mods are post-1.0 (`docs/goals.md`).
- **Risks:** streamed mips change the hash of the base level. Player faces and outfits may be
  composited at runtime (resolve-to-texture, feasibility.md "Face rendering"), so they cannot
  be replaced by key **[inferred]**.
- **LOD distance:** no work has been done on Top Spin 4's model LOD selection. Players are
  near the camera in gameplay; the crowd and wide shots are where LOD would show
  **[unverified]**. WWE's note lists LOD0 forcing as RE-heavy
  (`graphics-remaster.md:249`). Not before 1.0.

## Verifying Enhancements against the Oracle: the common method

1. **Off means identical.**
   - Every new option goes into `kEnhancements` (`native_renderer.cpp:1855-1861`) and
     `scripts/scene_runner.py`'s list (`:236-238`), so a parity run with one on fails.
   - The existing dual-mode scenes stay the gate for the default look.
   - A new Native option that is not off by default (like AF today) must be compared against
     the Oracle once, at its default.
2. **On means structurally the same frame.**
   - Dump frames with the option on, normalize them to 1280x720 (box-downsample scaled frames),
     and compare with the 1x dual frame of the same run at a looser threshold.
   - Hot regions that are edges are expected. Hot regions that are areas (a missing glow, a
     shifted layer, a hard shadow) are bugs.
3. **Where Edge has the same option, compare like with like:**

   | Our option | Edge setting |
   |---|---|
   | AF | `anisotropic_override` |
   | CAS / FSR 1 | `postprocess_scaling_and_sharpening`, `postprocess_ffx_*` |
   | FXAA | `postprocess_antialiasing` |
   | Resolution scale | `draw_resolution_scale_x/y` (+ `_threshold`, `_scaled_texture_offsets`), if Edge survives it |

   Edge's D3D12 cannot be captured with RenderDoc. Use its Vulkan backend for captures
   (oracle-baseline.md "Setup").
4. **Per-pass checks** use the RenderDoc tools in `docs/agents/native-debugging.md`: `history`
   on a pixel of the bloom or shadow, `texture ID` on a resolve target, at scale 1 and 2.

## Measured: GPU frame cost at the maximum settings (desktop)

Measured 2026-10-27 (image-quality ticket 09) on the desktop: AMD Radeon RX 7900 XT, Vulkan
1.4, the hidden-window headless run at a 1280x720 output. Source: the Native renderer's GPU
timer (`--ts4_native_gpu_timer_csv`, timestamp queries around each frame's work), the `frame`
rows of one Native-only run of `tests/scenes/exhibition-match.toml` to frame 4800 per setting
(`analysis/scenes/iq09/native_runs.py`, `cost.py`, `cost.txt`). Maximum = Resolution scale 4
(render 5120x2880, not clamped), MSAA 8x, 16x anisotropic filtering, High CAS. CAS is not in
these numbers: it runs in the presenter, after the timer's `present` span (Native's own copy);
its cost is measured on its own below.

| Part (frames) | Defaults: GPU ms median / p95 / p99 / max | Maximum: GPU ms median / p95 / p99 / max |
|---|---|---|
| Menus (600-2399) | 0.93 / 1.65 / 1.71 / 2.03 | 5.52 / 8.44 / 9.66 / 13.47 |
| Match (3000-4800) | 1.56 / 8.54 / 9.78 / 10.20 | 7.32 / 13.42 / 16.86 / 19.66 |

- The maximum costs about 4.6 ms (menus) to 5.8 ms (match) more GPU time per frame (median), roughly 4 to 6 times the
  default. The median stays well inside the 16.7 ms of a 60 fps frame; in the match about 1% of
  frames take longer than 16.7 ms on the GPU. Both runs took the same wall time (86.8 s and
  87.1 s for 4802 guest swaps), so the game kept its 60 fps tick; whether those frames show as
  stutter on a visible swapchain was not measured (the runs present to a hidden window).
- The renderer's CPU time per frame does not grow (match median 1.04 ms at defaults, 0.98 ms at
  the maximum).
- Not split by Enhancement: one run per setting set, maximum against defaults only. The per-pass
  rows in the same CSVs (`analysis/scenes/iq09/cost-*/gpu.csv`) are there for a split.
- CAS, measured separately 2026-10-28 (ticket 09) in one RenderDoc capture, since the timer does
  not see it: Native, `--ts4_sharpening=high`, everything else at default, exhibition match frame
  3600 (`scripts/rdoc.ps1 capture -Frame 3600 -InputScript scripts/input/boot-to-exhibition.txt`),
  same RX 7900 XT, 1280x720 output. RenderDoc's `EventGPUDuration` on replay (`scripts/rdoc/query.py
  durations`, `analysis/scenes/iq09/cas-cost/query-*/query.txt`): the presenter's CAS compute
  dispatch (1280x720 RGBA8 in, RGB10A2 out) takes **about 0.021 ms** (0.0210 and 0.0216 ms in two
  replays; 0.056 and 0.060 ms in two others where every event ran slower, likely GPU clocks).
  The presenter's draw to the swapchain after it takes 0.015 ms, Native's own `present` blit
  0.013 to 0.015 ms. The iq03 practice-ground High capture gives the same (CAS 0.0205 ms). CAS
  runs at the output size, so the Resolution scale does not change it; at a 3840x2160 output it
  would be about 9 times as much (some 0.2 ms, an estimate, not measured). Replay times each event
  on its own, so the frame's sum on replay (6.2 to 7.9 ms) is not comparable to the timer's numbers
  above.

## Unverified and open

- ~~Whether Edge `4d07204` survives `draw_resolution_scale` 2 in Top Spin 4.~~ It does (ticket
  09, `analysis/scenes/iq09/edge-practice-scale2/`): Edge `4d07204` on D3D12 with
  `draw_resolution_scale_x = 2` and `_y = 2` boots to the practice ground; its title bar says
  `RTV/DSV 2x2`. Its frame sits about 2 px (of a 1028-wide view) right of Edge at 1x and of the
  port, and is sharper than Native at scale 2 after the same downscale. Canary crashes (#358).
- Which target is the shadow map (1040x1024 is the candidate), and what 560x512, 560x128,
  320x256 and 160x128 are.
- Where the HUD starts in a match frame (the pre-UI point for SMAA or any host pass).
- Which alpha-tested draws (net, hair, crowd) would gain from alpha to coverage under forced
  MSAA.
- Steam Deck GPU cost of scale 2, 4x MSAA and 16x AF (ticket 30).
- Whether the game's half-resolution post chain shows the half-pixel artifact of §9 at scale 3.
- Device limits for scale 4 (5120-wide targets; 4160x4096 for the 1040x1024 target).

## Sources

- **This repo:**
  - `src/native/native_renderer.cpp` (117-153, 355-361, 1099-1300, 1386-1442, 1630-1744,
    1846-1861, 1995-2004, 2120-2175, 2436-2475, 3124-3190);
  - `src/native/guest_decode.h` (455-511, 574-643);
  - `src/native/sampler_choice.h`, `src/native/textures.cpp` (45-52, 360-380, 476-479,
    682-687), `src/native/pipelines.cpp` (345-375);
  - `src/settings/registry.h`, `scripts/build.ps1` (92-94), `scripts/scene_runner.py`
    (222-238), `patches/xenosrecomp/0006-alpha-to-mask.patch`;
  - `.tools/xenosrecomp/XenosRecomp/shader_common.h` (232-242),
    `.tools/xenosrecomp/XenosRecomp/shader_recompiler.cpp` (470-500, 2644);
  - `.tools/xenia-edge/xenia-edge.config.toml` (183-224, 272-283, 354-378, 406);
  - `analysis/scenes/*/*/runtime.log` (local);
  - docs: goals.md, ADR-0001, ADR-0004, ADR-0005, oracle-baseline.md, native-renderer.md,
    feasibility.md, recomp-community-help.md, docs/agents/native-debugging.md, ticket 28.
- **ReXGlue SDK (vendored):**
  - `src/graphics/pipeline/texture/cache.cpp` (52-94), `src/graphics/util/draw.cpp` (29-41),
    `src/graphics/flags.cpp` (18-26);
  - `src/graphics/graphics_system.cpp` (52), `src/graphics/pipeline/shader/dxbc_translator_fetch.cpp` (27);
  - `src/ui/presenter.cpp` (38-90, 283-284), `src/ui/vulkan/vulkan_presenter.cpp` (377-414).
- **Xenia Edge** at [`82a9462`](https://github.com/has207/xenia-edge/tree/82a946231b2356901afe9e94c7ceacfffc936193):
  - `src/xenia/gpu/texture_cache.cc` (30-42);
  - `src/xenia/gpu/render_target_cache.cc` (156-170, 692-708);
  - `src/xenia/gpu/spirv_shader_translator_fetch.cc` (1252-1290);
  - `src/xenia/gpu/draw_util.cc` (33-39, 332-349, 476-480, 562-575);
  - `src/xenia/gpu/gpu_flags.cc` (254-266, 289-301);
  - `src/xenia/gpu/xenos.h` (481-485);
  - `src/xenia/app/emulator_window.cc` (196-271).
- **UnleashedRecomp** at [`cf829a9`](https://github.com/hedge-dev/UnleashedRecomp/tree/cf829a9eca8fb680fba4b0409ddeb6ca92f22e3c):
  - `README.md` (105-117, 128-144, 207-211, 258-270);
  - `UnleashedRecomp/user/config_def.h` (58-76);
  - `UnleashedRecomp/gpu/video.cpp` (1022-1040, 1652-1654, 1823-1836, 3193, 3655-3656,
    4379-4396, 5205-5228, 5888-5904);
  - `UnleashedRecomp/patches/camera_patches.cpp`, `UnleashedRecomp/patches/aspect_ratio_patches.cpp`,
    `UnleashedRecomp/gpu/shader/resolve_msaa_*`.
- **WWE All Stars (local):** `docs/research/native-image-quality.md`,
  `docs/research/graphics-remaster.md`, `docs/adr/0028-texture-packs-in-scope.md`,
  `.scratch/image-quality/map.md`.
- **Vulkan:** [VkSamplerCreateInfo](https://docs.vulkan.org/refpages/latest/refpages/source/VkSamplerCreateInfo.html),
  [VkPipelineMultisampleStateCreateInfo](https://docs.vulkan.org/refpages/latest/refpages/source/VkPipelineMultisampleStateCreateInfo.html),
  [VkPhysicalDeviceLimits](https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceLimits.html),
  [VkPhysicalDeviceFeatures](https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceFeatures.html),
  [vkCmdBlitImage](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdBlitImage.html),
  [Limits chapter](https://docs.vulkan.org/spec/latest/chapters/limits.html) (required limits
  table), [Rasterization chapter](https://docs.vulkan.org/spec/latest/chapters/primsrast.html)
  (sample shading).
- **AMD GPUOpen:** [FSR 1](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-spatial/),
  [CAS](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/contrast-adaptive-sharpening/),
  [FSR 2](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-temporal/),
  [FSR 3 upscaler](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/super-resolution-upscaler/).
- **NVIDIA:** [DLSS Programming Guide](https://github.com/NVIDIA/DLSS/blob/main/doc/DLSS_Programming_Guide_Release.pdf)
  §3.5 (mip bias), §3.6 (motion vectors), §3.7 (jitter);
  [FXAA white paper](https://developer.download.nvidia.com/assets/gamedev/files/sdk/11/FXAA_WhitePaper.pdf).
- **Intel:** [XeSS SR developer guide](https://github.com/intel/xess/blob/main/doc/xess_sr_developer_guide_english.md).
- **SMAA:** [iryoku/smaa](https://github.com/iryoku/smaa) (`SMAA.hlsl` 125-134, 229-258; `LICENSE.txt`).
- **Xenia Canary compatibility:** [game-compatibility#358](https://github.com/xenia-canary/game-compatibility/issues/358).
