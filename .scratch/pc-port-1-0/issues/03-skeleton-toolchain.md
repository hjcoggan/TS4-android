# 03: Skeleton and pinned toolchain bootstrap

**What to build:** A fresh clone bootstraps the pinned toolchain and builds an empty host executable. This is the prefactor everything else builds on (ADR-0003, ADR-0004).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Bootstrap fetches ReXGlue v0.10.0, LLVM, XenosRecomp and the other pinned tools, checking hashes
- [ ] Numbered patches in the repo are applied to the upstream sources in order, and a failing patch stops bootstrap with a clear message
- [ ] The WWE All Stars project skeleton (CMake presets, build script, runtime host) is copied in and renamed for Top Spin 4
- [ ] WWE runtime patches that apply to this game are carried over, and the ones that don't are listed with a reason
- [ ] Configure and build produce a host executable that starts and exits cleanly with no guest code
- [ ] Generated code and build output are git-ignored
