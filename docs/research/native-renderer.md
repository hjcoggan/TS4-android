# Native renderer research

Ticket 26. The Native renderer is ported from WWE All Stars (ADR-0004, ADR-0005). It hooks the
game's statically linked XDK Direct3D 9 and draws with Vulkan. Top Spin 4 links two copies of
D3D9, one in Loader and one in Swing (module-inspection.md), so every hook exists once per
Module. The hook table is `symbols/d3d-functions.csv`.

## Finding the functions (2026-10-25)

Both copies are XDK 2.0.20209. WWE All Stars links 2.0.11775, the same as SvR 2011, whose D3D
functions WWE named.

- **`scripts/match_functions.py`**, copied from WWE and widened to near matches, matches WWE's
  D3D library (TU2 image, `0x82DA7AA0`-`0x82DCECA0`) into each Module. Into Loader it gives 285
  exact and 273 near matches out of 713 functions. Into Swing it gives 156 exact and 255 near
  matches. Matching Loader's copy into Swing (`0x830A0000`-`0x830F0000`) confirms every Swing
  hook target. Output goes to `analysis/function-matches-<module>.tsv` (not committed).
- **`scripts/scan_pm4.py`**, copied from WWE, lists functions that build PM4 type-3 headers.
  Every `DRAW_INDX` and `DRAW_INDX_2` builder lies inside its Module's D3D copy: Loader
  `0x830AA5B0`-`0x830E2978`, Swing `0x882888F0`-`0x882A3C60`. Only `LOAD_ALU_CONSTANT` and
  `WAIT_FOR_IDLE` builders lie outside. So no game code writes draw packets itself. Every
  draw goes through a function the table lists, and the census run checks that.
- **Present** is the only caller of `VdSwap` in each Module: Loader `0x830B8C60`, Swing
  `0x88295D80`. The matcher agrees.
- **Resolve** is only a near match (about 70%), but its prologue saves the same arguments:
  device in r3, flags in r4 (bit 17 tested first) and f1. The arguments differ from WWE,
  though: the callers in both Modules pass the XDK's order, destination texture in **r6**,
  destination point in r7 and mip level in r8. WWE's older XDK took the destination in r8.
  Read from r8, every destination was 0 and Native never found its front buffer.

## Device layout: same as WWE All Stars

The renderer reads draw state from the guest `D3DDevice` (WWE `guest_decode.h`,
`native_renderer.cpp`). The offsets read below are the same in all three libraries:

| Field | Offset | Seen in |
|---|---|---|
| Ring write cursor; ring reserve | `0x30`; `0x3554` | EndVertices bytes (Loader), Swing's inline ends |
| Index buffer | `0x3144` | SetIndices, same access sequence |
| Vertex declaration | `0x2ED8` | SetVertexDeclaration (Loader, byte-equal) |
| Pixel and vertex shader | `0x3244`, `0x3248` | stores in both copies |
| Shader objects | VS microcode `+0x20`, header `+0x368`; PS microcode `+0x18`, header `+0x28` | Register*Shader, same code |

The register-mirror groups match too (see the draw census below).

## Where the copies differ

- **Swing has no EndVertices.** LTCG inlined it into BeginVertices' two game callers,
  `0x887CB428` and `0x887CAF80`, which end the draw themselves (device `+0x3554` to `+0x30`)
  and then call `0x887CA798`, which restores device state (textures, fetch constants). The
  pending draw is taken on entry to `0x887CA798`, before that restore. DrawVerticesUP
  (`0x882934C0`) still ends its own draws.
- **Swing's DrawVertices always starts at vertex 0.** LTCG specialised it: it keeps r5 as the
  vertex count (`mr r24,r5`) and a zero as the start (`mr r23,r26`), where Loader's and WWE's
  copies take the start in r5 and the count in r6. Read the WWE way, the menu labels (quad
  lists of 28 to 216 vertices) became 4-vertex draws from vertex 28 to 216, and the main
  menu lost all its text. Its row is `DrawVerticesFromZero`.
- **Swing has no BeginIndexedVertices or DrawIndexedVerticesUP** (Loader does). Swing's
  SetVertexDeclaration is inlined too. That is harmless, because the renderer reads the
  declaration from the device at draw time.
- **Loader's DrawIndexedVertices has no direct caller**, nor do its other two `DRAW_INDX`
  builders (`0x830E24D8`, `0x830E2978`). Loader's XUI draws through DrawVertices (36 callers)
  and the user-memory paths. Loader has two byte-equal EndVertices bodies (`0x830E1600`, with
  2 callers; `0x830E1B98`, with none).

## Draw census (2026-10-25)

Hooks are generated per Module (`cmake/d3d_hooks.cmake`, `src/d3d/hooks.h`). They count
their calls through the sink library (`src/d3d/sink.h`). One headless Classic run to the main
menu with `--ts4_d3d_census --gpu_draw_census` (`analysis/smoke-20261025-141001`), compared
per 60-swap window:

| Window | GPU DRAW_INDX | Hooked | GPU DRAW_INDX_2 | Hooked | GPU IBs | Hooked |
|---|---|---|---|---|---|---|
| Loader, title (swaps 420-480) | 240 | 240 BeginVertices | 120 | 60 ClearRect + 60 Resolve | 120 | 120 |
| Swing, main menu (steady) | 17460 | 10440 DrawIndexedVertices + 420 DrawVertices | 3960 | 480 ClearRect + 600 Resolve | 1140 | 780 |

- **Loader is fully covered.** Its DrawVertices calls add no GPU draws of their own.
- **Swing's DRAW_INDX_2 gap is D3D's 48 point-list draws per frame**, all from one IB
  (`1147C980`, prim 1). WWE found the same pattern: a D3D-internal sync buffer with no visible
  content. Without them, 3960 - 2880 = 1080 = ClearRect + Resolve.
- **Swing's DRAW_INDX gap (about 110 per frame) is predicated tiling with 2 tiles.** A
  temporary GPU-side log of one main-menu frame's draws (284 DRAW_INDX) was set against
  Native's trace of the same screen (175 draws), counting how often each index count occurs.
  Every difference is a draw inside the tiled pass executed once more: GPU/Native 2/1 (25
  index counts), 4/2, 6/3, 10/5, and 5/3 (one draw outside the pass, two inside). So
  175 + 109 = 284. Every GPU draw comes through a hook. Tile replays run from changing ring
  segments, not static command buffers (no unchanging IB held a draw). A sweep build
  (`scripts/gen_d3d_sweep.py`, `-DTOPSPIN4_D3D_SWEEP=ON`: 627 counted functions in
  `0x88286000`-`0x882AF000`, `analysis/smoke-20261025-141244`) found no other draw entry
  point. Native has no EDRAM limit and draws each call once, and the main menu matches Classic.
  The cursor-based draw attribution (`--ts4_d3d_census`, census.h) was not conclusive here:
  D3D moves to a new ring segment several times a frame, and spans across a move are not
  counted.
- **Register-mirror groups** logged by both copies are exactly WWE's `kMirrorGroups`, plus
  the booleans group (`0x4900` at `0x2780`, WWE's `kDeviceBooleans`).

## Native against Classic (2026-10-25)

`python scripts/scene_runner.py tests/scenes/boot-main-menu.toml --native 1`: two Classic
runs and one Native run. Each Native capture is judged per pixel against the lowest FLIP
error over the Classic runs, with the same tile rule. What it took past the title:

- **`k_CTX1` textures** (Swing's normal maps) have no Vulkan format. They are decoded to R8G8
  on upload (`src/native/texture_decode.h`, the SDK's `ConvertTexelCTX1ToR8G8` layout).
- **Rectangle lists** (primitive 8, Swing's mip-chain pass `0x88756788`) are drawn as triangle
  lists, the fourth corner made as v1 + v2 - v0 (`src/native/rect_list.h`). That pass also
  resolves into mip levels above 0. Resolve targets hold level 0 only, so such a resolve no
  longer overwrites level 0. Resolve targets now hold the fetch's mip levels and a resolve fills
  its level (see "Same-frame differences" below).
- **1D textures** are bound as width x 1 2D textures, and the shaders fetch them that way
  (`patches/xenosrecomp/0002-topspin4-inputs`).
- **Swing's DrawVertices** arguments (above).

Result (`analysis/scenes/boot-main-menu`): title frame 480 worst tile 0, mean FLIP 0.0000;
main menu frame 1400 worst tile 0, mean FLIP 0.0018. The session report shows Native active,
fallback `none`, no draw skips, no debug cvars and no Enhancements.

## In the match (2026-10-25)

`tests/scenes/exhibition-match.toml` (boot to an Exhibition match, captures at player select
and three in-match frames). Shaders created in the match were dumped on a Classic run and
added to the pack (330 shaders, all convert). The match first came out flat and dark on Native:
no sun, grey court lines (mean FLIP 0.54). RenderDoc captures of the same frame on both
renderers (`analysis/rdoc-match-classic`, `analysis/rdoc-match-native`) narrowed it down:

- Every texture input matched (shadow depth map, light maps, cube maps), and so did the pixel
  and vertex constants, once aligned with Classic's packed registers. A pixel history
  (`scripts/rdoc.ps1 query ... 'history <rt> X Y'`, new) showed the court's own draw
  producing the darker colour.
- **Normals.** Top Spin 4's normals, tangents and binormals are D3DDECLTYPE `0x2A2187`, Xenos
  `k_2_10_10_10` signed normalized. Native passed them as raw bits, and the shader read them
  as a float: a near-zero normal, so N·L was zero and the sun term vanished.
  `patches/xenosrecomp/0003-dec3n-normals` decodes them (`SPEC_CONSTANT_DEC3N_NORMAL`).
  Result: mean FLIP 0.54 to 0.048.
- **Skin weights.** USHORT4N blend weights (`0x1A205A`) keep their 16-bit halves swapped
  after the 32-bit vertex swap, as texcoords and positions do (WWE swapped those two only).
  The wrong bone dominated, and ball kids, umpire and the CPU player came out twisted.
  `patches/xenosrecomp/0004-blend-weight-halves` swaps them back (mask bit 8 + index).
- **Shader constants from a buffer** (`SetShaderConstantsFromBuffer`, Loader `0x830D4FE0`,
  Swing `0x8829DDB0`, about 180 calls a frame in the match) never reach the device's constant
  mirror. Native now copies each range into the mirror. This did not change the match frame,
  but without it any constant set only this way would be stale.

Result: player select passes (worst tile 0), and the in-match frames match Classic's sun,
shadows and lines. The remaining hot tiles (worst 50-107) sit on moving characters, the score
and the radar-gun readout. The match is not deterministic from run to run: one Classic run
was still in the walk-out cutscene at frame 3600, and Classic run against Classic run failed
every match frame. Native active, no fallback, no draw skips (`analysis/scenes/exhibition-match`).

## Same-frame differences (2026-10-25)

Dual runs (`--dual 1`) and RenderDoc captures taken with `--ts4_native_dual_compare` hold both
renderers' work for one frame, Native's draws first (blocks `uniformsN`), then Classic's
(`xe_*`). Classic draws its 2x MSAA match in two 1280x384 tiles into one 1280x1024 target, the
second tile shifted up by 384 rows. `scripts/rdoc/query.py` gained `mesh EID [N]` (post-VS
positions, all of them to CSV for N > 8), depth and raster state, sampler filters and
anisotropy, and both shaders' SPIR-V. What it found, frame by frame:

- **2x MSAA.** Swing draws the match pass at 2x (`RB_SURFACE_INFO` 14010500), as a tiled
  surface one tile (384 rows) tall. Native draws every call once, so an MSAA target is sized to
  the draws' viewport, resolves average its samples into a single-sample twin. Alpha to mask (`RB_COLORCONTROL`
  bit 4) was Vulkan alpha-to-coverage at first; it is now the emulated path's (below, "Other modes").
- **Mip chains rendered level by level.** The radar-gun display is a 256x64 render target whose
  mips the game renders and resolves one by one; Classic samples mips 2-4 at that size. Native's
  resolve targets held level 0 only (LOD clamped to 0), so the digits came out full bright.
- **Alpha-test function.** The second hair pass keeps fragments below the threshold
  (`patches/xenosrecomp/0005-alpha-test-function`). Post-VS positions of the hair and headband
  matched Classic's to 0.0003 NDC and 3e-5 depth, so the difference was not geometry.
- **Anisotropic filtering.** Classic filters linear, mipmapped textures at 4x
  (`anisotropic_override` defaults to 3); Native used the fetch's own setting (off). The court and
  small banner text were softer. `ts4_native_anisotropy` now defaults to -2, Classic's setting.
  Since image-quality ticket 02 both renderers take it from the `ts4_anisotropic_filtering`
  setting, whose default is the game's own filtering (the Oracle's `anisotropic_override` -1).

Result (`python scripts/scene_runner.py tests/scenes/exhibition-match.toml --native 1 --dual 1`):
every dual frame passes, worst tiles 0, 0, 0 and 18, mean FLIP 0.0022 at player select and 0.010
in the match (0.038 before the filtering fix). No fallback, no draw skips.

## Other modes (2026-10-25)

Scenes for the other 1.0 modes: `tests/scenes/career-start.toml` (Player Creator to the Career
main screen), `academy-lesson.toml` (the first Academy lesson) and `king-of-the-court.toml`
(two KotC matches). Their shaders were dumped on Classic runs and added to the pack (493).
What the Academy's trees took, found the same way (same-frame capture, `analysis/rdoc-academy-2350`):

- **Alpha to mask.** Xenia emulates it in the shader, with per-sample thresholds (2x: 0.5 and
  1.0, less a 2x2 dither offset from `RB_COLORCONTROL` bits 24-31). Vulkan's alpha-to-coverage
  is the driver's own pattern and thinned the leaves. `patches/xenosrecomp/0006-alpha-to-mask`
  writes `SV_Coverage` the emulated path's way (`g_AlphaToMask`, shared constants +572).
- **The fetch constant's LOD bias** (dword 4, 5 fractional bits). Swing sharpens foliage with
  -1 and -2; Native's samplers applied only the global bias, so the alpha-tested leaves sampled
  blurred mips and lost most of their alpha. The leaf textures, samplers and constants
  otherwise matched Classic's.

What King of the Court took (`analysis/rdoc-kotc-6400`):

- **POSITION1 had no input location.** XenosRecomp gives POSITION1 location 15, shared with
  TEXCOORD7; Native's `kInputLocations` knew only TEXCOORD7 there, so the first morph target of
  Top Spin 4's morph meshes (four extra POSITIONn/NORMALn streams) read zeros. The TV cameramen
  came out posed differently: 41% of their post-VS vertices up to 0.02 NDC off Classic's.

## Shadow map and texel offsets at scale (image-quality ticket 05, 2026-10-27)

Captures of Exhibition frame 3600 on Native at 1x and at `--ts4_resolution_scale=2`
(`analysis/scenes/iq05/rdoc-1x`, `rdoc-2x`; the draw order and EIDs are the same in both).

- **The shadow map is the 1040x1024 target.** Two depth-only passes draw into it, one per player
  (EIDs 655-948 at viewport 512,0 512x1024 and 1098-1247 at 0,0 512x1024). It is resolved
  (`resolve depth 1040x1024`, EID 1250) to a 1024x1024 R32_FLOAT texture, which the court and
  player draws sample as `ShadowDepthMapPlayer` (`SharedConstants + 44`). 1040 is 13 EDRAM tiles
  of 80 pixels: the 1024-wide map padded to the tile width. The colour half of the same target is
  resolved to a 1024x1024 RGBA8 at EID 1311. At 2x the target is 2080x2048 and the textures are
  2048x2048. The 512x512 R32_FLOAT the court samples is not a resolve and stays 512x512.
- **No converted shader has a fetch-instruction offset.** Every `tfetch*` call in the 493-shader
  pack (`cache/shader-native/hlsl`) has offset (0, 0), and nothing calls `getWeights2D` or
  `tfetch2DBicubic`. `patches/xenosrecomp/0007-texel-offsets-at-scale` therefore changes no
  sample in this game. Only its heap-index mask (`& 0x1FFFFFFF`) is live, matching the bits
  `textures.cpp` sets. It stays as Edge's `draw_resolution_scaled_texture_offsets` behaviour for
  any shader added to the pack later.
- **Blur and shadow taps are in UV, so they cover the same guest area at any scale.** No
  fragment shader of the post chain or the shadow receivers queries a texture's size.
  - Bloom: 1280x720 scene resolve -> 640x360 (EID 7925) -> 13-tap blur, vertical then horizontal
    (EIDs 7956, 7982, the same pixel shader) -> composite (EID 8008). The taps are
    `texcoord + k * c0.xy`, k = -6..6; c0 is (0, 1/512) and (1/512, 0) at 1x and at 2x. The blur
    target is 1280x720 at 2x.
  - Shadow filtering: the receiver that wrote the shadow-edge pixel (EID 5263) takes 16 taps at
    UV offsets that are literals in the shader (at most 0.0016, about 1.6 texels of the 1024 map).
    Its SPIR-V and every pixel constant it reads are identical at 1x and 2x.
- **No target needs pinning to 1x.** Every resolve target scaled with the frame (radar gun
  256x64 -> 512x128 with its mips, 160x128 -> 320x256, 640x360 -> 1280x720). Ticket 04's
  seven scenes at 2x and 3x had no failing tile.

## Open

- King of the Court frame 4300 (worst tile 42, passing): Classic draws a dark blob on the
  court by the serving player's foot, likely the ball's shadow during the toss, and Native does
  not. Not reproduced in a capture yet, because the serve's timing differs between runs.

- Of the `census` rows, only Swing's `Draw_88296A60` and `Draw_8828FF28` ran, once each, at
  boot. They need a renderer path only if a later scene uses them per frame.
- SetStreamSource has 34 callers in Loader but 11 in Swing, and may be inlined elsewhere in
  Swing. The renderer takes vertex strides from it.
- Loader's and Swing's devices may be alive at the same time, for example during the Loader's
  loading screen while Swing starts. WWE's renderer assumes one device, so this needs checking.
