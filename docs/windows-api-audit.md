# Windows API audit — `_ref/Top-Spin-4-Recomp/src/`

Status: complete for `src/`, `src/native/`, `src/settings/`. Read-only reference tree at
`_ref/Top-Spin-4-Recomp`; our tree is `ts4-port`. All line numbers are reference-tree lines.

Method: exhaustive `grep` over `src/` for `windows.h`, `winbase/winuser/shellapi/dbghelp/mmsystem`,
`HWND/HANDLE/HMODULE/HINSTANCE/HKEY/DWORD/BOOL/LONG/...`, `std::wstring/wchar_t/L"..."`,
`_WIN32/WIN32/_MSC_VER`, `#pragma comment`, MSVC intrinsics (`_byteswap_ulong`, `__declspec`),
Win32 process/file/dialog/shell APIs, `GetProcAddress`, registry, COM, XInput/DirectInput,
`MessageBox`, and wide-path CRT calls. Every hit was then read in context. `rx`/`rg` was not
installed; the editor's grep was used.

Effort: **S** = localised change, **M** = new small platform implementation, **L** = whole
subsystem / design decision.

## Summary table

| File | Windows usage (lines) | Android plan | Effort |
|---|---|---|---|
| `src/achievement_toast.cpp` | `#ifdef _WIN32` + `windows.h`, `mmsystem.h` (14–22); `#pragma comment(lib, "winmm.lib")` (20); `PlaySoundW` (75); hard-coded `C:/Windows/Fonts/segoeui.ttf`, `seguisb.ttf` (82–83) | Guard the sound behind `platform/audio.h` (SDL audio / AAudio, or no-op); replace font fallback with the game font / Android system font (`/system/fonts/...`). The toast itself is ImGui, portable. | S |
| `src/not_set_up.cpp` | Whole file is Win32: `windows.h` (19); `wchar_t` launcher name (25); `std::wstring`/`QuoteArgument` (27–39); `STARTUPINFOW` + `CreateProcessW` (41–47); `CloseHandle` (48–49); `MessageBoxW` + `MB_*` (62–68) | Drop the Launcher relaunch on Android. Replace with `platform/dialog.h::ShowError` (Android AlertDialog via JNI or the SDK's `ShowSimpleMessageBox`) + log, then exit. The “not set up” condition itself is portable (`user_paths::SetupNeededReason`). | M (or S to stub) |
| `src/crash_report.cpp` | `windows.h`, `dbghelp.h`, `shellapi.h` (22–32); `HWND/DWORD` atomics (50–52); `GetCurrentThreadId/Sleep/TerminateProcess` (64–72); `CreateFileW/ReadFile/GetFileSizeEx/CloseHandle` (75–91); `localtime_s` (96); `BOOL CALLBACK` + `MINIDUMP_*` (113–124); `VirtualQueryEx` (127–143); `OpenProcess` + `MiniDumpWriteDump` (147–172); `MultiByteToWideChar`/`WideCharToMultiByte` (175–187); `CreateProcessW` helper (192–219); `ShowWindowAsync` (262); `MessageBoxW` + `ShellExecuteW("explorer.exe")` (266–269); wide path ops (275–281); `CreateFileW/WriteFile` (297–318); `LONG WINAPI` SEH filter (337–347); `RaiseException` (351–354); `CommandLineToArgvW/GetCommandLineW/LocalFree` + `SetUnhandledExceptionFilter` + `_set_*_handler` (364–381); `SetWindow(HWND)` (383) | **Decision needed.** Either (a) stub the whole module on Android (`Install()` = no-op, keep debug crash cvars), or (b) port a native crash writer: `signal`/`sigaction`, `_Unwind_Backtrace`, `libunwindstack`/`debuggerd` tombstone, zip via existing `crash_zip.h`, Android `share` Intent instead of `explorer.exe`. MiniDump/dbghelp has no Android equivalent. Keep `crash_report_plan.h`/`crash_zip.h` (pure, portable). | L (full port) / S (stub) |
| `src/crash_report.h` | Only a comment says the window is an `HWND` (21); API takes `void*` (22) | Keep the opaque `void*` API; on Android pass `nullptr` (the SDK already returns `nullptr` from `Window::GetNativeWindowHandle` off Windows). | S |
| `src/crash_dump_request.h` | Pure command-line/struct helper, but its comments document `MiniDumpWriteDump`/`VirtualQueryEx`/`EXCEPTION_POINTERS` (1–5, 25, 61) | Compile as-is only if a dump helper exists; otherwise drop with `crash_report`. No Win32 calls. | S |
| `src/fault_log.cpp` | `#if defined(_WIN32)` around `windows.h`/`dbghelp.h` (9–13); `HANDLE/DWORD64` + `SYMBOL_INFO`/`SymFromAddr`/`SymGetLineFromAddr64`/`SymGetModuleInfo64` (25–40); `LONG CALLBACK` + `EXCEPTION_POINTERS`/`CONTEXT`/`STACKFRAME64` + `StackWalk64` (42–75); `AddVectoredExceptionHandler` (79) | Already portable: the `#else` branch is `void Install() {}` (80–82). On Android either keep the no-op or add `_Unwind_Backtrace` + `dladdr` logging. dbghelp is not available. | S |
| `src/relaunch.cpp` | `#ifdef _WIN32` + `windows.h`/`shellapi.h` (23–29); window enumeration `HWND/RECT/EnumWindows/GetWindowThreadProcessId/IsWindowVisible/GetWindowRect/GetForegroundWindow` (52–106); `CreateEventW` + `GetCommandLineW`/`CommandLineToArgvW` (111–157); `AllowSetForegroundWindow` + `CreateProcessW` + `WaitForMultipleObjects` + `SetForegroundWindow` (159–195); `GetNativeWindowHandle`→`HWND`, `SetWindowPos`, `ShowWindow`, `OpenEventW/SetEvent` (201–225); calls guarded at 256–259 and 267–276 | Already has a non-Windows fallback: `OnGuestExit` logs “not supported here” (273–276). On Android keep that (or use an explicit “return to title” design); no process hand-over/focus stealing. Drop `relaunch_plan.h`/`command_line.h` wide-string helpers with it. | S (drop) |
| `src/relaunch.h` | Says “Windows only” (11); rest is portable declarations | Keep declarations or compile out with `relaunch.cpp`. | S |
| `src/relaunch_plan.h` | `std::wstring_view` flags and `std::vector<std::wstring>` argument filtering (18–58); Windows command-line semantics | Drop on Android with `relaunch.cpp`; if kept, convert to UTF-8 `std::string`. | S |
| `src/frame_dumps.cpp` | `#if defined(_WIN32)` + `windows.h` + RenderDoc header (22–28); `_wfopen` wide path (80–87); `VirtualQuery`/`MEM_*`/`PAGE_*` + `_byteswap_ulong` in memory dump (141–188); RenderDoc capture gated (194–208); `GetModuleHandleA("renderdoc.dll")` + `GetProcAddress` (424–438) | Development/scene-run feature only. Keep the existing `#if defined(_WIN32)` guards; on Android the mem-dump and RenderDoc paths are already compiled out. Replace `_wfopen` with `std::fopen`/`native::OpenFile` (UTF-8 paths work on Android). Optionally implement frame/memdump later with `/proc/self/maps`; not needed for play. | S |
| `src/settings/schema_export.cpp` | `#if REX_PLATFORM_WIN32` + `windows.h`/`io.h`/`shellapi.h` (14–26); `CommandLineToArgvW`/`GetCommandLineW`/`LocalFree` (34–44); `AttachConsole`/`_fileno`/`freopen("CONOUT$")`/`_setmode`/`_O_BINARY` (49–57) | `--print-settings-schema` is a launcher/tooling feature. On Android, either stub it (return early) or feed arguments from Java/SDL; stdout is already usable. No console attach. | S |
| `src/settings/settings_page.cpp` | Windows font fallback `"C:/Windows/Fonts/segoeuib.ttf"` (553) | Use the game's font first (already does); fallback to the Android system font (e.g. `/system/fonts/Roboto-Regular.ttf`) or ImGui's built-in font. The file otherwise uses portable paths/ImGui/`InputSystem`. | S |
| `src/native/pipelines.cpp` | `#ifdef _WIN32` + `windows.h` (22–24); `SetThreadPriority`/`THREAD_PRIORITY_*` in `ScopedPriority` (649–656) | Add `platform/thread.h::SetCurrentThreadPriority(kLow/kHigh)`; Android implementation `setpriority(PRIO_PROCESS, 0, ...)` or `sched_setscheduler`; otherwise no-op. Pipeline build works unchanged. | S |
| `src/native/topup_converter.cpp` | `#ifdef _WIN32` + `windows.h` (16–18); `DWORD` timeout (43); `HANDLE job` (56–58); `GetModuleFileNameW` (66–74); `CreateFileW` + `STARTUPINFOEXW` + `InitializeProcThreadAttributeList`/`UpdateProcThreadAttribute` + `CreateProcessW` + job objects + `WaitForSingleObject`/`TerminateProcess`/`GetExitCodeProcess` (84–159); `CreateJobObjectW`/`SetInformationJobObject` (270–277) | Live shader conversion shells out to `XenosRecomp.exe`; Android has no such binary and no job objects. Disable live top-ups on Android (`Configure()` early return / `enabled=false`); ship a pre-converted shader pack instead. The `#else` already returns “live conversion runs on Windows only”, but `Configure()` still probes for the `.exe`. | S (stub) / M (port via `posix_spawn`) |
| `src/native/host_paths.h` | `_wfopen` under `_WIN32` (23–29) | Keep the `#else` `std::fopen` branch; Android uses UTF-8 paths natively. This header is the right place to centralise the rule. | S |
| `src/native/live_topup.cpp` | Searches for `XenosRecomp.exe` (265–285); no `windows.h` | On Android `FindConverter` simply returns `nullopt` once the `.exe` is absent; make that explicit/no-op and avoid scanning. | S |
| `src/d3d/sink.h` | `__declspec(dllexport/dllimport)` under `_WIN32` (19–24) | Already has the portable `__attribute__((visibility("default")))` branch (25–27). Android loads the two guest Module `.so`s; keep visibility default. | S (no change) |
| `src/command_line.h` | Whole helper quotes a **Windows** command line for `CommandLineToArgvW`/CRT parsing (1–34); wide `std::wstring` | Only used by `not_set_up.cpp`/`relaunch.cpp`. Drop on Android, or keep as a Windows-only TU. Shared code should use `argv`/`std::string`. | S |
| `src/topspin4_app.h` | Passes `window()->GetNativeWindowHandle()` to `crash::SetWindow` as an `HWND` (328); otherwise SDK abstractions | On Android `GetNativeWindowHandle()` is `nullptr`; guard the callback or make it a no-op. No direct Win32 call. | S |
| `src/remapped_input.cpp` | `#if REX_PLATFORM_WIN32` around the XInput include (15–17) and XInput driver construction (64–68); link to SDL otherwise | No change needed for Android: the XInput branch compiles out; SDL is the only controller driver (see Input path). Verify `REX_PLATFORM_ANDROID` selects SDL. | S |
| `src/thirdparty/renderdoc_app.h` | Platform typedefs (`WIN32`/`_MSC_VER`, `linux`, `apple`) at 36–45; comments mention `HWND` (495) and `GetProcAddress/dlsym` (852) | Vendored header only; included from `frame_dumps.cpp` under `_WIN32`. Keep as-is; not compiled on Android. | S (no change) |
| `src/topspin4.rc` | Windows version resource: `#include <winver.h>`, `VS_VERSION_INFO`, `VOS_NT_WINDOWS32`/`VFT_APP`, `OriginalFilename "topspin4.exe"` (1–31); compiled only by `enable_language(RC)` under `if(WIN32)` | Drop from the Android source list; Android versioning belongs in `AndroidManifest.xml`/Gradle. | S |
| `src/native/native_renderer.cpp` | `localtime_s` under `_WIN32` (1053–1057), `localtime_r` otherwise | Already portable; no change. | S (no change) |
| `src/settings/settings_file.cpp` | Only a comment about reading through the wide path (22–23); actual read is `std::ifstream` | No code change; update/remove the comment when `host_paths` is unified. | S (no change) |
| `src/user_paths.cpp` / `src/user_paths.h` | Uses SDK `rex::filesystem::GetExecutableFolder()` (cpp 7–15); includes `native/host_paths.h` (h 16). No Win32 API, but on Android `GetExecutableFolder()` resolves `/proc/self/exe` = `/system/bin/app_process*` | Replace the base-folder lookup with `platform/paths.h::AppDataFolder()` / `BaseFolder()` backed by the Android app's `internalDataPath` / `SDL_GetBasePath()`. Keep the folder layout (`userdata`, `gamedata`, `updatedata`, `cache`). This is a **functional blocker**: the portable-install assumption (“beside the exe”) does not hold on Android. | M |
| `src/native/frame_dumps.h` | Accepts both `/` and `\` in `dir` (45) | No change. | S (no change) |
| `src/module_shaders.cpp` | `"game:\\Loader_DLL.xex"` / `"game:\\Swing_DLL.xex"` (28) are **guest** VFS paths, not host Windows paths | No change; VFS mapping is the SDK's. | S (no change) |
| `src/achievement_sound.h` | Only comments use `userdata\achievement.wav` / `sounds\achievement.wav`; code uses `std::filesystem` | No change. | S (no change) |

### Files checked with no Windows dependency

`src/main.cpp`, `src/d3d/census.cpp`, `src/d3d/host.cpp`, `src/d3d/sink.cpp`, `src/d3d/hooks.h`,
`src/cutaway_rate*.{h,cpp}`, `src/load_flash*.{h,cpp}`, `src/movie_clock*`, `src/module_shaders.*`,
`src/persist_display_hook.cpp`, `src/script_input.{h,cpp}` (XInput constants only; see Input path),
`src/pad_bindings.h`, `src/settings/open_combo.h`, `src/settings/registry.h`,
`src/settings/settings_cvars.cpp`, `src/settings/presets.h`, `src/settings/schema.h`,
`src/settings/config_file.h`, `src/settings/page_model.h`, `src/settings/page_style.h`,
`src/settings/main_menu_*`, `src/native/*` except the files above, `src/thirdparty/renderdoc_app.h`
(guarded). They use only the C++ standard library, Vulkan, ImGui, fmt, and the ReXGlue SDK.

### Adjacent build-system usage (outside `src/`)

`CMakeLists.txt` uses `if(WIN32)`, `add_executable(... WIN32 ...)`, `src/topspin4.rc`,
`enable_language(RC)`, and links `dbghelp shell32` (49–59, 148–149). `winmm` is pulled in by
`#pragma comment` in `achievement_toast.cpp`. None of that applies to Android; the
`TS4_WITH_REXGLUE`/`ANDROID` path in `ts4-port/CMakeLists.txt` already branches, but the Windows
source files above are not yet copied into the port.

## Input path

### Call chain (guest → pad)

1. **Guest code** calls the Xbox 360 imports `XamInputGetState` / `XamInputGetKeystroke`
   (`XamInputGetStateEx`, `XamInputSetState` for rumble). These are implemented in the SDK at
   `_ref/rexglue-sdk/src/kernel/xam/xam_input.cpp:95` and `:136`; they call
   `runtime->input_system()->GetState(user_index, ...)` / `GetKeystroke(...)`.
2. **`rex::input::InputSystem`** (`_ref/rexglue-sdk/src/input/input_system.cpp`) owns a list of
   `InputDriver`s, enumerates their devices, and uses `SlotAssignment` to map device IDs to Xbox
   user indexes (player 1..4). `ReXApp` attaches the window after creation
   (`_ref/rexglue-sdk/src/ui/rex_app.cpp:244`) and the default factory is
   `CreateDefaultInputSystem` (`:311`).
3. **Default drivers** (`_ref/rexglue-sdk/src/input/input_system.cpp:322–357`):
   - `sdl::SDLInputDriver` **always**, on every platform (the SDK builds
     `sdl/sdl_input_driver.cpp` on Windows and non-Windows; `src/input/CMakeLists.txt:33–36`).
   - `xinput::XinputInputDriver` **only on Windows**, and only when `input_backend == "xinput"`
     (`REX_PLATFORM_WIN32` guard).
   - `mnk::MnkInputDriver` (keyboard/mouse to pad emulation) always.
   - `nop::NopInputDriver` as the fallback/synthetic device.
   - `input_backend` defaults to `"sdl"` (`input_system.cpp:27`).
4. **This port replaces the factory** in `TopSpin4App::OnPreSetup`
   (`src/topspin4_app.h:232`) with `topspin4::InstallRemappedInput`
   (`src/remapped_input.cpp:98–115`). It builds the same system but wraps the controller driver in
   `RemappedDriver`:
   - `CreateRemappedInputSystem` (`remapped_input.cpp:59–87`) chooses XInput only under
     `REX_PLATFORM_WIN32` when `input_backend == "xinput"`; otherwise SDL when
     `input_backend == "sdl"`; then MnK and NOP.
   - `RemappedDriver::GetDeviceState` (`:140–148`) asks the inner driver for the physical
     `X_INPUT_STATE`, applies the Back+Start combo gate (`settings/open_combo.h`), then
     `pad::Remap` from `src/pad_bindings.h` (the `ts4_pad_*` bindings).
   - `RemappedDriver::GetDeviceKeystroke` (`:185–215`) filters/reorders `VK_PAD_*` keystrokes and
     remaps them with `pad::RemapKeystroke`; this is what XUI/menus and the intro videos read.
   - `RemappedDriver::SetDeviceVibration` (`:181–183`) simply forwards rumble.
5. **`SDLInputDriver`** (`_ref/rexglue-sdk/src/input/sdl/sdl_input_driver.cpp`) is the actual
   hardware source on Android:
   - `Setup()` checks the SDL version; `OnWindowAvailable` runs on the UI thread and calls
     `SDL_InitSubSystem(SDL_INIT_EVENTS)` and `SDL_InitSubSystem(SDL_INIT_GAMEPAD)`, adds an
     `SDL_AddEventWatch`, and optionally loads `hid_mappings_file` (default
     `gamecontrollerdb.txt`) via `SDL_AddGamepadMappingsFromFile`.
   - It handles `SDL_EVENT_GAMEPAD_ADDED/REMOVED`, `SDL_EVENT_GAMEPAD_AXIS_MOTION`,
     `SDL_EVENT_GAMEPAD_BUTTON_DOWN/UP`; axes are written straight into `X_INPUT_STATE.gamepad`
     (left/right sticks; triggers are `axis >> 7`), buttons are packed into the XInput button mask.
   - `GetDeviceState` returns the cached `X_INPUT_STATE`; `GetDeviceKeystroke` synthesises
     `VK_PAD_*` keystrokes from button changes; `SetDeviceVibration` calls `SDL_RumbleGamepad`.
6. **Scripted runs** bypass all of the above: `InstallScriptInput`
   (`src/script_input.cpp:111–139`) sets `config.input_factory` to an `InputSystem` containing only
   `ScriptInputDriver`(s), which parse a text file and return `X_INPUT_STATE` directly
   (`:330–348`). It intentionally excludes SDL/XInput/MnK (`:117–118`). The Settings page is the
   only consumer that reads the pad itself: `settings_page.cpp:156–178` calls
   `InputSystem::HeldState(0, ...)` and `pad::Unmap` so remapping cannot move the page's own
   navigation.

### Answer

- **Not a custom “HID layer” in the game port.** The port uses the SDK's `InputDriver` abstraction.
  The actual controller backend is **SDL3 Gamepad** (`SDL_INIT_GAMEPAD`), which internally uses
  SDL's platform joystick/HIDAPI backends. **XInput** is an optional Windows-only secondary driver
  (`input_backend=xinput`). There is **no DirectInput, no raw HID, no COM** in this code.
- On Android the existing code path is already correct at the C++ level: `REX_PLATFORM_WIN32` is 0,
  so only `SDLInputDriver` + MnK + NOP are built, and `RemappedDriver` wraps SDL unchanged.
  `script_input` is also portable.

### What an Android gamepad path needs

1. **Feed SDL, or replace SDL.** SDL3's Android joystick backend is fed by the **Java**
   `org.libsdl.app.SDLControllerManager` (`onNativePadDown/Up`, `onNativeJoy`, `onNativeHat`),
   which `SDLActivity` calls from `dispatchKeyEvent`/`dispatchGenericMotionEvent`. A bare
   `android_main`/`native_app_glue` loop (what `ts4-port/src/android_main.cpp` currently is) does
   **not** feed `SDL_INIT_GAMEPAD`, so the SDL driver would see no pads. Two options:
   - **(A, recommended) Ship SDL's Android Java glue** (`org.libsdl.app.SDLActivity`,
     `SDLControllerManager`, `SDLSurface`, etc.), subclass `SDLActivity`, have it load
     `libtopspin4.so`, and use SDL's `SDL_main`/`SDL_MAIN_HANDLED` entry instead of `android_main`.
     This reuses `SDLInputDriver`, `RemappedDriver`, rumble, hot-plug, and the SDL gamecontroller
     database with essentially no input code changes.
   - **(B) Write an Android `InputDriver`** on `android_native_app_glue`: read `AInputEvent`s from
     the `ALooper`/`AInputQueue`, map `AINPUT_EVENT_TYPE_KEY` (`AKEYCODE_BUTTON_A/B/X/Y`,
     `AKEYCODE_BUTTON_L1/R1/L2/R2`, `AKEYCODE_DPAD_*`, `AKEYCODE_BUTTON_THUMBL/R`,
     `AKEYCODE_BUTTON_START/SELECT`) and `AINPUT_EVENT_TYPE_MOTION`
     (`AMOTION_EVENT_AXIS_X/Y/Z/RZ`, `AXIS_BRAKE/GAS`, `AXIS_HAT_X/Y`) into `X_INPUT_STATE`, and
     implement `EnumerateDevices/GetDeviceState/GetDeviceCapabilities/GetDeviceKeystroke/
     SetDeviceVibration`. Then wrap it with `RemappedDriver`. This is more code and needs its own
     mapping table, but avoids the SDL Java layer.
2. **Built-in Retroid pad.** Android reports it as an `InputDevice` with `SOURCE_GAMEPAD`
   (possibly `SOURCE_JOYSTICK`). SDL normally maps it from its built-in `SDL_gamepad_db.h`;
   if it is not recognised, ship `gamecontrollerdb.txt` and point `hid_mappings_file` at an
   **absolute** app path (the cvar is currently resolved relative to the process CWD, which is `/`
   on Android). If the pad reports analog triggers as buttons (common on handhelds), either add a
   mapping or handle the button→trigger conversion in a custom driver. The rest of the port only
   sees `pad_bindings.h` values (`LT/RT` 0–255, sticks ±32767), so mapping is the only place to fix
   it.
3. **Window/event loop must exist first.** `SDLInputDriver::OnWindowAvailable` needs a `WindowSDL`
   and the UI thread's `SDL_PumpEvents`. The SDK currently has **no Android window surface path**:
   `WindowSDL::CreateSurfaceImpl` (`_ref/rexglue-sdk/src/ui/window_sdl.cpp:375–425`) handles
   Win32/macOS/X11/Wayland only, and its `#else` branch includes X11 headers on Android;
   `GetNativeWindowHandle()` returns `nullptr` off Windows (`:231–239`); and
   `AndroidNativeWindowSurface` is referenced in `vulkan_presenter.cpp:802` but **not defined**
   anywhere. The port must add an Android SDL surface type (`ANativeWindow*` from
   `SDL_PROP_WINDOW_ANDROID_WINDOW_POINTER`) before any input can be delivered.
4. **Keep `input_backend=sdl`** and keep the `xinput` branch compiled out
   (`remapped_input.cpp:15–17, 64–68`). XInput does not exist on Android.
5. **Mapping file location.** Set `hid_mappings_file` to an absolute path in app assets/internal
   storage at startup (or copy it to `userdata`). This also applies to scripted runs only in the
   sense that scripts bypass SDL entirely.

## Suggested platform shim

Goal: shared sources (`src/*.cpp`, `src/native`, `src/settings`) include **no** `windows.h`, no
`HWND`/`HANDLE`, and no `std::wstring` paths. Platform code lives in `platform/` and is selected by
CMake (`platform/win32/*.cpp`, `platform/android/*.cpp`). Everything is UTF-8 `std::string` /
`std::filesystem::path`; native handles are opaque `void*` or a small struct.

Minimal set:

### `platform/paths.h`
```cpp
namespace topspin4::platform {
std::filesystem::path BaseFolder();       // Win: exe dir; Android: app internal data dir
std::filesystem::path UserDataFolder();   // BaseFolder()/userdata
std::filesystem::path PreparedDataRoot(); // Win: BaseFolder(); Android: app data / external files
std::filesystem::path CacheFolder();
std::filesystem::path TempFolder();
std::filesystem::path LogsFolder();
std::filesystem::path PathFromUtf8(std::string_view);  // UTF-8 -> path
std::string PathToUtf8(const std::filesystem::path&);
FILE* OpenFile(const std::filesystem::path&, const char* mode); // UTF-8, no _wfopen
}
```
Replaces `native/host_paths.h`, the `OpenFile` copies in `frame_dumps.cpp`, and the
`GetExecutableFolder()` assumption in `user_paths.cpp`. On Android back `BaseFolder()` with
`ANativeActivity::internalDataPath` (passed through JNI) or `SDL_GetBasePath()`, **never**
`/proc/self/exe`.

### `platform/dialog.h`
```cpp
namespace topspin4::platform {
enum class DialogKind { kInfo, kWarning, kError, kQuestion };
bool ShowDialog(DialogKind, std::string_view title, std::string_view message); // OK/Yes/No
void ShowErrorAndExit(std::string_view title, std::string_view message);
void ShowInFileManager(const std::filesystem::path&); // Win: ShellExecute; Android: ACTION_VIEW / no-op
}
```
Replaces `MessageBoxW` (`not_set_up.cpp:62–68`, `crash_report.cpp:266–269`),
`ShellExecuteW("explorer.exe")`, and `AttachConsole` (schema export can just write stdout).
Windows implementation calls `MessageBoxW`/`ShellExecuteW`; Android calls a JNI `AlertDialog` or
logs and returns false (headless).

### `platform/process.h`
```cpp
struct ProcessResult { bool started; int exit_code; std::string error; };
ProcessResult RunAndWait(const std::filesystem::path& exe,
                         const std::vector<std::string>& args,
                         const std::filesystem::path& cwd,
                         std::chrono::milliseconds timeout,
                         const std::filesystem::path& log_file);
bool StartDetached(const std::filesystem::path& exe,
                   const std::vector<std::string>& args,
                   const std::filesystem::path& cwd);
```
Replaces `CreateProcessW`/`STARTUPINFOW`/`PROCESS_INFORMATION`/job objects in
`not_set_up.cpp`, `relaunch.cpp`, `crash_report.cpp`, `topup_converter.cpp`. Windows implements
with `CreateProcessW` (argv built by an internal UTF-8→UTF-16 quoter); Android with
`posix_spawn`/`fork+exec` for bundled binaries, or returns `started=false` (“not supported”).
No shared `std::wstring` leaks.

### `platform/thread.h`
```cpp
namespace topspin4::platform {
enum class ThreadPriority { kLow, kNormal, kHigh };
void SetCurrentThreadPriority(ThreadPriority);
}
```
Replaces `SetThreadPriority`/`THREAD_PRIORITY_*` in `native/pipelines.cpp:649–656`.
Android: `setpriority(PRIO_PROCESS, 0, ...)` or no-op.

### `platform/audio.h`
```cpp
namespace topspin4::platform {
void PlaySoundFileAsync(const std::filesystem::path& wav); // no-op when muted/unsupported
}
```
Replaces `PlaySoundW` (`achievement_toast.cpp:75`). Windows: `PlaySoundW(SND_ASYNC)`; Android:
SDL audio / `AAudio` / `SoundPool` via JNI, or no-op.

### `platform/native_window.h`
```cpp
namespace topspin4::platform {
struct NativeWindow;                    // opaque
NativeWindow* GetNativeWindow();        // Win: HWND; Android: ANativeWindow*
void HideWindow(NativeWindow*);
void ShowWindow(NativeWindow*);
void SetWindowRect(NativeWindow*, int x, int y, int w, int h);
void BringToFront(NativeWindow*);
bool IsForeground();
}
```
Only needed if `relaunch`/`crash_report` UI is kept. On Android `GetNativeWindow()` may be null;
all functions no-op. Removes `HWND`, `GetWindowThreadProcessId`, `EnumWindows`,
`SetWindowPos`, `ShowWindow`, `SetForegroundWindow` from shared code.

### `platform/crash.h` (optional but cleaner than `#ifdef`s)
```cpp
namespace topspin4::platform {
void InstallCrashHandler();                            // Win: SetUnhandledExceptionFilter + dbghelp
                                                       // Android: signal handler + _Unwind_Backtrace
bool WriteCrashDump(const std::filesystem::path&);     // Win: MiniDumpWriteDump; Android: tombstone/log
std::vector<std::string> CommandLineArguments();       // Win: CommandLineToArgvW; else: argv/JNI
}
```
Allows keeping `crash_report_plan.h`/`crash_zip.h` shared and deleting the Win32 block from
`crash_report.cpp`/`fault_log.cpp`/`schema_export.cpp`.

### `platform/dynlib.h` (only for optional tools)
```cpp
namespace topspin4::platform {
void* OpenLibrary(const char* name);   // Win: LoadLibraryA; Android: dlopen
void* Symbol(void* library, const char* name); // Win: GetProcAddress; Android: dlsym
void CloseLibrary(void*);
}
```
Only `frame_dumps.cpp` (RenderDoc) needs it today; Android can compile the feature out.

### Rules for the port
- Shared code compiles with `-D_WIN32` absent and with no `windows.h`; wrap Windows-only TUs in
  CMake, not only in `#ifdef`.
- No `std::wstring`/`wchar_t` in shared code. Convert once at the Win32 boundary.
- `std::filesystem::path` is UTF-8 on Android; use `PathToUtf8` for logs and APIs.
- Prefer `platform/` functions over new `#ifdef _WIN32` blocks; the existing guards in
  `frame_dumps.cpp`, `fault_log.cpp`, `relaunch.cpp`, `host_paths.h`, and `sink.h` can stay as
  short-term fallbacks but should collapse into the shim.
- Build: drop `WIN32`, `dbghelp`, `shell32`, `winmm`, and `src/topspin4.rc` for Android; keep
  `-ffp-contract=off`; link `android`, `log`, `vulkan`, and SDL3.
