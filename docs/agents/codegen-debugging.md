# Codegen debugging

What to do when the smoke run (`pwsh scripts/smoke.ps1`) or codegen reports a problem. The loop:
smoke → read `result.json` and `runtime.log` in its `analysis/smoke-*` folder → fix (a seed, a
manifest mapping, a hook, a runtime patch) → `pwsh scripts/build.ps1 -Action Codegen` when the
manifest changed, then `-Action Build` → smoke again. The smoke run's scripted pad
(`scripts/input/boot-to-title.txt`) skips the intro and keeps real controllers out.

## By symptom

**Codegen: "target not in any function", or smoke: an unregistered call target `X`.**
Disassemble around it: `python scripts/disasm.py <X-16 hex> 12` (the Module is picked by address).
- `X` starts a function discovery missed: a one-instruction tail stub, a this-adjusting thunk, code
  right after a `bctr` or `blr`, a shared `li r3,0; blr` tail. Seed it as `0xX = {}` in the Module's
  `functions` table in `topspin4_manifest.toml`, with a comment giving the evidence.
- `X` is a return address: the instruction before it is a `bl` to a function that never returns
  (it ends in `ExTerminateThread`, say). The code at `X` is a separate function, usually reached
  through a pointer built with `lis`/`addi`. Seed `X`.
- Before chasing misses one at a time, run `python scripts/find_table_targets.py <boot|loader|swing>`.
  It lists entries found in `.pdata`, built by `lis`/`addi`, or stored next to known functions.
  Review its output and leave out SEH filter and handler blocks (they read `r31`, test an
  exception code, or start with `mr r8,r8`), and fragments that branch back into a larger function.

**Smoke: an access violation in guest code.** `src/fault_log.cpp` logs the host stack with each
frame's generated file and line. Read the generated code there and the PowerPC beside it.
- A callee-saved register (`r31` and up) reads null after calls: a `longjmp` ran through code the
  recompiler treats as a normal call. Find the Module's CRT `setjmp`: it stores f14-f31 and
  r13-r31 at `r3`, so search for `std r14,160(r3)`, and `longjmp` loads them back with
  `ld r14,160(...)`. Set `setjmp_address` and `longjmp_address` in that Module's manifest entry.
  Loader and Swing already have theirs.

**Smoke: stalled (alive, log quiet).** Take screenshots (`-ScreenshotEvery 5`), rerun with
`-LogLevel debug`, and count the repeated kernel calls after the last new log line. A static
screen waiting for input also reads as stalled.

**Input does nothing.** XUI screens and the intro videos read keystrokes
(`XamInputGetKeystroke`), not the pad state. The scripted pad sends both
(`src/script_input.h`).

**The runtime itself needs a change.** Edit `.tools/rexglue-source`, run
`pwsh scripts/new-patch.ps1 <name>`, and add the patch to `patches/README.md`.
`pwsh scripts/apply-patches.ps1 -Reset` throws edits away.

## Where things are

- Names for Swing functions: `symbols/tu1-symbols.csv`, with the method in
  `docs/research/tu1-symbol-map.md`. Local Ghidra project of all three TU1 Modules:
  `ghidra/TopSpin4TU1` (built by `scripts/ghidra-project.ps1`).
- Base and TU1 memory images: `analysis/xex/*.pe` and `analysis/xex/tu1/*.pe` (XexTool output;
  `docs/research/module-inspection.md`).
- Prior art to copy (ADR-0004): WWE All Stars at `D:\Projects\WWE_ALLSTARS-RECOMP`
  (`scripts/`, `src/`, `launcher/`, `patches/`, `docs/research/`).
- Debugger: `scripts\lldb.cmd` (the pinned lldb with its Python, from bootstrap). Run it from
  `cmd` or PowerShell, not bash, so `--x=D:/...` arguments survive. For a stall, attach to the
  running game and print every thread's stack in about a second; frames show the guest
  function and generated line (`__imp__sub_830AA4D8 at topspin4_recomp.54.cpp:275`):
  `scripts\lldb.cmd -b -p <pid> -k "thread backtrace all -c 8" -k detach`
  In batch mode the attach stop counts as a crash, so commands after it go in `-k`, not `-o`.
