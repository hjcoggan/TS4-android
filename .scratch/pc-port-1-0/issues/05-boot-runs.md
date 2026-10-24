# 05: Boot runs

**What to build:** Boot (default.xex) generates, compiles and runs under ReXGlue up to the point where it loads Loader, and the smoke run reports what's missing.

**Blocked by:** 03 (Skeleton and pinned toolchain bootstrap)

**Status:** ready-for-agent

- [x] A manifest for Boot exists with the TU1 image as input
- [x] Boot generates and compiles without errors
- [x] The smoke run (copied from WWE) launches the build for a bounded time and reports crashes, unregistered call targets, missing jump-table entries and stalls
- [x] The smoke run shows Boot reaching its load of Loader

## Comments

- 2026-10-24 (Claude): `topspin4_manifest.toml` has Boot as the entrypoint; `scripts/build.ps1 -Action Codegen` checks the six SHA-256s (three disc XEX, three TU1 `.xexp`) and stages them in `assets/`, where codegen applies TU1 (log: base 0.0.0.3 → 0.0.1.3). Boot needed no seeds. `scripts/smoke.ps1` reports outcome, unregistered call targets, unresolved jump-table/branch targets, other FATALs, a stall (alive at the deadline with the log quiet ≥ `-StallSeconds`) and the TU patches loaded. Smoke: Boot applies TU1 to itself, loads `Loader_DLL.xex` through `XexLoadImage` with `update:\Loader_DLL.xexp`, then stops at Loader's `DllMain` (`833E0E80`, not in the function table; FATAL target `8348B068`). Noted for later: Boot opens `\Device\Image` and gets `0xC000000F`.
