# 05: Boot runs

**What to build:** Boot (default.xex) generates, compiles and runs under ReXGlue up to the point where it loads Loader, and the smoke run reports what's missing.

**Blocked by:** 03 (Skeleton and pinned toolchain bootstrap)

**Status:** ready-for-agent

- [ ] A manifest for Boot exists with the TU1 image as input
- [ ] Boot generates and compiles without errors
- [ ] The smoke run (copied from WWE) launches the build for a bounded time and reports crashes, unregistered call targets, missing jump-table entries and stalls
- [ ] The smoke run shows Boot reaching its load of Loader
