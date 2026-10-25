# What the recomp and emulation community already has on our open problems

Research date: 2026-10-25. This is a desk study that builds on
[feasibility.md](feasibility.md). It answers the six questions from tickets 13
and 16 with published code, issues and PRs. Nothing was built or run. Upstream
source files were read as text at these heads:

- Xenia Edge `edge` at `b7a2637`
- Xenia Canary `canary_experimental` at `451f915`
- Xenia master
- ReXGlue `development` at `bd833a2`
- UnleashedRecomp `main` at `cf829a9`

The vendored tree `.tools/rexglue-source` is v0.10.0 (`f5337cd`) plus
`patches/rexglue/0001`–`0033`. **[community]** marks forum or relayed claims,
and **[inferred]** marks my own reasoning. Every other claim has a URL or a
local path.

## Summary: actionable leads, ranked by likely payoff

1. **The `0xC0100100` vertex-buffer failure has a published fix, and Edge
   already ships it.** Edge commit
   [`2fce2d8`](https://github.com/has207/xenia-edge/commit/2fce2d89577d676587fe2d7d887c096d127cc119)
   ("Mask guest fetch addresses to physical", 2026-08-13) masks the
   vertex-fetch, index-buffer and memexport addresses to physical
   (`& 0x1FFFFFFF`), both on the CPU side and in the shaders. Its commit message
   describes our symptom: "a mirror window address (0xC0000000 etc.) was
   rejected as out of bounds and the draw dropped." Canary did the same in
   [PR #1180](https://github.com/xenia-canary/xenia-canary/pull/1180) (XPS
   support). ReXGlue v0.10.0 and `development` mask none of these. The fix is a
   port of about eight small hunks (§1).
2. **Top Spin 4 uses XPS, and that is probably where `0xC0100100` comes from
   [inferred].** Canary labels the game `gpu-xps` (added 2026-08-27 by the
   tester of the XPS PR,
   [#358 timeline](https://github.com/xenia-canary/game-compatibility/issues/358)).
   Both `Loader_DLL` and `Swing_DLL` import `KeLockL2`/`KeUnlockL2` (the
   Oracle's import listing, `.tools/xenia-edge/xenia.log` lines 1455–1457 and
   2997–2998). Masking alone may leave stale geometry, because ReXGlue does not
   write-watch the `0x7F000000` window through which the CPU writes XPS data.
   Canary made that window a watched `PhysicalHeap` for this reason (§1).
3. **Retire ReXGlue's vertex-buffer residency cache.** It skips
   `RequestRange` for a fetch slot whose address and size have not changed, and
   it survives across frames, so buffers rewritten in place stay stale
   ([rexglue #510](https://github.com/rexglue/rexglue-sdk/issues/510), open).
   Edge deleted the same cache in
   [`c2674b1`](https://github.com/has207/xenia-edge/commit/c2674b19d2) (2026-05-15).
   It interacts with both the masking fix and XPS.
4. **The Oracle runs a guest scheduler, and ReXGlue does not.** Edge runs guest
   threads as cooperative fibers with a 1 ms quantum by default
   ([`34357e2`](https://github.com/has207/xenia-edge/commit/34357e2578), 2026-08-01).
   The owner's Edge config has `guest_scheduler = true`
   (`.tools/xenia-edge/xenia-edge.config.toml` line 530). Threading differences
   from the Oracle (Havok, worker pools, XPS producers) should be judged with
   that in mind (§3).
5. **Pick up ReXGlue fixes made after v0.10.0:**
   - lifted `sync`/`lwsync`/`eieio` fences
     ([#462](https://github.com/rexglue/rexglue-sdk/issues/462) and
     [#463](https://github.com/rexglue/rexglue-sdk/pull/463), commit `bd833a2`);
   - `stwcx.` as an atomic CAS ([#450](https://github.com/rexglue/rexglue-sdk/pull/450));
   - the AMD Windows `vkWaitForFences` failure above about 64 fences
     ([#521](https://github.com/rexglue/rexglue-sdk/issues/521)), which matters
     on the owner's RX 7900 XT.
6. **XMA:** Edge has at least four decoder fixes since August that ReXGlue lacks
   (sub-stream skip chain, split frame headers, `loop_start` alignment, sample
   rounding) (§5).
7. **Our host-render-target fix matches open upstream reports.** ReXGlue
   [#513](https://github.com/rexglue/rexglue-sdk/pull/513) (open PR) makes
   `execute_unclipped_draw_vs_on_cpu=true` the default, for exactly the
   wrap-around we found. [#508](https://github.com/rexglue/rexglue-sdk/issues/508)
   is the same black-front-buffer symptom, in NFL Head Coach 09 (§6).
8. **Nothing published is Top Spin 4 specific** beyond the compatibility
   reports already in feasibility.md. There is no game patch (no TOML for
   `54540859` in `xenia-canary/game-patches`), no Edge issue, and no ReXGlue
   issue (§2).

---

## 1. `Failed to request vertex buffer at 0x-3FEFFF00 (size 131072)`

### What the log line means

- **The negative number is a printing artefact.**
  - `xe_gpu_vertex_fetch_t::address` is a 30-bit `uint32_t` bitfield that holds
    a dword address
    (`.tools/rexglue-source/include/rex/graphics/xenos.h:1104-1116`).
  - `vfetch_constant.address << 2` promotes the bitfield to `int` before the
    shift, so `{:08X}` prints a negative value
    (`src/graphics/vulkan/command_processor.cpp:4092-4096`) **[inferred, C++
    integer promotion]**.
  - The value passed to `RequestRange` is `0xC0100100`.
- **Why it fails:** `SharedMemory::RequestRanges` returns false when
  `start > kBufferSize` (`src/graphics/shared_memory.cpp:353-355`), and
  `kBufferSize` is `1 << 29`, 512 MB
  (`include/rex/graphics/shared_memory.h:28-29`). The draw is then dropped:
  `IssueDraw` returns false, and the command processor logs
  `PM4_DRAW_INDX_2 ... Failed in backend`.
- **Where `0xC0100100` points:** it lies inside ReXGlue's `vC0000000` heap,
  which is a view of the same physical backing at offset 0
  (`src/system/xmemory.cpp:199-200` and `:408-411`, and the wiki's
  [Memory](https://github.com/rexglue/rexglue-sdk/wiki/Memory) page: "different
  views of the same physical backing"). Masked, it is physical `0x00100100`,
  which is exactly what the data would be **[inferred]**.
  - Caveat for any future case in the `0xE0000000` range: that view is offset by
    `0x1000` on Windows (wiki Memory, "PhysicalHeap 4K ... `0x1000`"). A plain
    `& 0x1FFFFFFF` is right for `0xA0`/`0xC0` addresses only. Edge has the same
    limitation **[inferred]**.

### What each emulator does

| | Vertex fetch, CPU side | Vertex fetch, shader | Index base (`VGT_DMA_BASE`) | Memexport |
|---|---|---|---|---|
| Xenia master | unmasked: `RequestRange(vfetch_constant.address << 2, ...)` ([vulkan_command_processor.cc](https://github.com/xenia-project/xenia/blob/master/src/xenia/gpu/vulkan/vulkan_command_processor.cc) ~L2508) | unmasked | unmasked | unmasked |
| Canary | `vfetch_constant.address & 0x07FFFFFF` (dwords) ([vulkan_command_processor.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/gpu/vulkan/vulkan_command_processor.cc) L2944-2945; D3D12 L2999-3000) | `& 0x07FFFFFF` ([spirv_shader_translator_fetch.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/gpu/spirv_shader_translator_fetch.cc) ~L95-102), DXBC `& 0x1FFFFFFC`, interpreter too | `CpuToGpu()` when the PM4 packet is read | not masked in #1180 |
| **Edge** (Oracle) | `xenos::CpuToGpu(vfetch_constant.address << 2) >> 2` with the comment "Mask to physical like the shader - the guest may use a mirror window." ([vulkan_command_processor.cc](https://github.com/has207/xenia-edge/blob/edge/src/xenia/gpu/vulkan/vulkan_command_processor.cc) L4115-4117; D3D12 L3061; Metal) | `& (0x1FFFFFFF >> 2)` ([spirv_shader_translator_fetch.cc](https://github.com/has207/xenia-edge/blob/edge/src/xenia/gpu/spirv_shader_translator_fetch.cc) L116-124) | `CpuToGpu(regs[VGT_DMA_BASE])` in `primitive_processor.cc` (both paths) | `CpuToGpu` in `draw_util.cc AddMemExportRanges` and `& 0x1FFFFFFF` in `spirv_shader_translator_memexport.cc` |
| **ReXGlue v0.10.0 and `development`** | unmasked (`src/graphics/vulkan/command_processor.cpp:4092`; D3D12 `:2514`; [`development`](https://github.com/rexglue/rexglue-sdk/blob/development/src/graphics/vulkan/command_processor.cpp) L4017) | unmasked: plain `>> 2` (`src/graphics/pipeline/shader/spirv_translator_fetch.cpp:70-72`) | unmasked (`src/graphics/primitive_processor.cpp:480`, `:618`) | unmasked |

`CpuToGpu` is `p & 0x1FFFFFFF` in all three Xenias
([Edge xenos.h L1161](https://github.com/has207/xenia-edge/blob/edge/src/xenia/gpu/xenos.h)).
Texture fetches in ReXGlue are already masked
(`src/graphics/pipeline/texture/cache.cpp:325`).

**The fix to port** is Edge
[`2fce2d8`](https://github.com/has207/xenia-edge/commit/2fce2d89577d676587fe2d7d887c096d127cc119),
by Herman S., 2026-08-13. It touches 8 files, about 30 lines:

- `d3d12_command_processor.cc`, `vulkan_command_processor.cc` and
  `metal_command_processor.cc`: mask the vertex-fetch address;
- `primitive_processor.cc`: mask the index base in both the 2 paths;
- `draw_util.cc`: mask memexport ranges;
- `spirv_shader_translator_fetch.cc` and `_memexport.cc`: mask in the shaders;
- bump `SpirvShaderTranslator::kVersion` so cached shaders are rebuilt.

Its message reads: "Vertex fetch, index buffer and memexport addresses reached
shared memory unmasked, so a mirror window address (0xC0000000 etc.) was
rejected as out of bounds and the draw dropped. Mask on the CPU side and in the
shaders, which derive the addresses independently." The DXBC translator also
needs Canary's `0x1FFFFFFC` mask if D3D12 is ever built (it is not, per
ADR-0005).

**The CPU-side mask alone is not enough** **[inferred]**. With only that,
`RequestRange` succeeds, but the shader still indexes the shared-memory buffer
at `0xC0100100 >> 2` dwords, beyond its 512 MB. Robust buffer access then
returns zeros, or the read is undefined. Both halves have to land together, as
in Edge.

**The Oracle shows no such errors.** The Oracle's log
(`.tools/xenia-edge/xenia.log`, build `edge@4d0720457`, 2026-10-24, which
contains `2fce2d8`) has zero `Failed to request vertex buffer` lines and zero
`Failed in backend` lines.

### Why the address is `0xC…`: XPS

- The label `gpu-xps` means "Title uses Xbox Procedural Synthesis for GPU access
  to L2 memory"
  ([label](https://github.com/xenia-canary/game-compatibility/labels/gpu-xps)).
  It went onto [#358](https://github.com/xenia-canary/game-compatibility/issues/358)
  on 2026-08-27, the day DanielCornflake posted test results on the XPS PR
  [#1180](https://github.com/xenia-canary/xenia-canary/pull/1180).
- The Canary commit
  [`2270830`](https://github.com/xenia-canary/xenia-canary/commit/22708301ba76d10aae6f7d7caac8b1cac9e4a8e6)
  (goldislead, 2026-08-30) "makes 0x7F000000 a real physical heap and the L2
  address range gets wrapped to the backing in vfetch, index buffers, and
  translators". It does four things:
  - adds `heaps_.v7F000000` as a `PhysicalHeap` over `0x7F000000–0x7FC7FFFF`,
    parented to physical, "so at least the write watches actually fire" (PR
    text);
  - changes the comment to "0xC... is physical, 0x7F... is virtual. Overlaid.";
  - makes `KeLockL2` return success: "Since the L2 cache isn't emulated, the XPS
    range is always backed";
  - masks fetch addresses in the command processors, the translators and the
    interpreter.
- **ReXGlue already aliases `0x7F000000` to physical 0, but does not watch it.**
  Its view table maps `0x7F000000–0x7FFFFFFF` to file offset `0x100000000`, the
  physical base (`src/system/xmemory.cpp:272-276`). So a CPU store to
  `0x7F100100` lands at physical `0x00100100`, where the masked GPU address
  reads. But `LookupHeap` returns `nullptr` for that range
  (`src/system/xmemory.cpp:400-403`), so no page protection or write watch
  covers it.
- **Consequence [inferred]:** XPS geometry written through `0x7F…` never
  invalidates shared memory. Once masked, the port could draw stale vertex data.
  This gets worse with #510 (below). The cure is Canary's `v7F000000` physical
  heap.
- `KeLockL2` in ReXGlue is still `// TODO; return 0;`
  (`src/kernel/xboxkrnl/xboxkrnl_memory.cpp:636-639`). That is the same value
  Canary returns, so it needs no change.
- **[inferred]** The 128 KB size (131072) fits an L2-locked XPS ring, but I
  found no source that gives Top Spin 4's XPS buffer size. A breakpoint on the
  game's `KeLockL2` call would confirm it.

### The vertex-buffer residency cache (related, also open upstream)

- ReXGlue's `IssueDraw` skips `RequestRange` when the slot's `VertexBufferState`
  address and size match the previous request
  (`src/graphics/vulkan/command_processor.cpp:4087-4091`). That state survives
  across frames, so a buffer the CPU rewrites in place is never re-uploaded
  ([#510](https://github.com/rexglue/rexglue-sdk/issues/510), Oblivion; open,
  no PR).
- This cache came from Edge, and Edge removed it on 2026-05-15:
  [`c2674b1`](https://github.com/has207/xenia-edge/commit/c2674b19d2), "Drop
  vertex buffer residency cache, hoist global lock like D3D12". Edge now calls
  `RequestRange` for every fetch under one hoisted critical-region acquire,
  noting that "SharedMemory tracks dirty pages and only uploads what actually
  changed, making redundant calls cheap".
- When porting the mask, either drop the cache as Edge did, or at least compare
  masked addresses. An XPS ring at a fixed address is exactly the "same
  address, same size" case **[inferred]**.

---

## 2. Top Spin 4 on Xenia: issues, config, patches

- **Compatibility:** nothing new since feasibility.md. Canary
  [#358](https://github.com/xenia-canary/game-compatibility/issues/358) has the
  labels `state-gameplay`, `marketplace-delisted`, `tech-middleware-havok`,
  `vsync-off-speedup` (2026-07-20) and `gpu-xps` (2026-08-27). Its comments
  report:
  - 2025-01-04: cutscene speed-up, and a crash at 1440p;
  - 2026-07-20: a full match on default settings, upscaling crashes ("graphics
    device lost"), and frame rates above 60 change game speed.

  Xenia master [#867](https://github.com/xenia-project/game-compatibility/issues/867)
  is unchanged. Edge's bundled database mirrors #867
  ([master.json](https://github.com/has207/xenia-edge/blob/edge/assets/game-compatibility/master.json)).
- **Game patches:** none. The tree of
  [xenia-canary/game-patches](https://github.com/xenia-canary/game-patches/tree/main/patches)
  has no `54540859` file (2K titles `545407xx`/`545408xx` exist, Top Spin
  doesn't). Issue search for "top spin" and "54540859" there returned nothing.
  So there is no community 60 fps or resolution patch to borrow.
- **Per-title code or config:** none. A code search for `54540859` in Edge,
  Canary and ReXGlue returned no hits, and no Edge or Canary commit message
  mentions Top Spin.
- **The relevant Oracle settings** (owner's `.tools/xenia-edge/xenia-edge.config.toml`):

  | Line | Setting |
  |---|---|
  | 340 | `render_target_path = "performance"` |
  | 390 | `execute_unclipped_draw_vs_on_cpu = true` |
  | 530 | `guest_scheduler = true` |
  | 6, 11 | `use_dedicated_xma_thread = false`, `xma_decoder = "new"` |
  | 296 | `guest_display_refresh_cap = true` |
  | 287 | `framerate_limit = 0` |

  These are the settings to match when comparing.

---

## 3. Threading in Xbox 360 recomps

**Havok and OpenMP-style worker pools:** I found nothing specific. Issue
searches for "Havok" and "OpenMP" in ReXGlue, XenonRecomp, UnleashedRecomp,
Canary and Edge found no threading report (the one UnleashedRecomp hit,
[#1369](https://github.com/hedge-dev/UnleashedRecomp/issues/1369), is a
collision cheat). The general primitives below are what those pools depend on.

### ReXGlue fixes and reports after v0.10.0

| Item | What | Status | Relevance |
|---|---|---|---|
| [#462](https://github.com/rexglue/rexglue-sdk/issues/462) and [#463](https://github.com/rexglue/rexglue-sdk/pull/463), commit `bd833a2` | `sync`/`lwsync`/`eieio` were lifted as nothing. Our tree still has "x86 has strong ordering so this is a no-op" (`src/codegen/builders/system.cpp:33-49`). The fix emits `atomic_thread_fence` (seq_cst/acq_rel/release). On x86 only `sync` becomes `mfence`; the others become compiler barriers. Without them, "the compiler may move the lifted loads and stores across a barrier that emits nothing" | in `development`, not in a release | **High** for Havok and job queues: lock-free publish patterns are exactly `lwsync; stw` |
| [#450](https://github.com/rexglue/rexglue-sdk/pull/450), commit `7f7c92e` | `stwcx.` becomes `std::atomic::compare_exchange_strong` (acq_rel), replacing `__sync_bool_compare_and_swap` (our tree: `src/codegen/builders/helpers.h:308`) | merged 2026-09-29 | Low on x86 (both are full CAS); take it with #463 |
| [#531](https://github.com/rexglue/rexglue-sdk/pull/531) | POSIX events lose wakeups in `SignalAndWait` and in set-then-reset pulses (Tekken 6 hangs). Guest condition variables are built as "mutex plus manual-reset event with set-then-reset notification" | open | POSIX only; matters for Steam Deck under Proton only if a native Linux build is made. Windows uses `threading_win.cpp` |
| [#500](https://github.com/rexglue/rexglue-sdk/issues/500) and [#501](https://github.com/rexglue/rexglue-sdk/pull/501) | `WaitMultiple` polls every 1 ms; the audio worker uses about 75% of a core | open, POSIX only | as above |
| [#476](https://github.com/rexglue/rexglue-sdk/issues/476) and [#459](https://github.com/rexglue/rexglue-sdk/pull/459) | `XHostThread` (audio worker callbacks) runs guest code without `fpscr.InitHost()`, so MXCSR is zeroed and FP exceptions are unmasked (SIGFPE, Kameo) | open | **Check:** our `xthread.cpp:651` calls `InitHost` for `XThread` only. XAudio callbacks into Swing run on the host audio thread |
| [#408](https://github.com/rexglue/rexglue-sdk/issues/408) | Xenia's 10 ms start-up sleep in `XThread::Execute` is missing | closed by the maintainer: removed on purpose, following Canary [`67d8095`](https://github.com/xenia-canary/xenia-canary/commit/67d80958c9d1ff926b9098be7f830c1937921d9a) | If a worker races its creator, this is the known knob |
| [#495](https://github.com/rexglue/rexglue-sdk/issues/495) and [#497](https://github.com/rexglue/rexglue-sdk/pull/497) | `XMPGetPlaybackController` sleeps 10 ms off the main thread and caps Halo: Reach at 45 fps | open | Already fixed locally by `patches/rexglue/0003-xmp-no-delay.patch` (`src/kernel/xam/apps/xmp_app.cpp:395-413`) |

### The Oracle's scheduler differs from the port's

- Since 2026-08-01, Edge runs guest threads "as cooperative fibers driven by an
  in-kernel scheduler instead of mapping each to its own host OS thread"
  ([`34357e2`](https://github.com/has207/xenia-edge/commit/34357e2578),
  `guest_scheduler` default `true`, `guest_scheduler_quantum_us = 1000`).
- About 40 kernel commits since then tune it:
  - FIFO mutant handoff;
  - auto-reset events consumed only by the front waiter
    ([`fb53ac2`](https://github.com/has207/xenia-edge/commit/fb53ac24ce));
  - event pulses delivered to parked waiters;
  - timers expired on a 1 ms tick
    ([`54729c2`](https://github.com/has207/xenia-edge/commit/54729c29fc));
  - an opt-in spin-starvation hack (`guest_scheduler_spin_quanta`,
    [`83abd43`](https://github.com/has207/xenia-edge/commit/83abd43576)).

  The full list is in the Edge log under `src/xenia/kernel`.
- **Consequence [inferred]:** on the Oracle a spin-waiting guest thread costs a
  quantum on one of six virtual CPUs and then yields at a safepoint. In the port
  it burns a host core and relies on the OS scheduler. Spin-wait hitches seen in
  the port but not in Edge (ticket 16) can come from this difference, not from a
  game bug. The `guest_scheduler_stats` line on Edge (config line 533) can show
  which threads spin there.
- ReXGlue drops `db16cyc` and `cctpl` (the Xenon spin and priority hints) as
  no-ops (`src/codegen/builders/system.cpp:51-60`). Edge gives arm64 a real spin
  hint ([`7ea10c4`](https://github.com/has207/xenia-edge/commit/7ea10c4f2d));
  x64 Edge uses its JIT. A mid-asm hook that turns `db16cyc` into `_mm_pause`
  in hot spin loops is the usual recomp answer **[inferred]**.

### Timing

- **`KeQueryPerformanceFrequency`:** the value differs between projects.

  | Project | Value | Source |
  |---|---|---|
  | ReXGlue | `guest_tick_frequency` = 50,000,000 | `src/system/runtime.cpp:108`, `src/kernel/xboxkrnl/xboxkrnl_threading.cpp:371-374` |
  | Canary, Edge | 50,000,000 | `emulator.cc` L211 and L326 |
  | UnleashedRecomp | 49,875,000, the console's real timebase | [`kernel/imports.cpp` L777-779](https://github.com/hedge-dev/UnleashedRecomp/blob/main/UnleashedRecomp/kernel/imports.cpp#L777-L779) |

  The port matches the Oracle, so no change.
- **`KeDelayExecutionThread`:**
  - ReXGlue converts the interval to whole milliseconds (`-ticks / 10000`). A
    zero delay yields, or sleeps 100 µs at low priority. Otherwise it calls
    `Sleep(ms)` (`src/system/xthread.cpp:1015-1047`), and the Windows backend
    uses a high-resolution waitable timer (`src/core/threading_win.cpp:106`).
  - Sub-millisecond guest delays therefore round down to 0 **[inferred]**.
  - UnleashedRecomp does the same rounding and returns `STATUS_USER_APC` at once
    on alertable waits ([imports.cpp L564-582](https://github.com/hedge-dev/UnleashedRecomp/blob/main/UnleashedRecomp/kernel/imports.cpp#L564-L582)).
  - Edge fixed a null-interval dereference
    ([`50e9c71`](https://github.com/has207/xenia-edge/commit/50e9c715df)).
- **UnleashedRecomp's lock replacements:**
  - `RtlEnterCriticalSection` becomes a CAS on `OwningThread` plus
    `std::atomic::wait`, and `KeAcquireSpinLockAtRaisedIrql` becomes a CAS loop
    with `std::this_thread::yield()`
    ([imports.cpp L691-708, L810-822](https://github.com/hedge-dev/UnleashedRecomp/blob/main/UnleashedRecomp/kernel/imports.cpp#L691-L708)).
  - The `atomic::wait` version later deadlocked during video playback
    ([UnleashedRecomp #424](https://github.com/hedge-dev/UnleashedRecomp/issues/424),
    open), a caution against copying it.

---

## 4. Frame pacing and locking to 60

- **UnleashedRecomp's limiter:**
  - It runs at present time and sleeps
    `floor_ms(next - now - 2 ms)`, then spin-yields until the deadline. On a
    miss it resets the deadline to now, and the deadline advances by `1/FPS`
    ([`gpu/video.cpp` L2856-2876](https://github.com/hedge-dev/UnleashedRecomp/blob/main/UnleashedRecomp/gpu/video.cpp#L2856-L2876)).
  - The game's own vsync wait (`ADXM_WaitVsync`) is replaced by
    `sleep_until` on the next 1/60 s boundary
    ([`patches/fps_patches.cpp` L120-128](https://github.com/hedge-dev/UnleashedRecomp/blob/main/UnleashedRecomp/patches/fps_patches.cpp)).
  - It added Vulkan present-wait for pacing
    ([PR #53](https://github.com/hedge-dev/UnleashedRecomp/pull/53)).
  - A 144 Hz user still saw judder at a stable 60, cured only by an external
    limiter with vsync
    ([#1252](https://github.com/hedge-dev/UnleashedRecomp/issues/1252),
    **[community]** workaround). That is the high-refresh case ticket 16 has to
    cover.
- **Frame-rate-coupled simulation:** Unleashed handles it per system with
  mid-asm hooks:
  - delta-time clamps that keep "the original game behavior when locked to
    30/60 FPS";
  - a fixed 30 Hz substep for a boss whose timers ignore delta time;
  - lerp-factor corrections for cameras
    ([fps_patches.cpp](https://github.com/hedge-dev/UnleashedRecomp/blob/main/UnleashedRecomp/patches/fps_patches.cpp)).

  Its tracker shows the long tail of such bugs, for example
  [#582](https://github.com/hedge-dev/UnleashedRecomp/issues/582) and
  [#1042](https://github.com/hedge-dev/UnleashedRecomp/issues/1042). This
  supports the 1.0 decision to stay at the 60 fps tick (goals.md).
- **Presenting at 30 between points while keeping 60 elsewhere:** I found no
  recomp that solved this generically. Top Spin 4 halves its own rate (ticket
  01, `docs/research/oracle-baseline.md`), so the port only needs to honour
  the guest's swap interval against a correct 60 Hz guest vblank **[inferred]**.
  Relevant upstream pieces:
  - Edge [`aa33399`](https://github.com/has207/xenia-edge/commit/aa3339970c):
    the vblank is paced on a guest-tick deadline with a two-period reset, the
    same shape as Unleashed's limiter;
  - Edge [`9d93773`](https://github.com/has207/xenia-edge/commit/9d93773228):
    synthesises `D1MODE_V_COUNTER` from the measured vblank cadence, for games
    that poll the scanline;
  - ReXGlue v0.7.4 "corrected guest vblank counter pacing"
    ([#276](https://github.com/rexglue/rexglue-sdk/pull/276)).

  The local patches `0004-frame-pacing`, `0013-smooth-presentation`,
  `0016-present-wait`, `0019-live-vblank` and `0024-guest-vblank-rate` (from
  WWE) already cover this ground.
- **Timer resolution:** a ReXGlue fix stopped SDL turning off the Windows
  high-resolution timer. With it off, more than half the audio blocks were
  repeats ([#403](https://github.com/rexglue/rexglue-sdk/pull/403), merged
  2026-08-08, included in v0.10.0).

---

## 5. Audio: XMA and XAudio2

- **ReXGlue history:** a rewritten XMA decoder in v0.7.1
  ([#250](https://github.com/rexglue/rexglue-sdk/pull/250),
  [#254](https://github.com/rexglue/rexglue-sdk/pull/254)), then "port over some
  fixes from xenia" ([#256](https://github.com/rexglue/rexglue-sdk/pull/256)).
  The crash report [#257](https://github.com/rexglue/rexglue-sdk/issues/257) is
  closed. Locally, `0010-xma-output-valid` and `0011-xma-frame-drain` are
  applied.
- **Edge decoder fixes from August to October 2026 that ReXGlue lacks:**
  - [`9d8210b`](https://github.com/has207/xenia-edge/commit/9d8210b32d):
    "Follow the sub-stream skip chain past frameless packets". It fixes
    multi-stream buffers that "decoded the wrong stream's frames", and LEGO
    games freezing in cutscenes. ReXGlue still scans packets one after another
    (`src/audio/xma_context.cpp:364-377`).
  - [`adf56b7`](https://github.com/has207/xenia-edge/commit/adf56b76c4): counts
    a frame whose header crosses the packet boundary. Otherwise "each drop cost
    two frames" (music glitches).
  - [`5dd1cdb`](https://github.com/has207/xenia-edge/commit/5dd1cdbbf5):
    aligns `loop_start` to a frame boundary. Otherwise FFmpeg rejects the packet
    and the context goes silent "for the rest of the session" (Tekken Tag 2).
  - [`052365b`](https://github.com/has207/xenia-edge/commit/052365bc01): drains
    the current frame before the work loop ends. It fixes a guest audio thread
    deadlocking. This is probably what our `0011-xma-frame-drain` does; diff
    the two.
  - [`2372a54`](https://github.com/has207/xenia-edge/commit/2372a54105): rounds
    samples instead of truncating. ReXGlue uses `_mm_cvtps_epi32`
    (`src/audio/xma_context.cpp:825-846`); check its rounding mode.
  - [`1fae8f4`](https://github.com/has207/xenia-edge/commit/1fae8f43a5): decodes
    on the kicking thread by default. This is Edge's
    `use_dedicated_xma_thread = false`, as in the owner's config.
  - [`12e3b42`](https://github.com/has207/xenia-edge/commit/12e3b4223d): keeps
    the audio pump off the global lock, and pacing "advances from the missed
    deadline instead of now".
- **Audio-thread FP state:** see #476 and #459 in §3. Guest audio callbacks on
  the host worker can run with exceptions unmasked.
- **UnleashedRecomp:** I found no XMA-specific reports (it decodes its own
  formats in `apu/`). Its audio issues are SDL or PipeWire crackling
  ([#1189](https://github.com/hedge-dev/UnleashedRecomp/issues/1189),
  [#818](https://github.com/hedge-dev/UnleashedRecomp/issues/818)).

---

## 6. ReXGlue: repo, changelog, known bugs

- **Releases after v0.10.0:** none. Only nightlies exist, the latest
  `nightly-20261002-bd833a2a`
  ([releases](https://github.com/rexglue/rexglue-sdk/releases)). `main` and
  `release/v0.10.0` stop at the v0.10.0 commit. Work continues on
  `development`, which has 26 commits since 2026-08-21:
  - codegen: fences (`bd833a2`), `stwcx.` CAS (`7f7c92e`), unsigned register
    types (`ea222e9`, ref #450), `vpkuwus` aliasing (`6319e23`, #449);
  - system: `writable_code_segments` (`5e2dae1`);
  - kernel: leaked event, socket and port objects (`0c7b01a`);
  - xam: content result codes (`ee20900`);
  - filesystem truncation (`01ac2a3`), input (vibration, hotplug, keyboard),
    UI, logging and build fixes.

  None touches GPU fetch masking, render targets or modules.
- **Docs:** the [wiki](https://github.com/rexglue/rexglue-sdk/wiki) (Memory,
  Mid-ASM Hooks, Function Overrides, Kernel State, CLI). It has nothing on
  render-target paths or `execute_unclipped_draw_vs_on_cpu`.
- **The host render-target path and `execute_unclipped_draw_vs_on_cpu`:**
  - [#513](https://github.com/rexglue/rexglue-sdk/pull/513) (open PR, Tekken 6)
    makes the cvar default true, as in Xenia. It describes our mechanism:
    `EstimateMaxY` returns 8192, and the depth claim "wraps past tile 2047 and
    takes ownership of color data". A second reporter confirms it on MoltenVK.
    We now set the cvar in `src/topspin4_app.h`, so this is parity, not a new
    lead.
  - [#508](https://github.com/rexglue/rexglue-sdk/issues/508) (open, NFL Head
    Coach 09): on the Vulkan FBO path the front-buffer resolve sources a depth
    RT after depth-func-NEVER point draws. It shows the same black front buffer,
    and the reporter's workaround was FSI. Their hypothesis is ownership
    transfer for draws that can't write; #513's explanation fits it as well
    **[inferred]**. Worth a comment linking the two.
  - [#520](https://github.com/rexglue/rexglue-sdk/issues/520) (open, Viva
    Piñata): on AMD the FSI path loses 83–92% of pixel-shader time to the POPS
    wait loop. A fork (`crabinacrabic/rexglue-sdk`, branch `rov-research`)
    reaches full speed with exact EDRAM. This matters only if Classic ever needs
    FSI again.
  - [#521](https://github.com/rexglue/rexglue-sdk/issues/521) (open): on the AMD
    Windows driver, `vkWaitForFences` with more than about 64 fences returns
    `VK_ERROR_UNKNOWN` and drops draws. The fix waits in chunks of 32 (fork
    commit
    [`afee59d`](https://github.com/crabinacrabic/rexglue-sdk/commit/afee59daa57952ee799a06e85f11fac8260eefb6)).
    This matters on the owner's RX 7900 XT if long resolve chains pile up
    submissions.
  - Occlusion queries: Swing's end-of-frame point draws are queries (ticket 13).
    Edge has full ZPD reports
    ([`fbd620c`](https://github.com/has207/xenia-edge/commit/fbd620c22b),
    default `fast`, [`95efc4c`](https://github.com/has207/xenia-edge/commit/95efc4cf67)).
    ReXGlue has the older single-query path behind `occlusion_query_enable`
    (`src/graphics/command_processor.cpp:79`, D3D12 `:153`). Edge also closes
    the query when render targets switch
    ([`d649a01`](https://github.com/has207/xenia-edge/commit/d649a01caf)).
    This could explain culling differences, though none have been seen yet
    **[inferred]**.
- **Multiple runtime DLL modules** (open, beyond feasibility.md):
  - [#420](https://github.com/rexglue/rexglue-sdk/issues/420) and
    [#467](https://github.com/rexglue/rexglue-sdk/pull/467): `AllocateThunk`
    allocates a new thunk for every `XexGetProcedureAddress` lookup. A title
    that looks up exports every frame exhausts the pool, and rendering stops
    after a fixed time (Guwange). Watch the log for "Thunk address space
    exhausted" in long matches.
  - Still open from feasibility.md: #443, #448, #454, #499, #421.

---

## Open questions

1. **Where does `0xC0100100` come from?** Is it the XPS ring (does the game's
   `KeLockL2` caller hand out `0x7F1xxxxx`/`0xC01xxxxx`), or a plain physical
   allocation whose CPU address the game's D3D copy put into the fetch
   constant? Set a breakpoint on the `KeLockL2` import in Swing.
2. **After masking, do those draws show correct geometry, or stale/zero data?**
   That depends on whether the CPU writes through `0x7F…` (unwatched in ReXGlue)
   and on #510. Compare with Edge at the same frame.
3. **What do the 384 dropped draws render on the Oracle?** Ball trail, crowd,
   particles? Nothing published says.
4. Is `0011-xma-frame-drain` equivalent to Edge's `052365b`?
5. Does Top Spin 4 call `XexGetProcedureAddress` per frame (#420)?
6. Does any Top Spin 4 thread run guest code on an `XHostThread` (#476)?
7. **Havok / OpenMP on 360 recomps:** I found no public reports, so expect to
   be first.
