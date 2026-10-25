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

**Smoke: stalled (alive, log quiet).** Rerun with `-ScreenshotEvery 5 -StacksAt 20,25`: the
screenshots and `last_screenshot_black` in `result.json` show what is on screen, and
`stacks-<s>-guest.txt` lists each thread's guest functions; a thread whose stack is the same at
both times is the one waiting. Then `-LogLevel debug` and count the repeated kernel calls after
the last new log line. A static screen waiting for input also reads as stalled.

**Screen black (`last_screenshot_black`), but frames present and the game loads files.**
Rendering, not logic. Classic must
run on the FSI render-target path (`TopSpin4App` defaults it); the host render-target path draws
Swing's frames black. A run passing `--render_target_path_vulkan` overrides the default.

**An object renders black or wrong, the rest of the frame is fine.** Compare with the Oracle
by frame, one draw at a time:
1. The Oracle's frame: `pwsh scripts/edge.ps1 -Renderer vulkan -ShotsAt 25 -RenderDocAt 30`
   drives Edge on a virtual pad (`scripts/input/edge-boot-to-practice.txt`) and captures with
   RenderDoc. The port's: `pwsh scripts/rdoc.ps1 capture -Frame 1050`.
2. Find the object's draws in both captures by index count, then diff them in one replay each:
   `pwsh scripts/rdoc.ps1 query <capture> 'draws --indices 1999' 'draw <eid>' 'texture <id> --all-mips'`
   (bindings, view swizzles, samplers, constant blocks, the pixel shader's SPIR-V).
3. When inputs match and the output differs, look inside the shader: RenderDoc cannot step the
   port's shaders (FSI) or Edge's (barycentrics). `--dump_shaders=<dir>` gives each shader's Xenos
   disassembly; the probe (patch 0033) shows a register as the shader's colour, right after a
   chosen instruction: `--gpu_debug_ps_hash=<16 hex> --gpu_debug_ps_register=0
   --gpu_debug_ps_components=xzw --gpu_debug_ps_after=156`. Walk the chain back from the output
   until a value goes wrong.

A difference from Edge is a lead, not a cause, until you show the different branch runs for
this draw: decode the fetch constant or register state before blaming it. Edge's translator is a
newer fork, so most differences in its SPIR-V are plumbing. The black practice player was the
fetch exponent adjust read from the wrong fetch-constant dword (patch 0032). A translator change
needs no cache reset: `cache/shaders/shareable` holds guest ucode (`.xsh`) and pipeline
descriptions (`.xpso`), and shaders are translated again on every run.

**The whole frame is black, but the render targets hold the picture.** On host render targets
(the default; `--render_target_path_vulkan=fsi` is the other path), a resolve dumps whichever
render target owns the EDRAM tiles to the EDRAM buffer first, and copies that out to guest
memory. If a later pass's target is believed to cover those tiles, the resolve copies the wrong
image. Follow the chain in a capture with `actions <eid> <eid>`, which shows each dispatch's source
texture: the dump should read the colour target you saw with `rt`. This is how every Swing frame
resolved black: a depth-only occlusion-query pass at EDRAM base 720 (4x MSAA) was estimated at
full height, wrapped around the EDRAM and took the colour target's tiles. Running unclipped draws'
vertex shaders on the CPU for their real extent (`execute_unclipped_draw_vs_on_cpu`, set in
`src/topspin4_app.h`, as Edge does) fixed it.

**A runtime change seems to have no effect.** `topspin4_stage_dlls` (CMakeLists.txt) copies the
runtime and GPU plugin DLLs next to the exe on every build; check their timestamps there.

**Input does nothing.** XUI screens and the intro videos read keystrokes
(`XamInputGetKeystroke`), not the pad state. The scripted pad sends both
(`src/script_input.h`).

**The runtime itself needs a change.** Edit `.tools/rexglue-source`, run
`pwsh scripts/new-patch.ps1 <name>`, and add the patch to `patches/README.md`.
`pwsh scripts/apply-patches.ps1 -Reset` throws edits away.

## Where things are

- Scene runs: `python scripts/scene_runner.py tests/scenes/boot-main-menu.toml` plays a scene
  twice, dumps its capture frames (exactly the guest frame named, from the GPU's swap) and
  checks they repeat. One frame from a smoke run: `-ExtraArguments
  '--ts4_dump_frames=1400','--ts4_dump_dir=<dir>'` (`src/frame_dumps.h`). Smoke, scene and
  `rdoc.ps1 capture` runs share `analysis/game-run.lock`, so a second run is refused while one
  is going. Capture frames on steady screens: loading screens show a random tip each boot, and
  the practice ground's labels slide in and the player idles.
- The Oracle: Xenia Edge with TU1 installed (title bar `v0.0.1.3`; `scripts/edge.ps1` refuses
  anything else). It reads input only from a pad: `scripts/edge.ps1` plays input scripts on a
  ViGEm virtual pad (`winget install ViGEm.ViGEmBus`, `pip install vgamepad`), at 60 frames a
  second of wall clock.

- Names for Swing functions: `symbols/tu1-symbols.csv`, with the method in
  `docs/research/tu1-symbol-map.md`. Local Ghidra project of all three TU1 Modules:
  `ghidra/TopSpin4TU1` (built by `scripts/ghidra-project.ps1`).
- Base and TU1 memory images: `analysis/xex/*.pe` and `analysis/xex/tu1/*.pe` (XexTool output;
  `docs/research/module-inspection.md`).
- Prior art to copy (ADR-0004): WWE All Stars at `D:\Projects\WWE_ALLSTARS-RECOMP`
  (`scripts/`, `src/`, `launcher/`, `patches/`, `docs/research/`).
- The Oracle's log of a run that reaches the menu and the attract demo:
  `.tools/xenia-edge/xenia.log` (Edge's file opens and guest thread starts and exits, by frame).
  Diff the port's sequence against it to find where a run diverges: pass
  `-ExtraArguments '--io_trace=true'` to the smoke run for the port's opens and thread starts and exits. Rerun Edge
  (`.tools/xenia-edge/xenia_edge.exe`, set `log_level = 3` in its config) for more detail.
- The flags WWE's launcher passes, which a direct start of this port must also set:
  `PlumbingToSet` in `D:\Projects\WWE_ALLSTARS-RECOMP\src\startup_paths.cpp`.
- Ghidra MCP: open `ghidra/TopSpin4TU1` with program names `default.xex`, `Loader_DLL.xex`,
  `Swing_DLL.xex`.
- Debugger: `scripts\lldb.cmd` (the pinned lldb with its Python, from bootstrap). Run it from
  `cmd` or PowerShell, not bash, so `--x=D:/...` arguments survive. For a stall, attach to the
  running game and print every thread's stack in about a second; frames show the guest
  function and generated line (`__imp__sub_830AA4D8 at topspin4_recomp.54.cpp:275`):
  `scripts\lldb.cmd -b -p <pid> -k "thread backtrace all -c 8" -k detach`
  In batch mode the attach stop counts as a crash, so commands after it go in `-k`, not `-o`.
