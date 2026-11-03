# Android port scope (from the Windows reference)

Read from `_ref/Top-Spin-4-Recomp` (commit at clone time). Source file and line references are
to that tree. Nothing here has been built on Android yet.

## What carries over almost as-is

- **Game translation.** The three Modules (`default.xex`, `Loader_DLL.xex`, `Swing_DLL.xex`)
  are translated to C++ by the ReXGlue recompiler, which runs on the host. The generated code
  is portable C++; only the host toolchain changes. The recompiler's own `linux-arm64` CI is
  noted in `docs/research/feasibility.md:167`. An Android target is not mentioned anywhere in
  the reference, so its support is unverified.
- **Vulkan renderer.** `src/native/` (55 files, 16 mention Vulkan) draws the game's D3D9 calls
  with Vulkan. Vulkan is the right API on Retroid. Surface and window creation are not in this
  repo, so they come from the ReXGlue SDK's platform layer, which we have not read yet.
- **Shader pack runtime.** `src/native/shader_pack.cpp` and `shader_topup.cpp` unpack
  pre-converted shaders with zstd and smol-v. These are portable C/C++ (`CMakeLists.txt`,
  `topspin4_native_deps`).
- **Texture layout.** `src/native/texture_layout.cpp` and the SDK's Xenos texture code are
  plain C++.

## What is Windows-only and must change

1. **Build system.** `CMakeLists.txt` links `dbghelp` and `shell32` under `if(WIN32)`, uses
   `add_executable(topspin4 WIN32 ...)`, and compiles `src/topspin4.rc`. The ReXGlue SDK is
   pulled in from `REXSDK_DIR` (`cmake/rexglue.cmake`). For Android it needs the NDK toolchain
   and the Android Vulkan surface.
2. **Shader converter.** XenosRecomp is built with `rc.exe` and MSVC tooling
   (`scripts/build-xenosrecomp.ps1`). Its patches (`patches/xenosrecomp/`) are portable, but
   the tool itself runs on the PC. The plan is to convert shaders on the PC and ship the pack
   to the device, so the converter never has to run on Android.
3. **Windows API use in `src/`.** About 15 files include `windows.h`, `HWND`, `HANDLE`, or
   `std::wstring`: `crash_report.cpp`, `fault_log.cpp`, `relaunch.cpp`, `user_paths.cpp`,
   `achievement_toast.cpp`, `not_set_up.cpp`, `frame_dumps.cpp`, `native/pipelines.cpp`,
   `native/topup_converter.cpp`, `settings/schema_export.cpp`, and the headers under them.
   Most of this is paths, dialogs, and crash dumps. Each one needs an Android path or a no-op.
   `frame_dumps.cpp:429` loads RenderDoc with `GetProcAddress`, which is not needed on device.
4. **Launcher.** `launcher/` is a Windows .NET app that checks the disc, copies the game files,
   and builds shaders. None of it runs on the Retroid. The disc-to-game-files step has to run
   on the PC, and the device gets a prepared data folder. This is a design choice; see below.
5. **Scripts.** 16 PowerShell scripts in `scripts/`. We need our own build and packaging
   scripts. `scripts/build-android.sh` is the first one.
6. **Input and settings.** `remapped_input.cpp` and `script_input.cpp` are the input layer. We
   have not yet checked how they read the controller. Retroid's controls will need mapping
   there. The `settings/` code is only partly reviewed.
7. **Patches.** `patches/rexglue/` (46 patches) changes the SDK's window, present, and frame
   pacing code (for example `0001-window-restore`, `0038-window-hidden`). Those patches target
   Win32 windowing. Each one has to be checked on Android, and some will be dropped.

## Decisions for you

- **Where the game data comes from.** Options: (a) the PC prepares a data folder and copies it
  to the Retroid over adb, or (b) the app reads an ISO or the extracted folder from storage on
  the device. (a) is simpler and keeps the 7.8 GB ISO off the handheld. We need to choose
  before writing the data loader.
- **Shader pack.** Ship the pre-converted pack (recommended) or run XenosRecomp on Android
  (much harder).
- **Target.** arm64-v8a, Android API 29 (`ANDROID_PLATFORM=android-29`, the value the
  DeepSeek task was given, not yet checked against the SDK). Confirm the minimum Vulkan
  version the renderer needs, and the Retroid's driver version.

## Unknowns to check first

- Does the ReXGlue SDK build for Android? Its platform layer is the biggest unknown.
- Does the SDK's Vulkan surface code have an Android path (`VK_KHR_android_surface`)?
- Can the Retroid's GPU run the native renderer's shaders and features at 60 fps?
- Does the NDK's `libc++` match what the SDK expects?

None of these can be settled by reading the code. A build that reaches the SDK's platform
layer will settle the first two.
