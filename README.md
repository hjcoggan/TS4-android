# Top Spin 4 — Android (static recompilation)

Run **Top Spin 4** (Xbox 360, 2K Czech, 2011) natively on Android arm64 by statically recompiling
the game's PowerPC code to C++ and drawing through Vulkan. No emulator: the recompiled code runs as
ordinary native code, with a runtime that implements the Xbox 360 kernel calls it needs.

Tested on a **Retroid Pocket Nova** (Snapdragon 8 Gen 2, Adreno 740, Android 16): 1280×960 4:3
output at a locked 60 fps in matches.

**Bring your own game.** This repository contains no game code, assets, executables or Title Update.
You dump your own disc (see [`docs/dumping.md`](docs/dumping.md)).

---

## Status

| | |
| --- | --- |
| Boots to gameplay | yes |
| Frame rate | 60 fps locked, 16.7 ms every frame (min = p99 = max) |
| Process CPU at 60 fps | ~155–162 % of one core (guest ~99 %, renderer 1–4 ms/frame) |
| Renderer | native (Vulkan, recompiled Xenos shaders) |
| Display | 1280×960 4:3, full-bleed on a 4:3 panel |
| Audio | works (XAudio2 via SDL) |
| Input | physical controller / built-in controls |

Known rough edges: occasional transient dark quads (one cause fixed, others suspected), and the
cold first launch is slow because the guest's menu system reads its archives for the first time
(~110 s cold, ~20 s warm).

---

## Credits

This is a port of a port. None of it would exist without:

- **[shaneomac1337/Top-Spin-4-Recomp](https://github.com/shaneomac1337/Top-Spin-4-Recomp)** — the
  original PC (x86-64) static recompilation of Top Spin 4. The recompiler manifests, the 46-patch
  SDK series and most of the reverse-engineering knowledge come from there. This repository is a
  fork of it, extended for Android.
- **[ReXGlue SDK](https://github.com/rexglue/rexglue-sdk)** (Tom Clay) and
  **[Xenia](https://xenia.jp)** (Ben Vanik and contributors) — the runtime, Vulkan layer and the
  recompiled-code ABI.
- **[XenonRecomp](https://github.com/hedge-dev/XenonRecomp)** and
  **[XenosRecomp](https://github.com/hedge-dev/XenosRecomp)** — the PowerPC and Xenos shader
  recompilers.
- **[codepdbh/nfsmw-android](https://github.com/codepdbh/nfsmw-android)** — the Android app layer
  and several SDK patches (see the licence note below).
- **[libadrenotools](https://github.com/bylaws/libadrenotools)** — loading a custom Vulkan driver
  (Turnip) on Adreno.
- **[SDL3](https://github.com/libsdl-org/SDL)** — windowing, input, audio.

Full attribution and licence texts: [`THIRD_PARTY.md`](THIRD_PARTY.md).

## Licence

**GPL-3.0** — see [`LICENSE`](LICENSE).

The Android app layer is derived from `nfsmw-android`, which is GPL-3.0, and the app shell plus the
native code it loads form one distributed program, so the whole work is GPL-3.0. The ReXGlue SDK
and the reference port's patches are BSD-3-Clause and remain compatible with that.

If you want a permissive build, the path is to rewrite `android/` from scratch; the native code
(`src/`) and the patch series are BSD-3-Clause-derived and could then be relicensed.

---

## What you need

**Hardware / host**

- macOS on Apple silicon (tested) or Linux x86-64 (untested but should work). Windows is not
  supported for building this port.
- An Android arm64 device with Vulkan 1.1+ and **8 GB+ RAM**. Adreno 7xx recommended.

**Toolchain**

| Tool | Version used | Notes |
| --- | --- | --- |
| Android NDK | `30.0.16248370` | other r30 versions likely fine |
| Android SDK | platform 36, build-tools 37.0.0 | `minSdk 29`, `targetSdk 36` |
| JDK | 17 | |
| CMake | ≥ 3.25 | Homebrew `cmake` is fine |
| Ninja | any recent | |
| Python | 3.9+ | helper scripts |

**Game data (yours)**

- A dump of Top Spin 4 for Xbox 360, and **Title Update 1** — see
  [`docs/dumping.md`](docs/dumping.md). The build verifies both against SHA-256 manifests, so a
  different dump will not be accepted.
- Expect ~5.4 GiB of extracted data.

Nothing game-derived is in the repository. That includes the achievement chime: the toast plays
`sounds/achievement.wav` from your own game data at runtime, or one you drop into
`files/userdata/achievement.wav`. If you want it embedded in a desktop build, put a copy at
`ts4-port/data/sounds/achievement.wav` before configuring; the build picks it up if present.

---

## Build

The build has two halves: a **host** side that turns the game's PowerPC and shader binaries into C++
and a shader pack, and an **Android** side that compiles that output into an APK. Everything
game-derived lands in `work/` and `ts4-port/generated/`, both gitignored.

### 1. Clone and place your dump

```sh
git clone https://github.com/hjcoggan/TS4-android.git
cd TS4-android

# The dump lives outside the repository, as ../dump relative to ts4-port/:
#   dump/Top Spin 4 (USA, Europe) (En,Fr,De,Es,It).iso
#   dump/tu1/                      (Title Update 1, unpacked STFS)
```

`ts4-port/` is the project root for all scripts below.

### 2. Prepare the SDK

```sh
cd ts4-port
./scripts/prepare-sdk.sh
```

This clones `rexglue-sdk` at the pinned commit (into `_ref/rexglue-sdk` if absent, otherwise
reusing it), checks out the `ts4-android` branch in `work/rexglue-sdk`, and applies, in order:

1. the reference port's 46 SDK patches (fetched from `shaneomac1337/Top-Spin-4-Recomp`),
2. this port's Android patches (`patches/rexglue-android/`),
3. this port's SDL patch (`patches/sdl3/`, applied inside the SDL submodule).

Re-running is safe and idempotent.

### 3. Prepare the game data

```sh
./scripts/prepare-gamedata.sh
```

Verifies your dump against the manifests, extracts the disc into `work/gamedata/` and Title Update 1
into `work/updatedata/`. Resumable.

### 4. Build the host recompiler and recompile the game

```sh
cmake -S ../work/rexglue-sdk -B ../work/build/rexglue-host -G Ninja \
    -DCMAKE_BUILD_TYPE=RelWithDebInfo -DCMAKE_OSX_ARCHITECTURES=arm64   # drop the last flag on Linux
cmake --build ../work/build/rexglue-host

./scripts/codegen.sh        # -> ts4-port/generated/  (~35 s)
```

`generated/` is game-derived: never commit it.

### 5. Build the shader pack

```sh
./scripts/build-shaders.sh  # -> work/shaderpack/topspin4.shaderpack  (~1 min)
```

Converts the game's Xenos shaders to SPIR-V on the host. XenosRecomp is built from source as part
of this step, so it runs on macOS and Linux alike.

### 6. Build the APK

```sh
export JAVA_HOME=$(/usr/libexec/java_home -v 17)   # or your JDK 17 path
export ANDROID_HOME=$HOME/Library/Android/sdk       # Linux: ~/Android/Sdk
export ANDROID_NDK_HOME=$ANDROID_HOME/ndk/30.0.16248370

cd android
./gradlew assembleProfile -Pts4WithRexglue=ON
```

Output: `android/app/build/outputs/apk/profile/app-profile.apk`.

Notes:

- `-Pts4WithRexglue=ON` is required; it points the native build at `../work/rexglue-sdk`
  (override with `-PrexsdkDir=/path/to/sdk`).
- `assembleDebug` also works if you want a debuggable build.
- If Gradle reports everything up-to-date after you changed SDK sources, force the native build
  first: `ninja -C android/app/.cxx/RelWithDebInfo/*/arm64-v8a` then `assembleProfile`.
- No `local.properties` is committed; `ANDROID_HOME` is enough.

### 7. Install

```sh
adb install -r android/app/build/outputs/apk/profile/app-profile.apk
adb shell am start -n com.ts4port.topspin4/.TopSpin4Activity
```

The game data is read from the app's external files directory. On first run, push the prepared data
(the project's `scripts/push-data.sh` does this):

```sh
adb push ../work/gamedata   /sdcard/Android/data/com.ts4port.topspin4/files/gamedata
adb push ../work/updatedata /sdcard/Android/data/com.ts4port.topspin4/files/updatedata
adb push ../work/shaderpack/topspin4.shaderpack \
        /sdcard/Android/data/com.ts4port.topspin4/files/shaderpack/topspin4.shaderpack
```

Files pushed with `adb` under `Android/data` keep the host uid; if the app cannot read them, run
`adb shell chmod -R 644` on them.

---

## Tests

The renderer's pure logic — clear plans, draw skipping, pipeline keys and stores, texture layout
and decoding, image quality, write watch, rect lists, guest fetch decoding, and the rest — has host
unit tests that need no device and no game data:

```sh
cd ts4-port
./scripts/test-host.sh              # build and run everything
./scripts/test-host.sh "[clear]"    # one pattern
./scripts/test-host.sh --list-tests # what is covered
```

They use Catch2 from the SDK tree and run against the same `src/native/` code the device build
compiles, so a regression in that logic fails here instead of on the handheld. Current state:
**260 test cases, 23 422 assertions, all passing**.

---

## Running

Configuration is read from files in `files/userdata/`:

- `display.conf` — resolution and refresh, e.g. `width=1280`, `height=960`, `refresh=60`.
- `cvars.conf` — one `name=value` per line, mapped to `REX_*` environment variables. Anything the
  SDK exposes can be set here without a rebuild.
- `input_script.txt` — optional scripted pad for automated testing (see `docs/`). Delete it when you
  want the physical controller back; while it exists it replaces the pad.

A **custom Vulkan driver** (Mesa Turnip) can be dropped into `files/drivers/` as an ADPKG; the
native renderer needs `shaderInt64`, which Adreno's stock driver reports as unsupported, so on
Adreno you generally need Turnip for the native renderer. Remove or rename `files/drivers/disable`
to turn this off.

---

## Repository layout

```
ts4-port/
  src/            runtime, native Vulkan renderer, guest glue
  android/        Gradle project and the app activity
  patches/
    rexglue-android/   our SDK patch series (+ THIRD_PARTY.md)
    sdl3/              our SDL submodule patch
  scripts/        prepare-sdk, prepare-gamedata, codegen, build-shaders, push-data
  codegen/        recompiler manifest and symbol lists
  data/           shader and pipeline metadata
  docs/           design notes, dumping guide, prior-art survey
  CMakeLists.txt, CMakePresets.json
```

Not in the repository, by design: `generated/` (recompiled game code), `work/` (game data, shader
pack, build trees), and `dump/` (your disc).

---

## Troubleshooting

**`prepare-gamedata.sh` rejects my dump.** The manifests pin a specific disc revision and Title
Update; a different region or an undumped TU1 fails the SHA-256 check. Check `docs/dumping.md`.

**Black screen on first launch.** The guest spends ~110 s reading its archives the first time.
Subsequent launches are ~20 s. Also make sure the screen is on: a dozing panel stalls the guest at
its first present.

**Native renderer refuses to start on Adreno.** Install Turnip (see *Running*); the stock driver
reports no `shaderInt64`.

**Everything is up-to-date after I edited SDK sources.** Gradle does not see the SDK dependency;
run `ninja` in `android/app/.cxx/RelWithDebInfo/*/arm64-v8a` first.

**Pulled the notification shade and the game died.** Fixed in the SDK series; if you see it, make
sure `prepare-sdk.sh` applied all patches.

---

## Performance notes

The port is CPU-bound, not GPU-bound: the renderer costs 1–4 ms/frame, while the recompiled guest
code saturates a core. Work that went into the current numbers, in order of effect:

- SDK scope timers disabled (they were 41 % of cycles in `clock_gettime`).
- Page-protection cache instead of parsing `/proc/self/maps` per write-watch fault (14 → 60 fps).
- arm64 virtual counter (`cntvct_el0`) for the guest tick instead of `clock_gettime`.
- Command-processor ring-buffer fix (14 → 60 fps).
- SDL: the UI thread no longer signals its own event wait, which removed a full core of spinning.

Useful measurement tools: `work/measure-cpu.py` (per-thread CPU) and `work/measure-fps.sh`
(presented frame pacing from SurfaceFlinger).

---

## Contributing

The SDK patches are the main surface. Work on `work/rexglue-sdk`, then export with
`./scripts/export-sdk-patches.sh` so `patches/rexglue-android/` stays the source of truth. SDL
changes go to `patches/sdl3/` as a plain diff applied with `git apply`.

Please do not commit anything game-derived: no `generated/`, no `work/`, no `dump/`, no extracted
executables or shader packs.
