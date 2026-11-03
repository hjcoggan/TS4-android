# Third-party code in `patches/rexglue-android/`

The SDK working copy is `_ref/rexglue-sdk` @ `f5337cd` (ReXGlue SDK, BSD-3-Clause,
Tom Clay / Xenia project) plus the reference port's 46 patches, plus the series in
this directory.

Three Android ports of ReXGlue titles were read as prior art (read-only, under
`_prior-art/`). Commits below say which part came from where; the licence of each
source is repeated here.

## ReXGlue SDK upstream — BSD-3-Clause

`f5337cd` and everything not marked otherwise. See `_ref/rexglue-sdk/LICENSE`.

## santiquiroz/gears-of-war-2-pc-port — BSD-3-Clause

Source: `_prior-art/gears-of-war-2-pc-port/sdk/patches/0007-android-arm64-port.patch`
(SDK base `923c1a5`, ported by hand onto `f5337cd` + the 46 reference patches).
Repo licence: BSD-3-Clause (its `LICENSE`), patch contributions likewise.

Taken:

| Commit | Part |
|---|---|
| `android: chain unclaimed faults and handle SIGBUS (Gears 0007)` | `src/core/exception_handler_posix.cpp`: SIGBUS treated as an access violation on Android, unclaimed SIGSEGV/SIGBUS/SIGILL forwarded to the previous handler (or `SIG_DFL` + re-raise) instead of looping. |
| `android: memfd fallback and direct ASharedMemory (Gears 0007)` | `src/core/memory_posix.cpp`: direct `ASharedMemory_create` (no `dlopen`), `memfd_create` syscall fallback, bionic 64-bit `off_t`. Supersedes this port's earlier ASharedMemory-only change. |
| `android: append-read streams and REX_EXECUTABLE_FOLDER (Gears 0007)` | `src/core/filesystem_posix.cpp`: `rewind` for `"a+"` streams (bionic starts reads at EOF), `REX_EXECUTABLE_FOLDER` override. Merged with this port's `dladdr`/`SDL_GetAndroidInternalStoragePath` paths; the patch's content-URI stubs were **not** taken (JNI implementation kept). |
| `android: load the GPU plugin by soname (nfsmw sdk)` | same change is in Gears 0007; attributed to nfsmw below. |

Not taken from 0007 (deliberately): the Gears GPU instrumentation and experiments
(`android_draw_experiments`, `android_gpu_stats`, texture-format/pipeline-cache logging),
the SGSR1 presenter upscaler, the FFmpeg `ffmpeg-android` config override, and the
`utf8.cpp`/`fpscr.h` changes. They are game/perf work, not needed for a compiling
Android runtime; see `ts4-port/docs/rexglue-android.md` for the follow-up list.

## codepdbh/nfsmw-android `sdk/` — BSD-3-Clause

Source: `_prior-art/nfsmw-android/sdk/` (ReXGlue v0.10.0, near-identical to `f5337cd`).
Only the `sdk/` tree was read; the repo's `android/` app layer is GPL-3.0 and **no code
from it is used**. SDK tree licence: BSD-3-Clause.

Taken:

| Commit | Part |
|---|---|
| `ui: reconnect the presenter across Android background/foreground (nfsmw sdk)` | `src/ui/window_sdl.{h,cpp}`, `src/ui/windowed_app_context_sdl.cpp`: detach the surface on `WILL_ENTER_BACKGROUND`, re-create it when a foreground/window event reports a new `ANativeWindow`. |
| `android: load the GPU plugin by soname (nfsmw sdk)` | `src/system/gpu_plugin_loader.cpp`: load the plugin by soname on Android so bionic searches `nativeLibraryDir`. |
| `android: accept REX_APP_FOLDER as well as REX_EXECUTABLE_FOLDER` | `src/core/filesystem_posix.cpp`: nfsmw's name for the app folder. |
| `cmake: align Android ELF segments to 16 KB` | `CMakeLists.txt`: `-Wl,-z,max-page-size=16384`. |

Cross-checked, no change needed:

- `src/core/fiber_switch.cpp` vs this port's `src/core/fiber_android.cpp`: both save
  x19-x28, x29 (fp), x30 (lr), sp and d8-d15, the full AAPCS64 callee-saved set. Ours
  builds the frame on the fiber's own stack; nfsmw keeps a `Context` in `Fiber`. Equal
  in coverage, ours kept.
- `src/core/exception_handler_posix.cpp`: nfsmw re-delivers unclaimed faults; Gears'
  version (kept) does the same and additionally treats SIGBUS as a protection fault on
  Android, so the Gears version is the superset.
- `src/core/memory_posix.cpp` `PageProtectionTable` + `MAP_FIXED_NOREPLACE` relocation
  check: **not taken yet.** It is a performance change (QueryProtect stops parsing
  `/proc/self/maps` on every MMIO fault) plus a fix for kernels that ignore
  `MAP_FIXED_NOREPLACE`; both are worth doing once the runtime is up. See the doc.

## Not used

- `_prior-art/Renut-Android` — **no license file; used with the user's permission for a
  private build** (the user has cleared this repo and Skate3-Mobile for this private,
  offline research project). It is on the same `f5337cd`, and its overlay headers
  (`src/platform/include/rex/ui/surface_android.h`, `main_android.h`), its
  `src/platform/shims/android_{chrono,core,ucontext}.h` and `query_protect.cpp` (PLT
  interposition for write-watch faults) are the natural source for the two follow-ups
  this series leaves open. No code from it is in the series yet: this port's own
  `surface_android`, `main_android` and `fiber_android` cover the same ground and were
  already building.
- `_prior-art/Skate3-Mobile`, `_prior-art/rexglue-skate3-android` — no license file /
  no code taken. GPL-3.0 is acceptable for this project, but nothing here needed it.
- `_prior-art/nfsmw-android/android/`, `NFS-CARBON-360-ANDROID-RECOMP` — GPL-3.0 app
  layers; only the nfsmw `sdk/` tree was read. If app-layer code is taken later, record
  the repo and GPL-3.0 here.
