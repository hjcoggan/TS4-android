# Codegen on macOS: data preparation, host recompiler, generated C++

Status: **working**. On 2026-11-03, on this Mac (macOS 26.7, Apple Silicon arm64,
Xcode 26 / AppleClang 21.0.0, CMake 4.4.4, Ninja 1.13.2), the port prepared the game data
from the disc image and TU1, built ReXGlue's recompiler for the host, and generated C++ for
all three Modules from the Reference build (disc + Title Update 1).

| Step | Command | Wall time | Result |
| --- | --- | --- | --- |
| Data prep | `ts4-port/scripts/prepare-gamedata.sh` | 14 s | `work/gamedata` 230 files, 5,737,767,977 B; `work/updatedata` 7 files, 2,425,486 B |
| Host configure | `cmake -S work/rexglue-sdk -B work/build/rexglue-host ...` | 34 s | `work/rexglue-sdk/out/mac-arm64/rexgluerd` |
| Host build | `cmake --build work/build/rexglue-host --target rexglue --parallel 8` | 88 s | 696 ninja steps, 0 errors, 374 warnings (all in SDK code) |
| Codegen | `ts4-port/scripts/codegen.sh` | 34 s (29–81 s; 81 s while another build ran) | `ts4-port/generated/` 527 files, 272,195,895 B |

Logs: `work/logs/prepare-gamedata.log`, `host-configure-1.log`, `host-build-1.log`,
`codegen.log`.

Nothing in the SDK was patched: the host build and codegen work on macOS arm64 as the
`ts4-android` working copy stands (`work/rexglue-sdk` @ `34cd226`, version
`0.10.0.71-dev.g34cd226`). See [Proposed SDK changes](#proposed-sdk-changes) for the two
paper cuts that did not need a patch.

## 1. Game data: ISO + TU1 → `work/gamedata`, `work/updatedata`

```bash
cd "/Users/heath/Top Spin 4"
ts4-port/scripts/prepare-gamedata.sh
```

This is the reference launcher's setup, minus the shader step (see
[Shaders](#shaders-what-is-not-done-here)). It reads only:

- `dump/Top Spin 4 (USA, Europe) (En,Fr,De,Es,It).iso` — XGD2, XDVDFS game partition at
  `0xFD90000`, 230 files, 5,737,767,977 bytes.
- `dump/tu1/` — Title Update 1 already unpacked from
  `TU_1A5822P_000000C000000.0000000000083` with the reference's `scripts/stfs_extract.py`.

and writes:

```
work/gamedata/        # the disc's game partition, mounted as game:
  default.xex  Loader_DLL.xex  Swing_DLL.xex  *.arc  Media/...  Locale/...
  .topspin4-prepared  # "manifest_version=54540859-60819732-v0.0.0.3"
work/updatedata/      # TU1, mounted as update:
  Boot.bin  CoreTech_Xenon.bin  Layoutall.bin  TitleUpdate.arc
  default.xexp  Loader_DLL.xexp  Swing_DLL.xexp
  .topspin4-prepared  # "manifest_version=54540859-60819732-tu1-v0.0.1.3"
```

How it matches the launcher (`launcher/src/Launcher.Core/`):

| Launcher behaviour | Here |
| --- | --- |
| `XdvdfsImage.cs`: XDVDFS parse, partition offsets `0 / 0xFD90000 / 0x2080000 / 0x18300000`, B-tree walk with left/right/`seen` guards | `ts4-port/scripts/xdvdfs_extract.py`, same algorithm and guards |
| `FileManifest.cs` + `disc-manifest.tsv` / `tu1-manifest.tsv`: set, size and SHA-256 of every file | Same manifests, copied to `ts4-port/codegen/manifests/`; `check` verifies all three |
| `DiscSetup.Check`: file list and sizes, then the Modules' hashes | `compare_sets()` then per-file SHA-256 during extraction |
| `TitleUpdateSetup.Check`: the STFS package's header, block hashes, patches' target version | Skipped: `dump/tu1/` is already unpacked and was verified when it was extracted. Every file is still checked against `tu1-manifest.tsv` |
| `PreparedFolder`: write into `<folder>.partial` through `.tmp`, hash, rename; marker `.topspin4-prepared`; resume from files that verify | Same: `<folder>.partial` + `.tmp` + `os.replace`, marker, resume by size + SHA-256 |
| `Preparation.SpareBytes`: 512 MiB head room + 256 MiB shader pack + 64 MiB pipeline store | Same 832 MiB checked with `df -Pk` before the first byte |
| `Preparation.Prepare`: check both inputs before writing anything | Same; a bad ISO or TU1 fails before `work/` is touched |

Re-running is safe and cheap: a complete folder is re-hashed against its manifest (a few
seconds) and kept; an interrupted one resumes into `.partial`.

The manifests and their hashes are the reference's, so the data is the Reference build
(ADR-0002): every Module v0.0.0.3 on disc, v0.0.1.3 after TU1. The three Modules were
checked independently against `scripts/build.ps1`'s gate:

```
2D72E84F…4CE28  default.xex      8F9ED530…FD092  Loader_DLL.xex
DFA71292…AC44   Swing_DLL.xex    1BA6119F…D3FC7  default.xexp
9666DF4D…0F657  Loader_DLL.xexp  D5837EF9…726ED  Swing_DLL.xexp
```

`xdvdfs_extract.py` is also usable on its own (standard library only, `python3 -I`):

```bash
python3 -I ts4-port/scripts/xdvdfs_extract.py list    "<iso>"
python3 -I ts4-port/scripts/xdvdfs_extract.py extract "<iso>" <dir> [<manifest.tsv>]
python3 -I ts4-port/scripts/xdvdfs_extract.py check   <dir> <manifest.tsv>
```

## 2. Host recompiler build

The reference downloads a prebuilt Windows `rexglue.exe` (`bootstrap.ps1`,
`rexglue-sdk-0.10.0-win-amd64.zip`). On macOS the recompiler is built from the same source
tree the Android runtime uses, so codegen and runtime cannot drift apart.

```bash
cd "/Users/heath/Top Spin 4"
cmake -S work/rexglue-sdk -B work/build/rexglue-host -G Ninja \
    -DCMAKE_BUILD_TYPE=RelWithDebInfo \
    -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DCMAKE_C_COMPILER=/usr/bin/clang -DCMAKE_CXX_COMPILER=/usr/bin/clang++ \
    -DREXGLUE_USE_VULKAN=ON -DREXGLUE_USE_D3D12=OFF \
    -DREXGLUE_ENABLE_TRACY=OFF -DREXGLUE_BUILD_TESTS=OFF
cmake --build work/build/rexglue-host --target rexglue --parallel 8
mkdir -p work/build/rexglue-host/bin
cp work/rexglue-sdk/out/mac-arm64/rexgluerd work/build/rexglue-host/bin/rexglue
```

Notes:

- `CMAKE_OSX_ARCHITECTURES=arm64` is required. The SDK's `mac-*` presets hardcode
  `x86_64` (and `-march=x86-64-v3`); without the override CMake would build an Intel binary
  and need Rosetta to run it.
- RelWithDebInfo gives the binary the SDK's `rd` postfix: `rexgluerd`. `codegen.sh` accepts
  either name and prefers the copy at `work/build/rexglue-host/bin/rexglue`.
- The SDK hardcodes `CMAKE_RUNTIME_OUTPUT_DIRECTORY` to `${REXGLUE_ROOT}/out/${REX_PLATFORM}`,
  so the binary lands in `work/rexglue-sdk/out/mac-arm64/` (a build artifact, like
  `out/android-arm64/`), not in the `-B` directory. The build tree itself is
  `work/build/rexglue-host` (637 MiB).
- There is no codegen-only target: the top-level project configures the whole SDK (Vulkan
  headers, loader, MoltenVK, FFmpeg, glslang, …) even though the recompiler uses none of
  it. `--target rexglue` builds the codegen library and CLI plus their dependencies.
- The build needs no Vulkan SDK, no MoltenVK install, no Homebrew packages beyond cmake and
  ninja: everything is vendored in `thirdparty/`.
- `rexglue --version` → `0.10.0.71-dev.g34cd226`. The reference's prebuilt tool is the
  0.10.0 release; see [Differences from Windows](#differences-from-windows).

## 3. Codegen

```bash
cd "/Users/heath/Top Spin 4"
ts4-port/scripts/codegen.sh
```

It is the reference's `scripts/build.ps1 -Action Codegen`, line for line:

1. **Gate the six inputs by SHA-256** (the same uppercase constants as `build.ps1`): the
   three Modules from `work/gamedata`, the three `.xexp` patches from `work/updatedata`.
   The reference copies them to `assets/` and hashes the copy; this script stages them in
   `work/codegen/assets/`, outside the repo, so no game file ever enters `ts4-port/`.
2. **Run the recompiler**:

   ```bash
   cd work/codegen
   work/build/rexglue-host/bin/rexglue codegen topspin4_manifest.toml --ignore-stamp
   ```

   The manifest is `ts4-port/codegen/topspin4_manifest.toml`, copied per run to
   `work/codegen/topspin4_manifest.toml` because codegen stamps `sdk_version` back into
   the manifest file. `work/codegen/generated` is a symlink to `ts4-port/generated`, so
   every output (including `generated/rexglue.cmake`, which codegen writes next to the
   manifest) lands in the repo's gitignored folder. The writer only replaces regular files
   inside the output directory, never the directory itself, so the symlink is stable.
3. **Check the output**: `generated/rexglue.cmake`, `generated/{boot,loader,swing}/sources.cmake`,
   `git check-ignore generated`, and that `generated/` is not tracked.

`rexglue init` is **not** used, here or in the reference: it bootstraps a new project
(manifest + CMake glue), while Top Spin 4's manifest is hand-maintained (it seeds the
functions the automatic pass misses) and codegen writes `generated/rexglue.cmake` itself.

### Output

```
ts4-port/generated/
  rexglue.cmake                       5,957 B   SDK glue (generated by codegen, not init)
  boot/     16 files   1,872,393 B     445 functions    default.xex   (entrypoint)
  loader/  172 files  88,886,651 B  17,086 functions    Loader_DLL.xex
  swing/   338 files 181,430,894 B  46,930 functions    Swing_DLL.xex
  total    527 files 272,195,895 B  64,461 functions
```

Per module: `sources.cmake`, `topspin4_pch.h`, `topspin4_funcs.h` + numbered
`topspin4_funcs.N.h`, `topspin4_recomp.N.cpp`, `topspin4_init.{h,cpp}`,
`topspin4_register.cpp`, `codegen.stamp`, `codegen.partition.json`; `boot/` also gets
`dll_targets.cmake` (Loader/Swing shared-library targets) and `module_registry.cpp`.

The TU1 patches are applied by the recompiler itself, from the `.xexp` files beside the
XEXs in `assets/` (log: `XEX patch applied successfully: base version: 0.0.0.3, new
version: 0.0.1.3` for each Module). Nothing is patched by this port's scripts.

Codegen is deterministic for the same inputs and SDK: a second run reports
`0 written, 521 unchanged` (521 of the 527 files are codegen outputs; the rest are stamps,
JSON and CMake glue) and leaves every file byte-identical. `generated/` is
gitignored and must never be committed (ADR-0006).

### Messages codegen prints

Non-fatal, and expected to be the same on Windows for this SDK revision; they are warnings,
not errors, and all 527 outputs were written:

- `Unresolved conditional branch to 0x8317F634 from 0x8317F658` (Loader) — one conditional
  branch whose target the analysis did not place inside a function.
- `Unexpected float16_4 pack instruction at 882B5E..–882B6A..` (Swing) — 20 `vpk*` VMX128
  forms the instruction builder does not model. Same class as the reference's known VMX128
  gaps (`docs/research/feasibility.md`).
- `BaseHeap::Release failed because address is not a region start` (twice, at exit) — the
  host arena teardown after codegen finished. Exit code is still 0; it is macOS-specific
  (the same teardown code runs on Windows without it).

If a future SDK bump makes one of these an error, follow the reference's
`docs/agents/codegen-debugging.md`.

### Wiring the output into the app build (for the CMake owner)

`ts4-port/CMakeLists.txt` already includes `generated/boot/sources.cmake`. The reference's
`cmake/rexglue.cmake` additionally includes `generated/boot/dll_targets.cmake`, which
declares the `topspin4_Loader_DLL` and `topspin4_Swing_DLL` shared-library targets that
`default.xex` loads through `XexLoadImage`:

```cmake
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/generated/boot/dll_targets.cmake")
    include(generated/boot/dll_targets.cmake)
endif()
```

`ts4-port/cmake/rexglue.cmake` does not do that yet (not this task's file).

## Shaders: what is not done here

The launcher's third setup step is `ShaderPreparation.cs`: read every shader container from
the game files (`ArchiveShaders`), add the Modules' own shaders (headless Module mode), run
**XenosRecomp** to convert them to SPIR-V/HLSL, and write `cache/topspin4.shaderpack`
(plus `cache/shader-topup/`). XenosRecomp is a Windows x64 tool built by
`scripts/build-xenosrecomp.ps1`; it does not run on macOS (HANDOFF decision 2: ship the
pre-converted pack). `prepare-gamedata.sh` therefore stops after the data folders and does
not create `work/cache/`. The runtime takes the pack path from the app's `cache_root`;
`work/cache` is where a local pack would go if one is ever produced or copied from the
Windows build.

## Differences from Windows

| | Reference (Windows) | This Mac |
| --- | --- | --- |
| Recompiler binary | prebuilt `rexglue.exe` 0.10.0 from the GitHub release | built from `work/rexglue-sdk` @ `34cd226`, `0.10.0.71-dev.g34cd226` |
| Toolchain | MSVC + LLVM 21 for the game build | AppleClang 21, CMake 4.4.4, Ninja 1.13.2 (no LLVM install needed for codegen) |
| Data extraction | launcher's `XdvdfsImage.cs` / `StfsPackage.cs`, Windows file APIs | `xdvdfs_extract.py`; TU1 is pre-unpacked by the reference's `stfs_extract.py` |
| STFS package check | header + hash tables + patch target versions, then file hashes | file hashes only (the package was verified when it was unpacked) |
| Shader pack | XenosRecomp → `cache/topspin4.shaderpack` | not built (Windows-only tool); pre-converted pack to be shipped |
| Output path | `generated/` beside the manifest, from the project root | `work/codegen/generated` is a symlink to `ts4-port/generated` |
| Manifest stamp | codegen rewrites `sdk_version` in the tracked manifest (same value) | rewritten only in the staged copy, so the tracked manifest stays byte-identical |
| File system | case-insensitive but case-preserving | case-insensitive by default on APFS; the extractor compares paths case-insensitively like the launcher, and writes the image's own names |
| Codegen messages | no `BaseHeap::Release` lines | two at exit, harmless |
| Game data in the tree | `assets/` in the project root | never: staged in `work/codegen/assets/` |

The 0.10.0.71-dev recompiler is the same source the Android runtime is built from (plus the
reference's 46 patches and our Android series), so generated code and runtime agree. The
reference's generated tree was produced with the 0.10.0 release; if a codegen bug is ever
suspected, compare against a Windows run with the pinned release before blaming the port.

## Proposed SDK changes

Neither is needed to build; both are quality-of-life, and the SDK was **not** edited.

1. **macOS presets assume Intel.** `CMakePresets.json`'s `mac-base` sets
   `CMAKE_OSX_ARCHITECTURES=x86_64` and `-march=x86-64-v3`, so `mac-arm64` inherits
   `-march=armv8-a` but still has to override the architecture. On an Apple Silicon host the
   `mac-arm64` preset should be the default choice, or `mac-base` should leave
   `CMAKE_OSX_ARCHITECTURES` unset so the host architecture is used.
2. **Host builds write into the source tree.** `CMakeLists.txt` sets
   `CMAKE_RUNTIME_OUTPUT_DIRECTORY`/`LIBRARY`/`ARCHIVE` to
   `${REXGLUE_ROOT}/out/${REX_PLATFORM}` unconditionally, overriding `-D` and ignoring the
   `-B` build directory. Proposed: only set them when the variable is not already defined
   (`if(NOT CMAKE_RUNTIME_OUTPUT_DIRECTORY)`), or make the root a cache variable. Until
   then, `codegen.sh` copies `out/mac-arm64/rexgluerd` to
   `work/build/rexglue-host/bin/rexglue`.

## Reproduce from scratch

```bash
export JAVA_HOME=/opt/homebrew/opt/openjdk@17        # Android builds only
cd "/Users/heath/Top Spin 4"

# 1. Game data (~5.4 GiB, needs ~6.2 GiB free)
ts4-port/scripts/prepare-gamedata.sh

# 2. Host recompiler (~2 min)
cmake -S work/rexglue-sdk -B work/build/rexglue-host -G Ninja \
    -DCMAKE_BUILD_TYPE=RelWithDebInfo -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DCMAKE_C_COMPILER=/usr/bin/clang -DCMAKE_CXX_COMPILER=/usr/bin/clang++ \
    -DREXGLUE_USE_VULKAN=ON -DREXGLUE_USE_D3D12=OFF \
    -DREXGLUE_ENABLE_TRACY=OFF -DREXGLUE_BUILD_TESTS=OFF
cmake --build work/build/rexglue-host --target rexglue --parallel 8
mkdir -p work/build/rexglue-host/bin
cp work/rexglue-sdk/out/mac-arm64/rexgluerd work/build/rexglue-host/bin/rexglue

# 3. Codegen (~35 s)
ts4-port/scripts/codegen.sh

# Verify
git -C ts4-port check-ignore generated          # prints "generated"
find ts4-port/generated -type f | wc -l         # 527
```

## Provenance

Copied from `_ref/Top-Spin-4-Recomp` @ `b9285dd` (reference source, no game data):

- `codegen/topspin4_manifest.toml` — the hand-maintained manifest, verbatim.
- `codegen/manifests/disc-manifest.tsv`, `codegen/manifests/tu1-manifest.tsv` — the
  launcher's embedded manifests (`launcher/`), paths/sizes/SHA-256 only.
- `codegen/symbols/d3d-functions.csv`, `codegen/symbols/tu1-symbols.csv` — the reference's
  symbol lists, for the app layer and scripts; codegen does not read them.

The reference's `launcher/` code and scripts were read, not copied into the port.
