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
  gamedata/     # disc-derived game files (prepared on the PC)
  updatedata/   # update/DLC files
  userdata/     # saves and settings
  cache/        # runtime cache (shaders, etc.)
```

Shaders ship as a pre-converted pack under `cache/` (or `gamedata/`, depending on the
loader); XenosRecomp does not run on Android.

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
