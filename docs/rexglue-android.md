# ReXGlue SDK on Android arm64 — feasibility study

SDK: https://github.com/rexglue/rexglue-sdk @ `f5337cd` (v0.10.0, the commit the Windows
reference port `_ref/Top-Spin-4-Recomp` pins). Local read-only clone: `_ref/rexglue-sdk`
(submodules initialised shallow; nothing in it modified).

Target: Android arm64-v8a, Vulkan, Retroid-class handheld (Adreno/Mali, 8 GB, Android 13/14,
4 KB pages assumed unless proven otherwise).

**Headline:** the SDK *already contains* a half-finished Android port inherited from Xenia —
`REX_PLATFORM_ANDROID` is defined and used in ~15 runtime files, `VK_KHR_android_surface` is
wired into the Vulkan instance/presenter, guest memory has an ashmem path, and threading has
pre-API-26 workarounds. What is missing is (a) the build system never selects Android,
(b) the Android `Surface` implementation (`include/rex/ui/surface_android.h`) does not exist even
though `vulkan_presenter.cpp` includes it, and (c) a handful of Android-only helper symbols
(`rex::GetAndroidApiLevel`, `<rex/main_android.h>`) are referenced but not defined. So this
is *feasible-with-work*, not blocked, and the work is mostly re-assembly of existing pieces.

---

## a. Top-level CMake

`CMakeLists.txt`

| Item | Where | Android verdict |
|---|---|---|
| cmake ≥ 3.25 | :2 | NDK r27+ ships cmake support; fine |
| Languages | :4-8 (adds OBJC on APPLE) | fine |
| Graphics backend | :37-43 `if(WIN32) D3D12 … else() Vulkan ON` | **good** — Android lands on the Vulkan branch |
| Clang required | :83-95 (`Clang` ≥ 18, `AppleClang` ≥ 16) | NDK r27/r28 report `Clang` 18/19 → passes; NDK ≤ r26 fails |
| 64-bit check | :98-100 | passes |
| C++ standard | :103-105 → **C++23**, no extensions | OK with NDK r27 libc++ (needs `std::expected` from `include/rex/result.h`, `std::jthread` from `src/core/timer_queue.cpp:43`); NDK r26 would be marginal |
| Code model / PIC | :117-129: `-mcmodel=large` only for Linux x86_64, else `-mcmodel=small`, PIC ON | fine on aarch64 (`adrp` ±4 GB is enough for the 35 MB recompiled text) |
| Global flags | :132-140 `-fno-strict-aliasing -ffp-model=strict -fno-char8_t` | all clang flags, accepted by NDK clang |
| **Platform detection** | :142-181 | **broken for Android.** `ANDROID` implies `UNIX=1`, so Android falls into :161 `elseif(UNIX AND NOT APPLE)`, sets `REX_PLATFORM=linux-arm64` and `REX_PLATFORM_LINUX=1`, and the `FATAL_ERROR "Unsupported platform …"` at :180 is never reached. Android silently configures as GNU/Linux. |
| Output dirs | :226-228 → `out/${REX_PLATFORM}` | would collide with linux-arm64 |
| Subdirs | :244, :254-263 | `src/rexglue` is the host recompiler CLI — must be excluded on Android |

Note `REX_PLATFORM_ANDROID` is *not* set by CMake at all; it comes from `__ANDROID__` in
`include/rex/platform.h:35-37` (which also sets `REX_PLATFORM_LINUX=1`). The codebase therefore
distinguishes "GNU/Linux" from "Linux-ish" via `REX_PLATFORM_GNU_LINUX` (`platform.h:38-40`),
used e.g. in `include/rex/ui/vulkan/api.h:35` to gate the XCB/Wayland includes. That split is
correct and helps the port: no Wayland/XCB headers leak in on Android.

### Third-party deps (`thirdparty/CMakeLists.txt`)

| Dep | Android |
|---|---|
| fmt, spdlog, tomlplusplus, utfcpp, xxHash, cli11, inja, stb, disruptorplus, renderdoc header, o1heap, tiny-aes, aes_128, libmspack(lzxd), disasm | portable C/C++, fine |
| **simde** (:98-103) | essential and fine — this is how the x86 vector intrinsics reach NEON |
| imgui (:230-242, OBJECT lib, no backend) | fine |
| **SDL3 3.4.x** (:247-304) | fine — SDL3 has `src/video/android`, `src/audio/aaudio`, `src/audio/openslES`, `src/joystick/android`. But the option block is Linux-specific: :251 `SDL_UNIX_CONSOLE_BUILD ON`, :293-302 turns on X11/Wayland/ALSA/PulseAudio/PipeWire for `UNIX AND NOT APPLE` (would enable ALSA/PulseAudio on Android). Needs `AND NOT ANDROID`. |
| vulkan-headers, VMA (:191-202) | fine; `api.h:29-33` defines `VK_USE_PLATFORM_ANDROID_KHR` |
| vulkan-loader, MoltenVK (:204-219) | correctly APPLE-only; Android uses the system loader |
| spirv-headers/spirv-tools/glslang (:319-347) | portable, but they are compiled in and add size/build time; consider `ENABLE_OPT OFF` already set |
| Tracy (:359-404) | ON by default (:24); works but pointless on a handheld → disable |
| FFmpeg (:411-704) | has a real aarch64 path: `enable_language(ASM)` + `-march=armv8-a` (:446-449), NEON sources and `HAVE_INLINE_ASM=1` (:543-554, :665-681), `compat/atomics/gcc` (:440). `libavcodec/jni.c`, `mediacodec.c` are in the list (:589-591) and compile as stubs off-Android / use `jni.h` on-Android. Works, but note `#include <linux/ashmem.h>` in the SDK's own memory code (below) and NDK r27+ removing some legacy uapi headers. |
| dxc/dxbc | D3D12-only, skipped |

Submodules are required by an explicit `FATAL_ERROR` check at `thirdparty/CMakeLists.txt:66-74`,
so the Android build still needs the full submodule set.

---

## b. Platform / windowing layer

Pipeline: `SDLWindowedAppContext::Initialize()` (`src/ui/windowed_app_context_sdl.cpp:40-70`,
`SDL_InitSubSystem(SDL_INIT_VIDEO)`) → `Window::Create` → `WindowSDL`
(`src/ui/window_sdl.cpp:116-127`) → `WindowSDL::CreateSurfaceImpl`
(`src/ui/window_sdl.cpp:373-423`) → `VulkanPresenter` (`src/ui/vulkan/vulkan_presenter.cpp`)
→ `vkCreate*SurfaceKHR` (:799-850).

What already exists for Android:

- `include/rex/ui/surface.h:31,42` — `kTypeIndex_AndroidNativeWindow`,
  `kTypeFlag_AndroidNativeWindow`.
- `src/ui/vulkan/vulkan_presenter.cpp:36` `#include <rex/ui/surface_android.h>`,
  :426-431 advertises the Android surface type, :799-811 the real
  `VkAndroidSurfaceCreateInfoKHR` + `vkCreateAndroidSurfaceKHR` path, :1224-1232 prefers
  `VK_FORMAT_R8G8B8A8_UNORM` on Android (Android compositors are RGBA, not BGRA).
- `src/ui/vulkan/vulkan_instance.cpp:153-156` requests `VK_KHR_android_surface`;
  :426-430 loads it; `include/rex/ui/vulkan/instance.h:67,100` and
  `include/rex/ui/vulkan/functions/instance_khr_android_surface.inc` exist.

What is missing (this is the actual port work for the window layer):

1. **`include/rex/ui/surface_android.h` does not exist** (the `rex/ui/` dir has only
   `surface_win.h`, `surface_mac.h`, `surface_gnulinux.h`). `vulkan_presenter.cpp:36` will fail
   to compile on Android today. Must add, mirroring `include/rex/ui/surface_win.h`:
   ```cpp
   class AndroidNativeWindowSurface final : public Surface {
     TypeIndex GetType() const override { return kTypeIndex_AndroidNativeWindow; }
     ANativeWindow* window() const;
    protected:
     bool GetSizeImpl(uint32_t& w, uint32_t& h) const override;
   };
   ```
   Only three members are needed: `GetType()`, `GetSizeImpl()` (`surface.h:53,81`) and the
   `window()` accessor used by the presenter. `src/ui/surface_android.cpp` implements
   `GetSizeImpl` with `ANativeWindow_getWidth/Height` (or `SDL_GetWindowSizeInPixels`).
2. **`src/ui/window_sdl.cpp` breaks on Android**: :31-36 is
   `#if WIN32 … #elif MAC … #else → <X11/Xlib-xcb.h>, surface_gnulinux.h`, and
   `CreateSurfaceImpl` (:379-423) has only Win32/macOS/GNU-Linux branches. Needs
   `#elif REX_PLATFORM_ANDROID` reading
   `SDL_PROP_WINDOW_ANDROID_WINDOW_POINTER` (SDL3 `SDL_video.h:1641`) and constructing
   `AndroidNativeWindowSurface`.
3. **`src/ui/CMakeLists.txt`**: :66-76 selects `surface_gnulinux.cpp` for everything that is not
   WIN32/APPLE → add an `elseif(ANDROID)` branch; :138-152
   `find_package(PkgConfig REQUIRED)` + `pkg_check_modules(X11_XCB REQUIRED x11-xcb)` +
   `wayland-client REQUIRED` → must be gated off for Android (no pkg-config in the NDK
   toolchain, no X11).
4. **Entry point**: `src/ui/windowed_app_main_sdl.cpp:129` defines plain `main()`. On Android the
   game is a `.so` loaded by the Java activity; the TU must include `<SDL3/SDL_main.h>` and be
   built as a library (the SDK already anticipates this:
   `include/rex/ui/windowed_app.h:27-30` sets `XE_UI_WINDOWED_APPS_IN_LIBRARY` for Android).
   SDL3-static already handles `JNI_OnLoad` (`thirdparty/sdl3/CMakeLists.txt:1662-1663`).
5. Window-level Android caveats: `SDL_WINDOW_RESIZABLE`/`HIGH_PIXEL_DENSITY` are no-ops; the
   surface is tied to the Activity lifetime, so `SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED`
   (`window_sdl.cpp:445-448`) is the resize path, and pause/resume means swapchain destruction —
   `VulkanPresenter` must tolerate `vkCreateAndroidSurfaceKHR` returning
   `VK_ERROR_SURFACE_LOST_KHR` and re-create (it already has a swapchain-recreate path,
   `vulkan_presenter.cpp:786-800`).
6. `src/ui/windowed_app_context_sdl.cpp:44-52` only special-cases `cocoa`; on Android leave the
   driver hint empty (SDL picks `android`).

---

## c. CPU / JIT assumptions

There is **no runtime JIT and no x86 emitter**. Guest code is AOT-recompiled to C++
(`using PPCFunc = void(PPCContext&, uint8_t* base)` — `include/rex/ppc/func.h:28-31`), and the
emitted source uses **simde** (`src/codegen/builders/memory.cpp:549-572` and the rest of
`src/codegen/builders/`), i.e. SSE intrinsics that simde maps to NEON on aarch64. Grep for
`musttail`, `preserve_none`, `goto *`, `&&label`, x64 emitters in `src/` + `include/` returns
nothing. The only inline asm in the runtime are:

- `include/rex/ppc/function.h:418` — `asm volatile("" ::: "memory")` (compiler barrier, portable).
- `include/rex/platform/fpscr.h:74-78` — `mrs/msr fpcr`, inside the `__aarch64__` branch.

All x86-specific code is guarded and has an arm64 sibling:

| Area | x86 | arm64 |
|---|---|---|
| byte-swap/copy | `src/core/memory.cpp:46-203` | `:205-320` (`arm_neon.h`) |
| MMIO fault decode | `src/system/mmio_handler.cpp:128-260` | `:267-430` (AArch64 load/store decoder) |
| primitive processor | `src/graphics/primitive_processor.cpp:955,1015,1027,1081,1127,1171` | same lines `#elif REX_ARCH_ARM64` |
| exception context | `include/rex/exception_handler.h:135-233` | `:239-298` (ESR/FPSIMD) |
| FPSCR | `fpscr.h:26-64` (`simde_mm_getcsr`) | `fpscr.h:66-79` (FPCR) |

CI already builds and ships `linux-arm64` (`.github/workflows/build-linux-aarch64.yaml`,
preset `linux-arm64` in `CMakePresets.json`), so the arm64 code paths are exercised, just never
on Android's libc.

Two CPU-level notes for the port:

- `src/core/memory.cpp:139-147` `IsWritableExecutableMemorySupported()` returns **true** on
  Android. Android 10+ (targetSdk ≥ 29) denies W+X anonymous mappings via SELinux `execmem`.
  Today nothing at runtime allocates executable memory (no code cache, no `kExecuteReadWrite`
  callers outside the memory API — grep confirms), so this is latent, not fatal; it should
  return false on Android and use the dual RW/RX mapping path that already exists for macOS.
- `-ffp-model=strict` + explicit FPCR writes are correct on aarch64, but the Xbox 360 denormal
  behaviour is approximated through FPCR FZ/FZ16 (`fpscr.h:66-79`), the same as the existing
  linux-arm64 build. Expect to re-validate FP-sensitive gameplay paths.

---

## d. Memory

Guest memory is a **4.5 GB file-backed reservation** mapped at a 4 GB-aligned base, with
`membase` = that base and the physical view at `membase + 0x100000000`:

- `src/system/xmemory.cpp:137-146` — size `0x120000000` rounded to allocation granularity,
  `CreateFileMappingHandle(...)`.
- `:154-174` — try `1ull << n` for n = 32..63 with `MAP_FIXED`-style views; `virtual_membase_ =
  mapping_base_`, `physical_membase_ = mapping_base_ + 0x100000000`.
- `:355-380` `MapViews` maps 9 views with `MAP_SHARED | MAP_FIXED` and a granularity-rounded
  file offset (`MapFileView`, `src/core/memory_posix.cpp:498-529`).
- POSIX reserve/commit emulation: `src/core/memory_posix.cpp:240-317` (`mmap` +
  `MAP_FIXED_NOREPLACE`, EEXIST → `mprotect`), `:319-339` decommit/release, `:341-374` Protect.

Android-specific code already present in `src/core/memory_posix.cpp`:

- `:48-58` `#include <linux/ashmem.h>`, `dlopen("libandroid.so")`.
- `:87-112` `AndroidInitialize/Shutdown` → `ASharedMemory_create` (API 26+).
- `:432-456` `CreateFileMappingHandle` uses `ASharedMemory_create`, else `/dev/ashmem`.
- `:492-496` skips `shm_unlink` on Android.
- `src/core/mapped_memory_posix.cpp:124-160` `OpenForAndroidContentUri`.

Problems to fix:

1. `rex::GetAndroidApiLevel()` is **referenced at `src/core/memory_posix.cpp:94` and
   `src/core/threading_posix.cpp:74` but defined nowhere** in the tree → link error. Must be
   added (trivial: `android_get_device_api_level()` from `<android/api-level.h>`).
2. `<linux/ashmem.h>` (`:54`) is not in modern NDK sysroots (removed as part of the ashmem
   deprecation). Keep only the `ASharedMemory_create` path (API 26+ is a safe floor for a
   Retroid-class device) and drop the ashmem fallback, or vendor the 30-line header.
3. Address space: an arm64 Android app has a 39-bit (512 GB) user VA with 4 KB pages, 48-bit
   with 16 KB pages. A 4.5 GB reservation at a 4 GB-aligned base is comfortably inside that, and
   ashmem/memfd pages are faulted lazily, so RSS is not 4.5 GB. This is the same scheme the
   Xenia Android port ships, so it is proven. `MAP_FIXED_NOREPLACE` requires a 4.17+ kernel
   (all target devices); `MapFileView` already verifies the returned address.
4. **16 KB page-size hazard.** `include/rex/system/xmemory.h:41-48` `PhysicalHostOffset()` is a
   `constexpr` that adds the 0x1000 offset for the 0xE0…0xFF heap only for
   `REX_PLATFORM_WIN32 || (REX_PLATFORM_MAC && __aarch64__)`, while the heap itself computes the
   offset dynamically from `allocation_granularity() > 0x1000`
   (`src/system/xmemory.cpp:1876-1883`). On a 16 KB-page Android device every recompiled
   access to `0xE0000000..0xFFFFFFFF` would be off by 0x1000. Either confirm the target device
   runs 4 KB pages (most Android 13/14 arm64 handhelds do) or make this runtime-consistent.
5. `GetExecutableFolder()` uses `/proc/self/exe` (`src/core/filesystem_posix.cpp:94-104`), which
   on Android returns `/system/bin/app_process64` → the GPU plugin loader
   (`src/system/gpu_plugin_loader.cpp:58-64`, plugin staged "next to the executable") will not
   find `librexgpu-xenos.so`. Must resolve to `nativeLibraryDir` (via JNI/`dladdr`) instead.
   `GetUserFolder()` (`filesystem_posix.cpp:106-121`, XDG/HOME) also needs to point at the app's
   internal storage. `OpenAndroidContentFileDescriptor` is declared
   (`include/rex/filesystem.h:133`) but never defined.

---

## e. Audio and input

Audio: SDL3 only (`src/audio/sdl/sdl_audio_driver.cpp:46-90`,
`SDL_OpenAudioDeviceStream`), with XMA decode through the vendored FFmpeg and a NOP backend
(`src/audio/CMakeLists.txt:13-20`). Android story: **good** — SDL3 has AAudio
(`thirdparty/sdl3/src/audio/aaudio`) and OpenSL ES backends, and SDL picks them automatically.
The only change is CMake: `thirdparty/CMakeLists.txt:287-302` currently enables
ALSA/PulseAudio/PipeWire for `UNIX AND NOT APPLE` and must skip Android.

Input: SDL3 gamepad driver (`src/input/sdl/sdl_input_driver.cpp:92-96`
`SDL_InitSubSystem(SDL_INIT_GAMEPAD)`), XInput driver is Win32-only
(`src/input/CMakeLists.txt:38-42`). Android story: **good** — SDL3's `joystick/android` backend
turns Android input devices (BT/USB pads, and the handheld's built-in controls) into gamepads.
Touch/mouse goes through the same `Window` events; the ImGui overlay already handles mouse.
No Android-specific work beyond verifying the built-in controller's axis/trigger mapping.

Note `thirdparty/CMakeLists.txt:271-280` turns off `SDL_HAPTIC`, `SDL_SENSOR`, `SDL_POWER`,
`SDL_FILESYSTEM` — rumble on the handheld will not work until `SDL_HAPTIC` is re-enabled for
Android (a one-line CMake change if rumble matters).

---

## f. Threading / exception handling

- Threads: pthread throughout (`src/core/threading_posix.cpp`), and the file already contains
  deliberate Android support — `kThreadTerminate` signal instead of `pthread_cancel`
  (:106-110), pre-API-26 `pthread_getname_np` via `dlopen("libc.so")` (:67-89),
  `android_pre_api_26_name_` storage (:643-660, :723-750). Missing: `rex::GetAndroidApiLevel()`
  and `#include <rex/main_android.h>` (:45-48 — that header does not exist in `include/rex/`).
- Thread priorities: `pthread_attr_setschedpolicy(SCHED_FIFO)` (:658-676) would fail for an
  unprivileged Android app, but it is only reached when `initial_priority != 0`, and nothing in
  the tree sets it (`include/rex/thread.h:399`, no writers) → no action needed.
- SEH: `src/core/seh_posix.cpp:20-70` implements SEH by **throwing a C++ exception from a
  signal handler** (`SIGSEGV`/`SIGBUS`/`SIGFPE`/`SIGILL`). bionic allows this and the NDK's
  libc++abi can unwind through the signal frame, but it is not an officially supported contract;
  this is the single highest-risk runtime item to validate on device (guest code traps on MMIO
  and on `Xam` guard pages regularly).
- Fault handling: `src/core/exception_handler_posix.cpp` — `sigaction` + `ucontext_t`, with an
  explicit ARM64 path reading `esr_context`/`fpsimd_context` (:112-180, :195-260) and a comment
  that already names Android (:208-213). `ucontext`/`makecontext`/`swapcontext` for fibers
  (`src/core/fiber_posix.cpp:33-90`) exist in bionic (deprecated but present) — no work.
- `REX_HAS_SIGEV_THREAD_ID` is enabled for all Linux (:52-60) and only remaps the field name
  under `__GLIBC__`; bionic names the field `sigev_notify_thread_id` natively, so the timer queue
  (`timer_queue.cpp`) should compile as-is.
- `src/core/CMakeLists.txt:104` links `pthread rt dl` for all UNIX — **`-lrt` does not exist on
  Android** (it is part of libc) and will fail at link time; `dl` is fine.

---

## g. The 46 patches vs Android

All 46 apply **cleanly and in order** to `f5337cd` (verified with `git archive` + sequential
`git apply` in a scratch copy; a per-file `git apply --check` fails for 18 of them because they
are an ordered series — `scripts/apply-patches.ps1` already applies them in name order).

Classification: **none is Win32-exclusive.** Only 3 patches contain Win32-only hunks, and all
three are inside `#if REX_PLATFORM_WIN32` regions or Win32-only translation units, so they
compile to nothing on Android. "Adapt" below means the code compiles but its *behaviour* is
platform-dependent.

| # | Patch | Files touched | Android |
|---|---|---|---|
| 0001 | window-restore | `src/ui/window.cpp` | keep |
| 0002 | async-pipelines | `src/graphics/vulkan/pipeline_cache.cpp` | keep |
| 0003 | xmp-no-delay | `src/kernel/xam/apps/xmp_app.cpp` | keep |
| 0004 | frame-pacing | `include/rex/thread.h`, `ui/presenter.h`, `src/core/threading_{posix,win}.cpp`, `src/graphics/graphics_system.cpp`, `src/ui/{presenter,rex_app}.cpp`, `src/ui/overlay/achievement_toast.cpp` | keep — `threading_win.cpp` hunk is Win32-only and inert |
| 0005 | resolve-readback | `include/rex/graphics[/vulkan]/command_processor.h`, `src/graphics[/vulkan]/command_processor.cpp` | keep |
| 0006 | draw-census | `src/graphics/command_processor.cpp`, `src/graphics/vulkan/command_processor.cpp` | keep |
| 0007 | native-frame | `src/system/CMakeLists.txt`, `src/graphics/vulkan/command_processor.cpp`, `src/ui/vulkan/vulkan_device.cpp`, `ui/vulkan/device.h`, **new** `system/external_frame.{h,cpp}` | keep (adds a source to `src/system/CMakeLists.txt`) |
| 0008 | native-shader-storage | `src/ui/rex_app.cpp` | keep |
| 0009 | ib-census | `src/graphics/command_processor.cpp` | keep |
| 0010 | xma-output-valid | `audio/xma/context.h`, `src/audio/xma_context.cpp`, **new** `audio/xma/work_rules.h` | keep |
| 0011 | xma-frame-drain | `audio/xma/work_rules.h`, `src/audio/xma_context.cpp` | keep |
| 0012 | guest-tick-source | `chrono/clock.h`, `src/core/clock.cpp` | keep (uses `steady_clock`, not `rdtsc`) |
| 0013 | smooth-presentation | 15 files incl. **new** `system/external_frame.{h,cpp}`, `system/swap_choice.h`, `graphics/vblank_source.h`, `src/graphics/display_vblank.{h,cpp}`; `src/graphics/CMakeLists.txt` (+`gdi32` under `if(WIN32)`) | keep; **adapt**: `display_vblank.cpp` is D3DKMT/Win32 with a non-Win stub (`DisplayVblank::Open()` returns false) → Android gets "no display vblank" and pacing falls back. Optional later: `AChoreographer`/`VK_KHR_display_timing`. |
| 0014 | in-game-settings | `input/input_system.h`, `src/core/cvar.cpp`, `src/input/input_system.cpp`, `src/ui/rex_app.cpp` | keep |
| 0015 | device-extensions | `ui/vulkan/device.h`, **new** `device_extension_plan.h` + 5 `functions/*.inc`, `ui/vulkan/instance.h`, `src/ui/vulkan/vulkan_{device,instance}.cpp` | keep; `VK_KHR_present_wait{,2}` are negotiated and simply absent on Android → 0016 falls back |
| 0016 | present-wait | `include/rex/thread.h`, **new** `ui/present_pacing.h`, `ui/present_waiter.h`, `ui/presenter.h`, `ui/vulkan/presenter.h`, `src/core/threading_{posix,win}.cpp`, `src/graphics/graphics_system.cpp`, `src/ui/{presenter,vulkan/vulkan_presenter}.cpp` | keep — Win32 hunk inert |
| 0017 | live-sharpening | `ui/presenter.h`, `src/ui/presenter.cpp` | keep |
| 0018 | live-vsync | `src/ui/vulkan/vulkan_presenter.cpp` | keep (present modes are queried, FIFO is guaranteed) |
| 0019 | live-vblank | `src/graphics/graphics_system.cpp` | keep |
| 0020 | tu2-update | `src/system/user_module.cpp` | keep |
| 0021 | content | `src/kernel/xam/xam_content.cpp` | keep (guest VFS, not host paths) |
| 0022 | overlay-pacing | `ui/imgui_dialog.h`, `ui/overlay/achievement_toast.h`, `src/ui/imgui_drawer.cpp`, `src/ui/overlay/achievement_toast.cpp` | keep |
| 0023 | fullscreen-toggle | `rex_app.h`, `ui/ui_event.h`, `src/ui/rex_app.cpp`, `src/ui/window_sdl.cpp` | keep; **adapt** — Alt+Enter/double-click is desktop-only, `SDL_SetWindowFullscreen` on Android maps to immersive mode |
| 0024 | guest-vblank-rate | `graphics/graphics_system.h`, `graphics/vblank_source.h`, `system/interfaces/graphics.h`, `ui/presenter.h`, `src/graphics/graphics_system.cpp`, `src/ui/presenter.cpp` | keep |
| 0025 | gpu-loss-handler | **new** `system/host_gpu_loss.h`, `src/system/host_gpu_loss.cpp`; `src/graphics/graphics_system.cpp`, `src/system/CMakeLists.txt` | keep (more relevant on Android: backgrounding/app-switch loses the device) |
| 0026 | headless-keyboard | `src/kernel/xam/xam_ui.cpp` | keep (ImGui keyboard, no Win32) |
| 0027 | refuse-unrecompiled-modules | `src/system/kernel_state.cpp` | keep |
| 0028 | modules-from-own-folder | `src/system/kernel_state.cpp` | **adapt** — compares resolved guest VFS parents; works, but "own folder" is the APK/asset root on Android |
| 0029 | guest-swap-count | `src/kernel/xboxkrnl/xboxkrnl_video.cpp`, **new** `system/guest_swaps.h` | keep |
| 0030 | io-trace | `src/kernel/xboxkrnl/xboxkrnl_io.cpp`, `src/system/xthread.cpp` | keep |
| 0031 | frame-shown-observer | `system/guest_swaps.h`, `src/graphics/command_processor.cpp`, `src/kernel/xboxkrnl/xboxkrnl_video.cpp` | keep |
| 0032 | spirv-fetch-exp-adjust-word | `src/graphics/pipeline/shader/spirv_translator_fetch.cpp` | keep — Android drivers are stricter about fetch constants |
| 0033 | gpu-debug-ps-probe | `graphics/pipeline/shader/{spirv_,}translator.h`, `src/graphics/pipeline/shader/spirv_translator{,_rb}.cpp`, `translator.cpp` | keep |
| 0034 | mask-fetch-addresses | `spirv_translator{,_fetch,_memexport}.h/cpp`, `src/graphics/{d3d12,vulkan}/command_processor.cpp`, `primitive_processor.cpp`, `util/draw.cpp` | keep — the `d3d12` hunks are simply not compiled |
| 0035 | message-box-log | `src/kernel/xam/xam_ui.cpp` | keep (ImGui dialog) |
| 0036 | message-box-rewriter | **new** `kernel/xam/message_box.h`, `src/kernel/xam/xam_ui.cpp` | keep |
| 0037 | signin-ui-closes-unchanged | `system/kernel_state.h`, `src/kernel/xam/xam_{notify,user}.cpp`, `src/system/kernel_state.cpp` | keep |
| 0038 | window-hidden | `src/ui/window_sdl.cpp` | keep; **adapt** — a hidden SDL window on Android still owns the surface; only used for unattended runs |
| 0039 | swap-shows-own-frame | `system/external_frame.h`, `system/guest_swaps.h`, `system/swap_choice.h`, `src/kernel/xboxkrnl/xboxkrnl_video.cpp`, `src/system/external_frame.cpp` | keep |
| 0040 | swap-numbering-recovery | `system/guest_swaps.h`, `system/swap_choice.h`, `src/kernel/xboxkrnl/xboxkrnl_video.cpp` | keep |
| 0041 | presented-output-capture | `ui/presenter.h`, `ui/vulkan/presenter.h`, `src/ui/vulkan/vulkan_presenter.cpp` | keep (readback path is backend-agnostic) |
| 0042 | present-hold | **new** `ui/present_hold.h`, `ui/presenter.h`, `src/ui/{presenter,vulkan/vulkan_presenter}.cpp` | keep |
| 0043 | pacing-log-hold-columns | `ui/present_pacing.h`, `ui/presenter.h`, `src/ui/presenter.cpp` | keep |
| 0044 | present-hold-high-water | `ui/present_hold.h`, `ui/presenter.h`, `src/ui/presenter.cpp` | keep |
| 0045 | present-hold-repeats | `ui/presenter.h`, `src/ui/presenter.cpp` | keep |
| 0046 | profile-name | `system/xam/user_profile.h`, `src/system/xam/content_manager.cpp` | keep |

Consequence for the port: the patch series does **not** need to be forked for Android. Keep the
series identical to the Windows port (that also keeps the two ports in lockstep), and put the
Android-specific work in *new* files plus a small set of `ANDROID` guards that the patch series
does not touch (top-level CMake, `src/ui/CMakeLists.txt`, `window_sdl.cpp` include block,
`src/core/memory_posix.cpp` ashmem block). The three files where patches and Android edits would
collide are `src/ui/window_sdl.cpp` (0023, 0038 — both near the top of the file and in
`OpenImpl`, while the Android change is in the `#if` block at :31-36 and in `CreateSurfaceImpl`
at :373-423, so they can coexist), `src/system/kernel_state.cpp` (0027, 0028 — path semantics
only) and `src/graphics/graphics_system.cpp` (0013/0019/0024/0025 — no platform code).

---

## Verdict

**Feasible with work.** No architectural blocker: AOT C++ recompilation (no x86 emitter), simde
for vectors, a complete arm64 runtime path with CI, a Vulkan presenter that already speaks
`VK_KHR_android_surface`, and an ashmem-backed guest memory scheme copied from Xenia's own
Android port. The Android port is *incomplete and untested*, not absent: it fails to configure,
references one missing header, and misses two small helper symbols.

Rough sizing: ~3-5 days of SDK-side work to get `librexruntime.so` + `librexgpu-xenos.so`
building for arm64-v8a, plus ~1-2 weeks of app shell + device bring-up.

### Ranked work items

1. **CMake Android platform branch** — `CMakeLists.txt:142-181` (add `ANDROID` before the
   `UNIX` branch: `REX_PLATFORM=android-arm64`, `REX_PLATFORM_ANDROID=1`), exclude
   `src/rexglue` (:263), separate output dir (:226), keep `-mcmodel=small`.
   Also `src/ui/CMakeLists.txt:138-152` (drop pkg-config/X11/Wayland),
   `src/core/CMakeLists.txt:104` (drop `-lrt`), `thirdparty/CMakeLists.txt:293-302` (no
   ALSA/Pulse/PipeWire/X11/Wayland) and :251 (`SDL_UNIX_CONSOLE_BUILD` off), Tracy off.
   *Blocks everything else.*
2. **Android surface backend** — new `include/rex/ui/surface_android.h` +
   `src/ui/surface_android.cpp` (`AndroidNativeWindowSurface`: `GetType`, `GetSizeImpl`,
   `window()`), `src/ui/window_sdl.cpp` `#elif REX_PLATFORM_ANDROID` include + a
   `CreateSurfaceImpl` branch on `SDL_PROP_WINDOW_ANDROID_WINDOW_POINTER`, and an
   `elseif(ANDROID)` in `src/ui/CMakeLists.txt:66-76`. Without it the Vulkan presenter does not
   compile.
3. **Missing Android symbols / filesystem** — define `rex::GetAndroidApiLevel()`, delete the
   `#include <linux/ashmem.h>` fallback (`src/core/memory_posix.cpp:54,445-456`), implement
   `OpenAndroidContentFileDescriptor` (`include/rex/filesystem.h:133`), and make
   `GetExecutableFolder()`/`GetUserFolder()` return `nativeLibraryDir`/`filesDir`
   (`src/core/filesystem_posix.cpp:94-121`) so the GPU plugin and saves resolve.
4. **Android app shell** — Gradle + `Activity`/`SurfaceView` (or `GameActivity`), `SDL_main`
   entry, package `librexruntime.so`, `librexgpu-xenos.so` and the recompiled game `.so`,
   asset/content-URI access for the disc image, and the JNI init order (SDL3 already registers
   `JNI_OnLoad`).
5. **First full arm64-v8a NDK build** (r27/r28) and fix the remaining POSIX compile breaks
   (`src/core/platform/*`, `dynlib_posix.cpp`, `socket_posix.cpp`, `system_posix.cpp`,
   `filesystem_posix.cpp`) — expected to be small but not zero, since none of this has ever been
   compiled for bionic.
6. **Runtime bring-up on device, in this order**: 4.5 GB guest arena maps at a 4 GB-aligned base
   (`src/system/xmemory.cpp:154-174`); GPU plugin `dlopen`; swapchain + first frame; MMIO fault
   handling (`src/system/mmio_handler.cpp` arm64 path); SEH-throws-from-signal
   (`src/core/seh_posix.cpp`) under real guest traps. Confirm the device's page size (4 KB vs
   16 KB) before trusting `PhysicalHostOffset()` (`include/rex/system/xmemory.h:41-48`).
7. **Vulkan/Android tuning** — default `vulkan_require_geometry_shader` and
   `vulkan_require_fill_mode_non_solid` to false on Android (`src/ui/vulkan/vulkan_device.cpp:35-41`;
   most Mali parts lack geometry shaders), verify present-mode choice
   (`vulkan_presenter.cpp:1382-1404`), and handle `VK_ERROR_SURFACE_LOST_KHR` on
   pause/resume/rotation.
8. **Re-apply the 46-patch series** on top of the Android CMake changes (order-preserving, as
   today), then diff the Windows and Android build scripts so the two stay in lockstep.
9. **Performance pass** (after correctness) — `-march=armv8.2-a+dotprod` vs `armv8-a`, JIT-less
   hot-path tuning, resolution scaling; note `display_vblank` has no Android implementation, so
   the "smooth presentation" pacing path (0013) degrades to its fallback.


---

## Build status (2026-11-03)

**The runtime libraries compile and link for Android arm64-v8a.** Verified twice: once from
an empty clone and empty build directory (748 targets, exit 0) with the first 18 commits, then
again after deleting the working copy and re-applying the whole 25-commit series with
`prepare-sdk.sh` and rebuilding (`ninja -k 0`, exit 0, no errors; ~600 warnings, almost all
`-Wunused-parameter` in third-party code).

| Output | Size (Debug) | Notes |
|---|---|---|
| `out/android-arm64/librexruntimed.so` | 111 MB | core, filesystem, ui, input, audio, graphics, system, kernel |
| `out/android-arm64/librexgpu-xenosd.so` | 65 MB | the Vulkan/Xenos GPU plugin |

Both are ELF 64-bit ARM aarch64, `NEEDED libc++_shared.so`, `libandroid.so`, `liblog.so`,
`libdl.so`, and `LOAD` segments aligned to `0x4000` (16 KB-page devices). The host-only
`rexglue` CLI and the `rexcodegen` static library (112 MB) are **not** built for Android.

### Exact commands

```bash
export ANDROID_NDK_HOME=$HOME/Library/Android/sdk/ndk/30.0.16248370   # NDK r30, clang 21

# 1. working copy: _ref/rexglue-sdk @ f5337cd + 46 reference patches + 25 Android patches
ts4-port/scripts/prepare-sdk.sh          # idempotent; resets to f5337cd and re-applies both series

# 2. configure (SDK standalone; the recompiler is excluded on Android)
cmake -S work/rexglue-sdk -B work/build/rexglue-android -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake \
  -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 -DANDROID_STL=c++_shared \
  -DCMAKE_BUILD_TYPE=Debug

# 3. build
ninja -C work/build/rexglue-android -k 0
```

`ts4-port`'s own `android-arm64-debug` preset + `-DTS4_WITH_REXGLUE=ON -DREXSDK_DIR=<work copy>`
is the intended consumer path, but the app target is not part of this step (see "Left").

Logs: `work/logs/sdk-android-<N>.log` (the successful from-scratch run is `sdk-android-15.log`).

### What the 25-patch series does

Series: `ts4-port/patches/rexglue-android/` (now 26 patches; regenerate with
`ts4-port/scripts/export-sdk-patches.sh`). Licences and provenance: `THIRD_PARTY.md` there.

Build system

1. OBJC/OBJCXX enabled after `project()` (`APPLE` is host-based before the toolchain loads;
   a macOS host cross-compiling to Android asked for Objective-C compilers).
2. `ANDROID` platform branch: `REX_PLATFORM=android-arm64`, own output dir, no
   `REX_PLATFORM_LINUX` from CMake, Tracy off by default.
3. `src/rexglue` (host recompiler) and `src/codegen` not cross-compiled; their install rules
   made conditional.
4. `src/ui`: no pkg-config/X11/Wayland; `surface_android.cpp` selected; links `android`.
5. `src/core`: no `librt`, no `libpthread` (bionic has them in libc); links `dl android SDL3`.
6. `thirdparty`: SDL's X11/Wayland/ALSA/Pulse/PipeWire backends off, `SDL_UNIX_CONSOLE_BUILD`
   off on Android.
7. `-Wl,-z,max-page-size=16384` for 16 KB-page devices.

Platform layer

8. `include/rex/ui/surface_android.h` + `src/ui/surface_android.cpp`:
   `AndroidNativeWindowSurface` over `ANativeWindow` (size from `ANativeWindow_getWidth/Height`).
9. `window_sdl.cpp`: `#elif REX_PLATFORM_ANDROID` include and `CreateSurfaceImpl` branch on
   `SDL_PROP_WINDOW_ANDROID_WINDOW_POINTER`.
10. Background/foreground: detach the presenter on `WILL_ENTER_BACKGROUND`, reconnect it when a
    foreground/window event reports a new `ANativeWindow` (otherwise: black screen after an
    app switch).

Missing symbols / bionic

11. `rex::GetAndroidApiLevel()` + `include/rex/main_android.h` (`android_get_device_api_level`).
12. `ASharedMemory_create` direct + `memfd_create` syscall fallback; no `/dev/ashmem`;
    bionic 64-bit `off_t`.
13. `OpenAndroidContentFileDescriptor`/`IsAndroidContentUri` over JNI (SDL activity's
    `ContentResolver` + `detachFd`).
14. `GetExecutableFolder()`: `REX_EXECUTABLE_FOLDER`/`REX_APP_FOLDER`, else `dladdr` on the
    runtime's own library (= `nativeLibraryDir`); `GetUserFolder()` = app internal storage.
15. GPU plugin loaded by soname on Android (`librexgpu-xenos.so` via the app's linker namespace).
16. Recompiled guest modules loaded by Android soname candidates (`lib<name>.so`, `<name>.so`,
    then `<name>`); the Windows path continues loading the registered name directly.
17. `chrono`: the `clock_time_conversion` fallback for Android libc++ (it has no `clock_cast`).
18. Fibers: `src/core/fiber_android.cpp`, an AAPCS64 context switch (x19-x28, x29, x30, sp,
    d8-d15) — bionic has no `getcontext`/`makecontext`/`swapcontext` on 64-bit.
19. No robust mutexes (`PTHREAD_MUTEX_ROBUST` does not exist in bionic).
20. SEH: SIGBUS treated as an access violation on Android; unclaimed SIGSEGV/SIGBUS/SIGILL
    forwarded to the previous handler (or `SIG_DFL` + re-raise) instead of faulting forever.
21. `"a+"` streams rewound after `fopen` (bionic starts reads at EOF; shader storage truncated).

### Left (deliberately out of scope here)

- **App shell** (`ts4-port/android/`, owned by another agent): Gradle + SDLActivity,
  `libSDL3.so`, packaging `librexruntime.so` / `librexgpu-xenos.so` / the game `.so`,
  game-data import, controller mapping.
- **SDL static vs shared.** This series keeps `SDL_STATIC ON` (the SDK's default), which is
  enough to compile. Gears' 0007 builds `libSDL3.so` on Android because SDLActivity loads it
  first and it owns the JVM/activity state; a static SDL inside `librexruntime.so` would be a
  second copy without that state (and `SDL_GetAndroidJNIEnv()` in
  `filesystem_android.cpp` would return nothing). **Switch to `SDL_SHARED ON` for Android when
  the app shell lands.**
- **`host_address_offset` must be 0 on Android.** `PhysicalHeap::Initialize`
  (`src/system/xmemory.cpp`) sets `host_address_offset = 0x1000` when
  `allocation_granularity() > 0x1000`, while recompiled code uses the compile-time
  `REX_PHYS_HOST_OFFSET` = 0 on Android (`resources/templates/codegen/pch_h.inja`). On a
  16 KB-page device every access to `0xE0000000..0xFFFFFFFF` would be 0x1000 low. Either keep
  the heap view 4 KB-aligned so the offset stays 0, or make both ends runtime. A startup
  check in `PhysicalHeap::Initialize` (`REX_PLATFORM_ANDROID && host_address_offset != 0` →
  log + abort) is the cheap guard; not added yet.
- **W^X:** `IsWritableExecutableMemorySupported()` (`src/core/memory_posix.cpp`) returns true on
  Android, but Android 10+ denies W+X anonymous mappings. Nothing allocates executable memory
  today, so it is latent; return false (as macOS does) before anything uses it.
- **`display_vblank`** has no Android implementation (patch 0013's `DisplayVblank::Open()`
  returns false), so smooth presentation uses its fallback. `AChoreographer` /
  `VK_KHR_display_timing` later.
- **Vulkan defaults:** `vulkan_require_geometry_shader` / `vulkan_require_fill_mode_non_solid`
  default to true off-macOS (`src/ui/vulkan/vulkan_device.cpp`); set them false for Adreno.
  `VK_ERROR_SURFACE_LOST_KHR` on pause/resume is handled by the reconnect path above.
- **Prior-art follow-ups worth taking** (see `THIRD_PARTY.md`): nfsmw's
  `PageProtectionTable` + `MAP_FIXED_NOREPLACE` relocation check in `memory_posix.cpp`
  (QueryProtect currently parses `/proc/self/maps` on every MMIO fault), Renut's
  `query_protect.cpp` (same goal, PLT interposition), Gears' `utf8.cpp` repair (invalid
  UTF-8 in guest names throws through guest code), Gears' SGSR1 presenter upscaler,
  `libadrenotools` driver loading, and `0015`/`0016` (old-kernel fixed mappings, Turnip
  sparse-memory default).
- **FFmpeg:** the SDK's aarch64 assembly builds with NDK r30; Gears disables it on Android
  (`thirdparty/ffmpeg-android/`) as a precaution. Not taken; revisit if XMA decode misbehaves.
- **Device bring-up** (not attempted): 4.5 GB guest arena at a 4 GB-aligned base, GPU plugin
  `dlopen`, swapchain + first frame, MMIO fault handling, SEH under real guest traps, page
  size of the target device.
