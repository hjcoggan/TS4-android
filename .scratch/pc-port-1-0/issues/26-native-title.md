# 26: Native renderer boots to title

**What to build:** The Native renderer, ported from WWE, draws boot and title with D3D hooks generated for both Loader's and Swing's copy of D3D9.

**Blocked by:** 09 (Scene runner and boot-to-main-menu scene), 10 (Settings system and renderer switch)

**Status:** ready-for-agent

- [ ] The WWE Native renderer and its XenosRecomp patches are copied and adapted
- [ ] D3D hooks are generated for each Module's statically linked D3D9
- [ ] The boot-to-title and main-menu scenes pass FLIP against Classic
- [ ] The session report shows no draw skips or renderer fallbacks
