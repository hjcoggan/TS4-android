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
  device in r3, flags in r4 (bit 17 tested first), f1, and the destination in r8.

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
- **Swing's DRAW_INDX gap (110 per frame) is predicated tiling.** The GPU's IB history shows
  the changing IBs each executed 3 times (`2/3x6466w`, `5/6x9904w`): D3D replays the tiled
  pass once per EDRAM tile. With 3 tiles, 181 hooked draws, x of them in the pass, give
  181 + 2x = 291 for x = 55. That also accounts for the 6 extra IBs per frame. A sweep build
  (`scripts/gen_d3d_sweep.py`, `-DTOPSPIN4_D3D_SWEEP=ON`: 627 counted functions in
  `0x88286000`-`0x882AF000`, `analysis/smoke-20261025-141244`) found no other D3D function
  near 110 calls per frame. The Native renderer has no EDRAM limit and draws each call once.
  Native-against-Classic frame comparison is the check.
- **Register-mirror groups** logged by both copies are exactly WWE's `kMirrorGroups`, plus
  the booleans group (`0x4900` at `0x2780`, WWE's `kDeviceBooleans`).

## Open

- Of the `census` rows, only Swing's `Draw_88296A60` and `Draw_8828FF28` ran, once each, at
  boot. They need a renderer path only if a later scene uses them per frame.
- SetStreamSource has 34 callers in Loader but 11 in Swing, and may be inlined elsewhere in
  Swing. The renderer takes vertex strides from it.
- Loader's and Swing's devices may be alive at the same time, for example during the Loader's
  loading screen while Swing starts. WWE's renderer assumes one device, so this needs checking.
