# 06: Loader loads and runs

**What to build:** Loader is built as its own host library, Boot loads it through ReXGlue's module path, and Loader's code runs.

**Blocked by:** 05 (Boot runs)

**Status:** resolved

- [x] Loader is a module entry in the manifest, compiled to a separate host library
- [x] Boot's load of Loader succeeds, and procedure lookup on Loader returns working addresses
- [x] Loader's image is writable where the game expects it to be
- [x] Any ReXGlue fixes needed are numbered patches in the repo
- [x] The smoke run shows Loader running its boot sequence, up to its load of Swing
- [x] Changing only Swing or Boot does not rebuild Loader

## Comments

- 2026-10-24 (Claude): Loader is a `[[modules]]` entry built as `topspin4_Loader_DLL.dll`; the runtime wires it on `XexLoadImage`, runs its `DllMain`, and procedure lookup works. Codegen needed 49 seeds (4 from analysis errors and runtime misses, 45 from `scripts/find_table_targets.py loader`, reviewed) plus Loader's CRT `setjmp`/`longjmp` (`0x8346A940`/`0x8346A510`): XUI's libjpeg recovers from errors with them, and without the mapping a `longjmp` left `r31` null in the JPEG marker reader. Smoke: the boot videos play (2K Czech logo at 60 fps), then Loader loads `Swing_DLL.xex` with its TU1 patch and stops at Swing's entry (`88562DB0`, not recompiled yet). No ReXGlue patch was needed so far. Image writability: nothing in the Loader path writes its own image yet; re-check when Swing runs. Rebuild isolation: touching Boot's generated code rebuilds Boot and relinks the exe only; the Swing half is checked in ticket 07. `src/fault_log.cpp` logs a symbolized host stack for unhandled access violations (a start on ticket 11).
- 2026-10-24 (Claude): Swing half checked in ticket 07: touching Swing's generated code rebuilds only `topspin4_Swing_DLL.dll`. No ReXGlue patch was needed for Loader itself.
- 2026-10-25 (Claude), ticket audit: ticked the writability box on a direct observation. Loader's `.data` (0x83520000-0x8354BAC4) and `.idata` lie inside its image (base 0x83000000, size 0x5D0000). In a headless smoke run to the title screen, the debugger read all 178,884 bytes of `.data` from guest memory at 5 s and at 18 s: 94 bytes changed, with no fault, so the game's writes to Loader's image land. All boxes are now ticked.
