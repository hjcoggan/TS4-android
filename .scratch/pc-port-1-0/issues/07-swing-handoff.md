# 07: Hand-off to Swing

**What to build:** Swing is built as its own host library. Loader loads it, then unloads itself while exiting its thread, and Swing's main loop starts.

**Blocked by:** 06 (Loader loads and runs)

**Status:** ready-for-agent

- [x] Swing is a module entry compiled to a separate host library
- [x] Loader's load of Swing and the unload-and-exit-thread hand-off work
- [x] Swing's own module load and unload calls are handled, and loading any module outside the Reference build is refused cleanly
- [x] The smoke run shows Swing's main loop running with no unregistered call targets on the boot path
- [x] Any ReXGlue fixes needed are numbered patches

## Comments

- 2026-10-24 (Claude): Swing is a `[[modules]]` entry built as `topspin4_Swing_DLL.dll` (47,081 functions; 5 seeds from analysis errors, 60 reviewed from the seed finder; CRT `setjmp`/`longjmp` at `0x8856B7F0`/`0x88569D30`). The hand-off in TU1 is simpler than the import list suggested: Loader calls Swing's entry on its own thread, and its only `XexUnloadImageAndExitThread` call sits in a wrapper (`sub_8340AF50`) that nothing calls or references, so Loader stays resident. The runtime's stub for it is never reached. Swing's `XexLoadImage` calls go through the runtime's module path; `patches/rexglue/0027-refuse-unrecompiled-modules.patch` makes any module without a recompiled library (a DLC replacement DLL, say) fail to load as if absent, with an error line, instead of faulting on its first call. Not exercised on the boot path yet. Smoke: Swing loads its archives, starts its worker threads and presents 1280×720 frames at 60 fps with no unregistered call targets, but the screen stays black after `XNetLogonGetTitleID` (stub), which is where ticket 08's title-server hooks come in. Rebuild isolation: touching Swing's generated code rebuilds only Swing.
