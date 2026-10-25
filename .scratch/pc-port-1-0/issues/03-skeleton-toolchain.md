# 03: Skeleton and pinned toolchain bootstrap

**What to build:** A fresh clone bootstraps the pinned toolchain and builds an empty host executable. This is the prefactor everything else builds on (ADR-0003, ADR-0004).

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Bootstrap fetches ReXGlue v0.10.0, LLVM, XenosRecomp and the other pinned tools, checking hashes
- [x] Numbered patches in the repo are applied to the upstream sources in order, and a failing patch stops bootstrap with a clear message
- [x] The WWE All Stars project skeleton (CMake presets, build script, runtime host) is copied in and renamed for Top Spin 4
- [x] WWE runtime patches that apply to this game are carried over, and the ones that don't are listed with a reason
- [x] Configure and build produce a host executable that starts and exits cleanly with no guest code
- [x] Generated code and build output are git-ignored

## Comments

- 2026-10-24 (Claude): Bootstrap applies `patches/rexglue/` through `scripts/apply-patches.ps1` (stamped; a failing patch resets the tree and names the patch). 26 of WWE's 29 runtime patches carried over; the 3 left out, and the deferred XenosRecomp patch, are listed with reasons in `patches/README.md`. The ReXGlue `init` boilerplate lives in `cmake/rexglue.cmake` rather than `generated/`, so `generated/` stays entirely ignored. Without codegen output the host prints a notice and exits 0. FidelityFX is reported OFF at configure: the `.tools/fidelityfx-sdk` junction has no `sdk/include` yet. Not needed before the Native renderer.
