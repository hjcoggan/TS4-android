# 26: Native renderer boots to title

**What to build:** The Native renderer, ported from WWE, draws boot and title with D3D hooks generated for both Loader's and Swing's copy of D3D9.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene), 10 (Settings system and renderer switch)

**Status:** ready-for-agent

- [ ] The WWE Native renderer and its XenosRecomp patches are copied and adapted
- [ ] D3D hooks are generated for each Module's statically linked D3D9
- [ ] The boot-to-title and main-menu scenes pass FLIP against Classic
- [ ] The session report shows no draw skips or renderer fallbacks

## Comments

- 2026-10-25 (Claude): Plan agreed with the owner. Hooks are built into each Module's DLL, from one template and a per-Module address table, and reach the renderer through a small `topspin4_d3d_sink` shared library: a table of function pointers the exe fills. Steps: find the D3D functions; per-Module census hooks, checked against `--gpu_draw_census` on Classic; shader dump, the XenosRecomp patch and the shader pack; copy `src/native`; title (Loader), then main menu (Swing), each judged with FLIP against Classic.
- 2026-10-25 (Claude): Step 1 done, by static analysis only, no run yet. `symbols/d3d-functions.csv` names every function the renderer and the census hook in both copies. Method and evidence are in `docs/research/native-renderer.md` (`scripts/match_functions.py` and `scripts/scan_pm4.py`, both from WWE). All draw-packet builders lie inside the two D3D copies. The device offsets the renderer reads are the same as WWE's. Swing has no EndVertices (inlined into its two game callers, which are hooked instead) and no indexed user-memory draws. No box ticked: hooks are not generated yet.
