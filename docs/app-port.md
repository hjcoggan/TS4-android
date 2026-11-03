# App layer on Android: what was ported, stubbed and left

Status: 2026-11-03, second session. This is the `ts4-port/src` app layer (the Windows reference's
`_ref/Top-Spin-4-Recomp/src`, 169 files) behind the platform shim from
`docs/windows-api-audit.md`.

## What is here

| Part | Where | Notes |
| --- | --- | --- |
| The app layer | `src/*.cpp`, `src/native/`, `src/settings/`, `src/d3d/` | Copied from the reference at `b9285dd`. Behaviour unchanged except where the audit required a platform change. |
| Platform shim | `src/platform/` | `paths`, `dialog`, `thread`, `audio`, `fonts`, `crash`; `*_posix.cpp` for Android/macOS/Linux, `*_win32.cpp` for Windows. Shared code includes no `windows.h`. |
| Android entry | `src/android_main.cpp` | `SDL_main`, called by `TopSpin4Activity` (an `SDLActivity`). Replaces the old `android_main` placeholder. |
| Crash stub | `src/platform/crash_report_stub.cpp` | Implements `topspin4::crash` on non-Windows. |
| Vendored shader-pack deps | `src/thirdparty/zstd`, `src/thirdparty/smolv` | zstd 1.x decompression (BSD-3) and smol-v (MIT). |
| D3D hook table | `symbols/d3d-functions.csv` | Copied from the reference; `cmake/d3d_hooks.cmake` generates `d3d_functions.h` and the sink library from it. |
| Shipped data | `data/pipeline-keys.txt`, `data/sounds/achievement.wav` | Copied from the reference. Android: push `pipeline-keys.txt` into `cache/` and `achievement.wav` into `userdata/sounds/` (the Windows build copies them beside the exe instead). |
| XenosRecomp series | `data/xenosrecomp/*.patch` | The reference's 12-patch series, copied because `ts4-port/patches/` belongs to the SDK work. Its SHA-256 is `TOPSPIN4_EXPECTED_SHADER_CONVERTER_ID`, so a shader pack converted on the Mac with the same series is accepted. |

## Platform shim (audit "Suggested platform shim")

| Header | Windows | Android / POSIX |
| --- | --- | --- |
| `platform/paths.h` | exe folder (`GetModuleFileNameW`) | `REX_APP_FOLDER` → `REX_EXECUTABLE_FOLDER` → `XDG_DATA_HOME` → `HOME` → cwd. The activity sets the first two to the app's files dir. Never `/proc/self/exe`. |
| `platform/dialog.h` | `MessageBoxW`, `ShellExecuteW` | `__android_log_print` (logcat) and returns false; no AlertDialog yet. `ShowErrorAndExit` logs and `_Exit(2)`. |
| `platform/thread.h` | `SetThreadPriority` | `setpriority(PRIO_PROCESS, 0, nice)`; `kHigh` needs CAP_SYS_NICE, so it stays at 0 on Android. |
| `platform/audio.h` | `PlaySoundW(SND_ASYNC)` | No-op, logged once. The game's audio is the SDK's SDL driver; the toast WAV would need a second SDL stream. |
| `platform/fonts.h` | Segoe UI (`C:/Windows/Fonts`) | `/system/fonts/Roboto-{Regular,Bold}.ttf`, then DroidSans; ImGui's built-in font is the last resort. |
| `platform/crash.h` | `CommandLineToArgvW`, crash module | No-op handler, `WriteCrashDump` false, no arguments (the SDK's signal handler owns faults). |

Not introduced: `platform/process.h` and `platform/native_window.h`. Every Windows caller of
`CreateProcessW` is stubbed on Android (`not_set_up`, `crash_report`, `topup_converter`) or already
has a non-Windows fallback (`relaunch`), so a shared spawner would have no Android caller yet. The
window-handle helpers were only needed by the crash report's "hide the frozen window", which is
gone with the stub. Add them when the relaunch/return-to-title design is decided.

## Per-file changes (the audit's Android plan, applied)

- `user_paths.cpp` — `ExecutableFolder()` is `platform::BaseFolder()`. `user_paths.h` relaxes the
  "prepared folder" test on Android: the Mac's data-prep script writes no `.topspin4-prepared`
  marker, and a disc-only install (no Title Update) is accepted. **This was the functional
  blocker.**
- `not_set_up.cpp` — Windows branch unchanged. Android: log the reason, `_Exit(2)`. There is no
  Launcher process to hand over to.
- `crash_report.cpp` — compiled on Windows only. Android uses `platform/crash_report_stub.cpp`
  (same API, three debug cvars kept, forced crash/abort still crash).
- `fault_log.cpp` — unchanged; its `#else` branch is already `Install() {}`. (A later task can add
  `_Unwind_Backtrace` + `dladdr`.)
- `relaunch.cpp` — unchanged; its non-Windows `OnGuestExit` logs "not supported here".
- `frame_dumps.cpp` — unchanged; `_wfopen`, `VirtualQuery` and RenderDoc are already behind
  `#if defined(_WIN32)`.
- `settings/schema_export.cpp` — unchanged; `CommandLineToArgvW`/`AttachConsole` are behind
  `REX_PLATFORM_WIN32`, so the switch is never seen on Android.
- `achievement_toast.cpp` — `PlaySoundW` → `platform::PlaySoundFileAsync`; the two Segoe UI paths →
  `platform::SystemFontPath`.
- `settings/settings_page.cpp` — Segoe UI Bold fallback → `platform::SystemFontPath(true)`.
- `native/pipelines.cpp` — `SetThreadPriority` → `platform::SetCurrentThreadPriority`.
- `native/topup_converter.cpp` — `Configure()` returns immediately on Android (no XenosRecomp.exe,
  no job objects); `DWORD kTimeoutMs` is now Windows-only. Shaders come from the pre-converted
  pack.
- `native/host_paths.h` — `OpenFile` delegates to `platform::OpenFile`; `PathText`/`TextPath`
  unchanged.
- `d3d/sink.h` — added `<cstddef>` (it used `size_t` through a Windows-only transitive include).
- `topspin4.rc` — Windows only.

## Entry point and CMake

- Android is a **shared library**, `libtopspin4.so`. `TopSpin4Activity` extends `SDLActivity` and
  loads `topspin4`; SDL3's Java layer calls `SDL_main`, which `src/android_main.cpp` provides
  (`<SDL3/SDL_main.h>` renames `main` on Android).
- `cmake/rexglue.cmake` therefore does **not** call the SDK's `rexglue_configure_target()` on
  Android: that adds `src/ui/windowed_app_main_sdl.cpp`, whose plain `main()` SDLActivity never
  calls. It adds `src/ui/rex_app.cpp`, the build-config define, the target settings and the
  `rexgpu-xenos` build dependency instead. (Same approach as `codepdbh/nfsmw-android`, which
  patched the SDK file to include `SDL_main.h`; here the SDK tree stays untouched.)
- Codegen is not run during the Android build (the SDK does not cross-compile `src/rexglue`). The
  generated sources in `generated/boot` and `generated/{loader,swing}` are compiled as they are;
  `topspin4_codegen` is a no-op target so the generated `dll_targets.cmake` dependencies resolve.
  The boot sources are compiled **once**, into `rexglue_setup_target()`'s `topspin4_recomp` object
  library — adding them to the app target too duplicates every guest symbol at link time (seen and
  fixed in `app-android-5.log`).
- `TS4_WITH_REXGLUE=OFF` still builds a placeholder (`src/host_main.cpp`), host or Android, so the
  presets and the NDK work without the SDK.
- Without `generated/boot/sources.cmake`, `TOPSPIN4_NO_GUEST_CODE` selects `NoGuestCodeApp` in
  `src/main.cpp` (the reference's ticket-03 path): the `.so` links, starts and quits. Guest
  function symbols are only referenced by generated code, so nothing else needs stubbing.

## Build

```bash
export ANDROID_NDK_HOME=$HOME/Library/Android/sdk/ndk/30.0.16248370
cd ts4-port
cmake --preset android-arm64-rex-debug          # adds work/rexglue-sdk as a subdirectory
ninja -C ../work/build/ts4-android-rex-debug -k 0
```

`REXSDK_DIR` can be overridden on the configure line. The first build compiles the whole SDK
(~750 targets); later builds only the app, the two generated Modules and the boot module.

Without `generated/` (verified in a copy of the tree, `work/build/ts4-nogen-src` →
`work/build/ts4-android-nogen`, log `app-android-7-nogen.log`): the same configure and build work,
`TOPSPIN4_NO_GUEST_CODE` selects `NoGuestCodeApp`, no Loader/Swing targets exist, and the result is
a 38 MB `libtopspin4.so` with no guest symbols and an exported `SDL_main`.

Outputs (Debug): `libtopspin4.so` (41 MB), `libtopspin4_Loader_DLL.so` (94 MB),
`libtopspin4_Swing_DLL.so` (191 MB), `libtopspin4_d3d_sink.so`, plus the SDK's
`librexruntimed.so` and `librexgpu-xenosd.so` under `work/rexglue-sdk/out/android-arm64/`.
The `d` before `.so` is the SDK's Debug postfix; a Release build has none.

Verified 2026-11-03: `libtopspin4.so` links with the SDK and the generated guest code, ELF64
AArch64, `LOAD` aligned to `0x4000`, `NEEDED librexruntimed.so`, and exports `SDL_main`. Logs:
`work/logs/app-android-5.log` (first successful link), `app-android-6.log` (after the duplicate
guest-symbol fix), `app-android-8.log` (final), `app-android-7-nogen.log` (build without
`generated/`).

**Load order matters.** SDL is linked statically into `librexruntimed.so`, so SDL's `JNI_OnLoad`
(which captures the JavaVM) lives there. ART calls `JNI_OnLoad` only for the library passed to
`System.loadLibrary()`, so `TopSpin4Activity.getLibraries()` must be
`{ "rexruntimed", "topspin4" }` in Debug (`{ "rexruntime", "topspin4" }` in Release), and all
five `.so`s plus `libtopspin4_d3d_sink.so` must be in the APK's `lib/arm64-v8a`. `android_main.cpp`
logs an error when it starts without a JavaVM. (Switching the SDK to `SDL_SHARED=ON` for Android
would remove this constraint; it is an SDK change, not made here.)

Logs: `work/logs/app-android-<N>.log`.

## Provenance (private research build; never distributed)

| Taken | From | Licence |
| --- | --- | --- |
| `src/` app layer, `symbols/d3d-functions.csv`, `data/*`, `data/xenosrecomp/*.patch`, `cmake/d3d_hooks.cmake` | `_ref/Top-Spin-4-Recomp` (the project's own Windows port) | no LICENSE file in that repo; project-internal |
| `src/thirdparty/zstd` (decompression subset) | `_prior-art/Renut-Android/thirdparty/XenosRecomp/thirdparty/zstd`, itself vendored from facebook/zstd | BSD-3-Clause (`LICENSE`) |
| `src/thirdparty/smolv` | same `XenosRecomp/thirdparty/smol-v` | MIT (`LICENSE.txt`) |
| `src/android_main.cpp` | `work/rexglue-sdk/src/ui/windowed_app_main_sdl.cpp`, minus Win32 | BSD-3-Clause (Tom Clay) |
| Idea: app entry must be `SDL_main` on Android | `_prior-art/nfsmw-android/sdk/src/ui/windowed_app_main_sdl.cpp` | GPL-3.0 (idea only, no code copied) |

Nothing from `_prior-art` was built or executed.

## Remaining

1. Package and run: Gradle shell, `librexruntimed.so` + `librexgpu-xenosd.so` + the three game
   `.so`s + `libtopspin4_d3d_sink.so`, and the data push (`gamedata/`, `updatedata/`,
   `userdata/sounds/`, `cache/pipeline-keys.txt`). The Loader/Swing Modules are
   `libtopspin4_Loader_DLL.so` / `libtopspin4_Swing_DLL.so` and are loaded by soname, so they must
   be in the APK's `lib/arm64-v8a` (Gradle `jniLibs`), not in the assets.
2. Device bring-up: 4 GB-aligned guest arena, GPU plugin `dlopen`, swapchain + first frame, MMIO
   faults, SEH-by-signal, page size (`adb shell getconf PAGE_SIZE`).
3. Vulkan defaults for Adreno: `vulkan_require_geometry_shader` and
   `vulkan_require_fill_mode_non_solid` off (SDK-side).
4. Crash report on Android: signal handler + `_Unwind_Backtrace` + `dladdr`, zip with
   `crash_zip.h`, share Intent.
5. UI sounds (`platform/audio.h`), an AlertDialog for `platform/dialog.h`, and `hid_mappings_file`
   at an absolute path (`gamecontrollerdb.txt` for the Retroid pad).
6. Revisit `platform/process.h` when return-to-title is designed.
