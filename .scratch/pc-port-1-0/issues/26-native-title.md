# 26: Native renderer boots to title

**What to build:** The Native renderer, ported from WWE, draws boot and title with D3D hooks generated for both Loader's and Swing's copy of D3D9.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene), 10 (Settings system and renderer switch)

**Status:** ready-for-agent

- [ ] The WWE Native renderer and its XenosRecomp patches are copied and adapted
- [x] D3D hooks are generated for each Module's statically linked D3D9
- [ ] The boot-to-title and main-menu scenes pass FLIP against Classic
- [ ] The session report shows no draw skips or renderer fallbacks

## Comments

- 2026-10-25 (Claude): Plan agreed with the owner. Hooks are built into each Module's DLL, from one template and a per-Module address table, and reach the renderer through a small `topspin4_d3d_sink` shared library: a table of function pointers the exe fills. Steps: find the D3D functions; per-Module census hooks, checked against `--gpu_draw_census` on Classic; shader dump, the XenosRecomp patch and the shader pack; copy `src/native`; title (Loader), then main menu (Swing), each judged with FLIP against Classic.
- 2026-10-25 (Claude): Step 1 done, by static analysis only, no run yet. `symbols/d3d-functions.csv` names every function the renderer and the census hook in both copies. Method and evidence are in `docs/research/native-renderer.md` (`scripts/match_functions.py` and `scripts/scan_pm4.py`, both from WWE). All draw-packet builders lie inside the two D3D copies. The device offsets the renderer reads are the same as WWE's. Swing has no EndVertices (inlined into its two game callers, which are hooked instead) and no indexed user-memory draws. No box ticked: hooks are not generated yet.
- 2026-10-25 (Claude): Step 2. Hooks are generated per Module at configure time (`cmake/d3d_hooks.cmake`) from `symbols/d3d-functions.csv` into each Module's DLL (`src/d3d/hooks.h`). They reach the exe through `topspin4_d3d_sink.dll` (`src/d3d/sink.h`). `--ts4_d3d_census` logs each Module's counts every 60 of its presents. Headless Classic run to the main menu with `--gpu_draw_census` (`analysis/smoke-20261025-141001`): Loader's title is fully covered (GPU 240 DRAW_INDX = 240 BeginVertices, 120 DRAW_INDX_2 = ClearRect + Resolve, IBs equal). On Swing's main menu, DRAW_INDX_2 matches once D3D's 48 internal point-list draws are left out. The DRAW_INDX gap (110 per frame) is predicated tiling: the GPU's IB history shows the changing IBs executed 3 times each. A sweep build counting all 627 other D3D functions found no unhooked draw entry point (`analysis/smoke-20261025-141244`). Register-mirror groups in both copies equal WWE's. Box ticked: hooks are generated for both copies and observed running in both. Details are in `docs/research/native-renderer.md`.
