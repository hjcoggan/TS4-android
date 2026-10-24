# 07: Hand-off to Swing

**What to build:** Swing is built as its own host library. Loader loads it, then unloads itself while exiting its thread, and Swing's main loop starts.

**Blocked by:** 06 (Loader loads and runs)

**Status:** ready-for-agent

- [ ] Swing is a module entry compiled to a separate host library
- [ ] Loader's load of Swing and the unload-and-exit-thread hand-off work
- [ ] Swing's own module load and unload calls are handled, and loading any module outside the Reference build is refused cleanly
- [ ] The smoke run shows Swing's main loop running with no unregistered call targets on the boot path
- [ ] Any ReXGlue fixes needed are numbered patches
