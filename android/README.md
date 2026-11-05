Part of the Top Spin 4 Android port. GPL-3.0: this layer is derived from nfsmw-android's
GPL-3.0 app layer, which is why the repository as a whole is GPL-3.0. See ../README.md and
../THIRD_PARTY.md.

# Android build (Gradle)

This directory is a Gradle project that builds `com.ts4port.topspin4` for
`arm64-v8a` (minSdk 29, targetSdk 36). The native build is driven by Gradle's
`externalNativeBuild` (CMake) pointing at `../../CMakeLists.txt`.

## Prerequisites

- JDK 17 (`/opt/homebrew/opt/openjdk@17`)
- Android SDK with `platforms;android-36`, `build-tools;37.0.0`, `ndk;30.0.16248370`
- CMake on `PATH` (the project requires CMake >= 3.25; homebrew `cmake` is fine)
- Gradle is **not** installed by default. Install it once, then generate the wrapper:

  ```sh
  brew install gradle
  cd ts4-port/android
  gradle wrapper --gradle-version 8.13
  ```

  After that, use `./gradlew` instead of a system Gradle.

## Build

```sh
export JAVA_HOME=/opt/homebrew/opt/openjdk@17
export ANDROID_HOME=$HOME/Library/Android/sdk
export ANDROID_NDK_HOME=$ANDROID_HOME/ndk/30.0.16248370
cd ts4-port/android
./gradlew assembleDebug
```

Useful Gradle properties (override on the command line with `-P`):

- `rexsdkDir` — path to the ReXGlue SDK working copy (default
  `../work/rexglue-sdk`, beside `ts4-port`).
- `ts4WithRexglue` — `ON`/`OFF` (default `OFF`). Enables ReXGlue SDK integration in the
  native build.

Example with the SDK enabled:

```sh
./gradlew assembleDebug -Pts4WithRexglue=ON
```

## Install

```sh
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

## Game data layout (adb push)

The app reads prepared game data from its external files directory. Push a prepared
data folder (built on the PC) like this:

```sh
PKG=com.ts4port.topspin4
BASE=/sdcard/Android/data/$PKG/files
adb shell mkdir -p $BASE
adb push gamedata   $BASE/
adb push updatedata $BASE/
adb push userdata   $BASE/
adb push cache      $BASE/
```

Expected layout on the device:

```
/sdcard/Android/data/com.ts4port.topspin4/files/
  gamedata/     # disc-derived game files (prepared on the PC); must contain default.xex
  updatedata/   # update/DLC files
  userdata/     # saves and settings
  cache/        # runtime cache (shaders, etc.)
  drivers/      # optional: drop a <name>.zip custom Vulkan driver here
```

If `gamedata/default.xex` is missing the app shows a native-free error screen with the
commands above instead of starting the engine.

### adb push ownership

`adb push` creates shell-owned directories with mode `2770`; the app cannot traverse them or
write saves inside them. From the repository root, run `scripts/push-data.sh` after preparing
the data. It pushes the game/update files and shader pack, then applies `chmod -R a+rwX` only
to `gamedata`, `updatedata`, `userdata`, `cache`, and `drivers` under the app's files directory.
This leaves the app-owned `files/` and `files/logs/` directories untouched.

The base folder is passed to native code through the environment variables
`REX_APP_FOLDER` and `REX_EXECUTABLE_FOLDER` (set in `TopSpin4Activity.onCreate` before
`super.onCreate()`), plus `XDG_DATA_HOME`/`HOME`.

Shaders ship as a pre-converted pack under `cache/` (or `gamedata/`, depending on the
loader); XenosRecomp does not run on Android.

## Optional custom Vulkan driver

Drop a driver archive at `files/drivers/<name>.zip` (i.e.
`/sdcard/Android/data/com.ts4port.topspin4/files/drivers/<name>.zip`). On launch the
activity sets `TS4_GPU_DRIVER` to that file's absolute path before the native library
loads. Native side (not part of this directory) is expected to:

- read `TS4_GPU_DRIVER`,
- load it through adrenotools (libadrenotools + liblinkernsbypass, BSD-2) as a Vulkan
  loader proxy producing `libvulkan.so`,
- verify the resulting physical device reports `VK_DRIVER_ID_MESA_TURNIP` (or fall back
  to the system driver),
- keep `TU_DEBUG=sysmem` semantics (tiled GMEM loses performance on this renderer).

`jniLibs.useLegacyPackaging = true` is set so the proxy's `.so` files are real files on
disk (required by adrenotools).

## Notes / open questions

- SDL3 is built **statically** by the ReXGlue SDK (`thirdparty/CMakeLists.txt` forces
  `SDL_SHARED=OFF`), so `TopSpin4Activity.getLibraries()` returns only `["topspin4"]`;
  SDL's `JNI_OnLoad` is linked into `libtopspin4.so`. If the SDK is ever switched to a
  shared SDL3 build, add `"SDL3"` back to the front of that list.
- The native entry point is currently `src/android_main.cpp` (an `android_main` stub).
  Before the app can run, this must be aligned with SDLActivity's expected `SDL_main`
  entry (or `getMainFunction()` overridden) and the ReXGlue runtime wired up
  (`TS4_WITH_REXGLUE=ON`).
- Gradle/AGP versions chosen: AGP 8.13.0, Gradle 8.13, compileSdk/targetSdk 36, minSdk 29,
  NDK 30.0.16248370. Adjust if a newer AGP is preferred.
- Page size on the Retroid Pocket Nova must be checked (`adb shell getconf PAGE_SIZE`);
  the `0xE0000000` guest heap offset is only consistent on 4 KB-page devices.

## Third-party

Files adapted into this directory (app layer only; SDK-side patches live under
`patches/rexglue-android/`):

| File in this tree | Source (repo + path) | License |
| --- | --- | --- |
| `app/src/main/java/com/ts4port/topspin4/TopSpin4Activity.java` | `vaduur/LibertyRecompAndroid` — `os/android/app/src/main/java/com/libertyrecomp/LibertyActivity.java` (env prep, immersive fullscreen, keep-screen-on, sustained performance, driver-zip detection) | GPL-3.0 |
| `app/src/main/java/com/ts4port/topspin4/TopSpin4Activity.java` | `andrewnakas/skate3-android` — `app/src/main/java/com/nakas/skate3/Skate3Activity.kt` (SDLActivity shell structure, controller-friendly setup) | MIT |
| `app/src/main/AndroidManifest.xml` | `vaduur/LibertyRecompAndroid` — `os/android/app/src/main/AndroidManifest.xml` (extractNativeLibs, game_mode_config, SDL_ENV metadata, largeHeap) | GPL-3.0 |
| `app/src/main/res/xml/game_mode_config.xml` | `vaduur/LibertyRecompAndroid` — `os/android/app/src/main/res/xml/game_mode_config.xml` (copied verbatim) | GPL-3.0 |
| `app/build.gradle` (packaging block) | `vaduur/LibertyRecompAndroid` — `os/android/app/build.gradle.kts`; `andrewnakas/skate3-android` — `app/build.gradle.kts` (useLegacyPackaging, keepDebugSymbols, abiFilters) | GPL-3.0 / MIT |

Ideas-only references (no code copied): `codepdbh/nfsmw-android` app layer (GPL-3.0),
`Buku313/Skate3-Mobile` (no license file; used with permission for a private build) for
storage/controller notes, and `Renut-Android` (no license file; used with permission for
a private build) for runtime/device notes. These are recorded under their own licenses
and must not be redistributed.
