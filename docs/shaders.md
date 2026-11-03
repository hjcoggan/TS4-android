# The shader pack: what converts the shaders, and the pack the app ships

Status: **working on this Mac**. On 2026-11-03, `ts4-port/scripts/build-shaders.sh` built
`work/shaderpack/topspin4.shaderpack` from `work/gamedata`: **1099 shaders**, 2,142,156 bytes
(2.0 MiB), converter id `ed3da5da8c4ac2ba`. The pack was checked with the same rules
`src/native/shader_pack.cpp` applies at startup, every SPIR-V module passes
`spirv-val --target-env vulkan1.3`, and the bytes are **identical** to what the reference's own
`scripts/shader_pack.py` writes from the same conversion.

XenosRecomp does not run on the device (HANDOFF decision 2): this script is the only place
shaders are converted, and the device only ever reads the finished pack.

## 1. What produces the pack

| | |
| --- | --- |
| Input | The prepared game data (`work/gamedata`, ADR-0002): every `.arc` archive, plus the read-only data of `Loader_DLL.xex` and `Swing_DLL.xex`. Not the XEX's code, not `updatedata`'s `.xexp` patches. |
| Unit | A **Xenos shader container**: big-endian magic `0x102A1100` (pixel) or `0x102A1101` (vertex), then the header size and microcode size at 4 and 8, which add up to the container's length (`ShaderContainer.IsContainer`, `module_shaders.h`). |
| Tool | **XenosRecomp** (`zolaware/reblue-XenosRecomp` @ `339af41` + the reference's 12 patches) → HLSL → **DXC** (`dxc-macos`, prebuilt in the `dxc-bin` submodule) → SPIR-V 1.0, then **smol-v** encoding. |
| Output | `work/shaderpack/topspin4.shaderpack` — magic `WWSP`, format version 1 (`src/native/shader_pack.h`). |
| On the device | `<cache_root>/topspin4.shaderpack`. `cache_root` defaults to `<REX_APP_FOLDER>/cache`, so `/sdcard/Android/data/<pkg>/files/cache/topspin4.shaderpack`, beside `pipeline-keys.txt`. |

The pack holds, per shader, the container's **XXH3-64** hash, its smol-v stream's offset and size,
and its spec-constants mask; then one entry per shader with its container's **FNV-1a-64** hash (the
name `--ts4_shader_dump_dir` gives it) and a 32-bit mask of the texture fetch slots its body reads;
then all the smol-v streams in **one zstd frame**. Both tables are sorted by hash, which
`shader_pack.cpp` checks.

### Why the shaders come from two places

The reference's setup step (`launcher/src/Launcher.Core/ShaderPreparation.cs`) reads shaders in
three ways, and this script reproduces the first two:

1. **Archives** — `ArchiveShaders.Read`: walk every `.arc` (0x24-byte header, 12-byte
   `{name hash, offset, length}` rows, `PAM-LZX`/`PAM-NIL` at 0x1C), read each entry, and keep the
   ones whose bytes are a container. Entries are matched on the magic, because the archives' name
   tables are incomplete. Here: **1007 unique containers** from 145 archives.
2. **Modules** — the game's headless Module mode (`--ts4_prepare_modules`, `src/module_shaders.cpp`)
   scans the read-only data sections of the Loader and Swing Modules for containers; ~91 of the
   game's shaders are in no archive. The script does the same on the decompressed XEX images:
   **92 containers** (85 Loader, 7 Swing). The reference reports 91 for the Title Update 1
   modules; the difference is one container that TU1 changes (see [Gaps](#7-gaps-and-caveats)).
3. **Top-ups** — shaders the game met at runtime that the pack lacked. There are none here, and on
   Android there can never be any: the live converter needs XenosRecomp.

### Reading the game's data: `lzxtool`

The archives' entries are LZX-compressed in frames (`FF u16 uncompressed u16 compressed`, or a
`u16 compressed` whole 32 KiB frame), and the XEX images are AES-128-CBC encrypted and
LZX-compressed in blocks. Both need a real LZX decoder, so `build-shaders.sh` builds a small
extractor (`work/shaderpack/build/lzxtool`) from source embedded in the script, over:

- **libmspack's `lzxd.c`** from the SDK (`work/rexglue-sdk/thirdparty/libmspack`) — the decoder
  ReXGlue's own XEX loader uses, including the x86 `E8` transform;
- **CommonCrypto** for the XEX title key and image (`kRetailKey` from
  `work/rexglue-sdk/src/system/xex_module.cpp`, then AES-128-CBC with a zero IV);
- **libzstd** from the XenosRecomp build tree, for the pack check.

It is host tooling only: it never ships, and the app has no copy of it.

## 2. Pipeline

```
work/gamedata/*.arc ──lzxtool arc──┐
                                   ├─> work/shaderpack/containers/  (ps_/vs_<fnv>.bin, 1099)
Loader_DLL.xex ─lzxtool xex─> .img ─┤        │
Swing_DLL.xex  ─lzxtool xex─> .img ─┘        │ XenosRecomp
                                             ├──────────────> work/shaderpack/converted/
                                             │                shader_cache.cpp + hlsl/*.hlsl
                                             │                        │
                                   shader_pack.py (+ fetch slots, converter id)
                                             │                        │
                                             v                        v
                              work/shaderpack/topspin4.shaderpack  (WWSP v1, zstd)
                                             │
                                    lzxtool packcheck  (= shader_pack.cpp)
```

### Commands

```bash
cd "/Users/heath/Top Spin 4"

# Everything, about 90 s: extract, convert, pack, check.
ts4-port/scripts/build-shaders.sh

# Options: --clean (drop containers, conversion, pack first), --no-convert (repack only).
```

What the script runs, step by step:

```bash
# 1. the extractor (once per run; ~2 s)
clang -O2 -o work/shaderpack/build/lzxtool <embedded lzxtool.c> \
    work/rexglue-sdk/thirdparty/libmspack/libmspack/mspack/lzxd.c \
    -I.../mspack -I... -I<zstd> work/build/xenosrecomp/thirdparty/zstd/build/cmake/lib/libzstd.a

# 2. containers
work/shaderpack/build/lzxtool arc work/gamedata/Amsterdam.arc work/shaderpack/containers
work/shaderpack/build/lzxtool xex work/gamedata/Loader_DLL.xex work/shaderpack/xex/Loader_DLL.img
python3 -I work/shaderpack/build/module_containers.py work/shaderpack/xex/Loader_DLL.img \
    work/shaderpack/containers

# 3. convert (the converter must exist; the script builds it if it does not)
export DYLD_LIBRARY_PATH="work/xenosrecomp/thirdparty/dxc-bin/lib/arm64"
work/build/xenosrecomp/XenosRecomp/XenosRecomp \
    work/shaderpack/containers \
    work/shaderpack/converted/shader_cache.cpp \
    work/xenosrecomp/XenosRecomp/shader_common.h \
    work/shaderpack/converted/hlsl

# 4. the pack, then the check
python3 -I work/shaderpack/build/shader_pack.py \
    work/shaderpack/converted/shader_cache.cpp work/shaderpack/converted/hlsl \
    ed3da5da8c4ac2ba work/shaderpack/topspin4.shaderpack
work/shaderpack/build/lzxtool packcheck work/shaderpack/topspin4.shaderpack ed3da5da8c4ac2ba
```

## 3. Building XenosRecomp on macOS arm64

It builds as it is, with no Homebrew package and no Windows SDK. What was done once (the script
repeats the build if the binary is missing):

```bash
cd "/Users/heath/Top Spin 4"
git clone https://github.com/zolaware/reblue-XenosRecomp work/xenosrecomp
git -C work/xenosrecomp checkout 339af41
git -C work/xenosrecomp submodule update --init --recursive     # fmt, xxHash, zstd, smol-v, dxc-bin

# The reference's 12 patches, in name order (identical to ts4-port/data/xenosrecomp/*.patch).
for p in _ref/Top-Spin-4-Recomp/patches/xenosrecomp/*.patch; do
    git -C work/xenosrecomp apply --index "$p"
done

/opt/homebrew/bin/cmake -S work/xenosrecomp -B work/build/xenosrecomp -G Ninja \
    -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DCMAKE_C_COMPILER=/usr/bin/clang -DCMAKE_CXX_COMPILER=/usr/bin/clang++ \
    -DCMAKE_CXX_FLAGS=-DSVR2011_RECOMP -DXENOS_RECOMP_DXIL=OFF
/opt/homebrew/bin/cmake --build work/build/xenosrecomp --parallel 8
# -> work/build/xenosrecomp/XenosRecomp/XenosRecomp  (arm64, 120 ninja steps, ~40 s)
```

Notes:

- **DXC is not a problem.** `thirdparty/dxc-bin` ships a universal Mach-O
  `libdxcompiler.dylib` (55 MB) and `dxc-macos` for both x86_64 and arm64; the submodule's
  `CMakeLists.txt` already has an `APPLE` + `arm64` branch. The binary links
  `@rpath/libdxcompiler.dylib`, so it needs
  `DYLD_LIBRARY_PATH=work/xenosrecomp/thirdparty/dxc-bin/lib/arm64` to run — the script exports it.
  Nothing was installed with brew.
- The 12 patches apply cleanly in order (they are a series; `git apply --check` on one alone can
  fail because a later patch changes lines an earlier one added).
- `-DSVR2011_RECOMP` is the WWE All Stars / Top Spin 4 mode: 32 sampler slots, `COLOR0-7`
  interpolators, the Xenos boolean file, and the PWL-gamma output spec constant. It is a compile
  definition *and* is passed to DXC as `-DSVR2011_RECOMP`, so `shader_common.h`'s layout matches
  what the recompiler emits.
- `-DXENOS_RECOMP_DXIL=OFF` matches the reference's `scripts/build-xenosrecomp.ps1`; there is no
  DXIL in the pack and no `dxil.dll` dependency. The `rc.exe`/`.res` step of that script is
  Windows-only (it only exists to keep Defender quiet) and is not needed here.
- The converter's patch series must stay in step with the app build: the app's
  `CMakeLists.txt` hashes `ts4-port/data/xenosrecomp/*.patch` into
  `TOPSPIN4_EXPECTED_SHADER_CONVERTER_ID`, and a pack converted with another series is treated as
  stale (`kStaleShaderPack` → the Classic renderer).

## 4. Results

| | |
| --- | --- |
| Archives read | 145 `.arc` files, 4.8 GiB, 17 s, 0 entries failed to decode |
| Containers | 1099 = 1007 from archives + 92 from the Modules (`work/shaderpack/containers`, 4.3 MB) |
| Conversion | 1099 shaders, **0 failures** (no screening needed), 1099 HLSL files (57 MB) |
| SPIR-V | 10,328,634 bytes decompressed, 2,098,164 bytes zstd (5:1) |
| Pack | `work/shaderpack/topspin4.shaderpack`, 2,142,156 bytes, 1099 shader entries, 1099 fetch-slot entries |
| Converter id | `ed3da5da8c4ac2ba` (the SHA-256 of the patch series' hex, first 16 hex digits, as `CMakeLists.txt` computes it) |
| Whole run | 1 min 31 s on this Mac (first run, including the extractor build); 1 min 0 s for `--clean` |
| Pack SHA-256 | `546a95134194623af78250c9fc7e296b036262e5d4486a9339d2cefd6db68914` — identical after a `--clean` rebuild, so the pipeline is deterministic |

Checks that were run:

1. `lzxtool packcheck <pack> <id>` — magic, version, the header's sizes against the file's, the
   converter id, both tables sorted, every shader inside the SPIR-V, the zstd frame, and that every
   shader's stream starts with smol-v's magic (`SMOL`). Result: `1099 shaders, 1099 fetch slots,
   10328634 bytes SPIR-V (zstd 2098164), converter ed3da5da8c4ac2ba -- ok`.
2. The fetch-slot table's FNV hashes equal the container file names exactly (1099 of 1099), so
   every draw's container finds its slots.
3. `spirv-val --target-env vulkan1.3` on all 1099 decoded modules: 1099 valid, 0 invalid.
4. **Byte-for-byte the reference's writer**: running the reference's own
   `_ref/.../scripts/shader_pack.py` on the same `shader_cache.cpp` and `hlsl` folder produces a
   file `cmp` calls identical, so the format is the reference's, not a lookalike.
   (`work/shaderpack/refcheck/`.)

Logs: `work/logs/build-shaders-1.log` (first run), `build-shaders-2.log` (`--clean`),
`work/logs/shader-arcs.log` (per archive),
`work/logs/shader-convert.log`, `work/logs/xenosrecomp-{configure,build}.log`.

## 5. What the SPIR-V needs from the device

Aggregated over all 1099 shaders (`spirv-dis` + a word-level scan of the decoded modules;
`work/build/xenosrecomp/smolvdump` unpacks the pack):

| Capability / extension | Shaders | What it is |
| --- | --- | --- |
| `Shader` | 1099 | |
| `PhysicalStorageBufferAddresses` + `SPV_KHR_physical_storage_buffer`, memory model `PhysicalStorageBuffer64` | 1099 | Xenos constants, samplers and textures are reached through `vk::RawBufferLoad<uint>(g_PushConstants.SharedConstants + n)`: buffer device addresses in the push constant. |
| `RuntimeDescriptorArray` + `SPV_EXT_descriptor_indexing` | 1099 | Sampler slot selection at runtime, from the shared constants. |
| `Int64` | 1099 | 64-bit integer ops in the shader bodies (Xenos constant indices and bit fiddling). |
| `ImageQuery` | 700 | Texture size/level queries. |
| `ImageMipmap` | 1099 | Mipmapped sampling. |
| `OriginUpperLeft` execution mode | 663 | The fragment shaders. |

**Not used at all:** 16-bit storage or arithmetic (`StorageBuffer16BitAccess`, `Float16`), 8-bit
storage, subgroup/`GroupNonUniform*` operations, `DemoteToHelperInvocation`, atomics on float or
int64 images, variable pointers, geometry/tessellation, `SAMPLE_RATE_SHADING`. So there is no
subgroup or float16 work to do for Adreno, and nothing about the shaders is tied to a desktop GPU.

### Does anything need Android/Adreno-specific changes? No.

- Everything the shaders ask for is **core or widely-supported Vulkan 1.3** on the Adreno 740:
  `VkPhysicalDeviceVulkan12Features.bufferDeviceAddress`,
  `descriptorIndexing`/`runtimeDescriptorArray`/`shaderSampledImageArrayNonUniformIndexing`, and
  `shaderInt64`. The 740 (Snapdragon 8 Gen 2) reports all of them; the SDK's device selection
  already enables Vulkan 1.2/1.3 features, so the pipeline layout is unchanged from the Windows
  build.
- The two extensions are *SPIR-V* extensions, not device extensions: `SPV_KHR_physical_storage_buffer`
  compiles to `OpConvertUToPtr`/`OpLoad` on a physical pointer, and
  `SPV_EXT_descriptor_indexing` to non-uniform descriptor access. Adreno's compiler handles both;
  the physical-storage path is the one to watch on any GPU (it is a driver-visible feature), and
  the fallback if it ever misbehaves is the Classic renderer, not a shader rewrite.
- The shaders are **SPIR-V 1.0**, produced by DXC with `-spirv -fvk-use-dx-layout -HV 2021
  -all-resources-bound`, then smol-v encoded. smol-v is lossless, so the module the driver sees is
  the same on Android as on Windows.
- Nothing in the pipeline depends on the host's endianness, word size or driver: the pack is a
  byte-exact artifact, so the pack built here is the pack the device uses.
- Two things are *device* work, not shader work, and are owned elsewhere:
  `pipeline-keys.txt` (the shipped pipeline key list, `data/pipeline-keys.txt`) and the
  `native-pipelines` cache, which the app prebuilds on first run; and the sRGB/PWL gamma choice,
  which is a **spec constant** (`SPEC_CONSTANT_PWL_GAMMA_OUTPUT`, bit 3) set per target format at
  pipeline creation, not something baked into the SPIR-V.

## 6. Reproducing from scratch

```bash
cd "/Users/heath/Top Spin 4"
# 1. data (once): ts4-port/scripts/prepare-gamedata.sh
# 2. converter (once, section 3 above); the script builds it if work/build/xenosrecomp is empty
# 3. the pack
ts4-port/scripts/build-shaders.sh --clean
```

The script is idempotent: containers already extracted are kept (they are named by hash), a rerun
of `--no-convert` repacks from the existing conversion, and `--clean` starts over. The conversion
itself is deterministic for the same containers and patch series.

## 7. Gaps and caveats

- **One container differs from the reference's count** (92 module containers here, 91 on Windows
  after Title Update 1). The `.xexp` deltas are not applied to the Modules before scanning, so a
  container that TU1 rewrites is shipped in its disc form; if the game asks for the TU1 variant the
  container hash differs and that one draw has no shader. Applying `lzxdelta_apply_patch` to the
  module image would close this; it needs the SDK's delta-patch path, not more work here.
- **Shaders XenosRecomp cannot convert** are set aside in `work/shaderpack/skipped/` and left out
  of the pack (the Launcher's screening behaviour). This run had none; a future one would report
  the count. On Android a skipped shader is a shader the game can never get, because the live
  top-up cannot run.
- **A new patch series or new game data invalidates the pack.** The converter id changes with the
  patches; with different data the container hashes change. Either way the app logs a stale pack
  and runs on Classic — rebuild with this script.
- The pack is **game-derived**: `work/shaderpack/` is outside `ts4-port/` and must never be
  committed or distributed (ADR-0006).

## 8. Provenance

- `_ref/Top-Spin-4-Recomp` @ `b9285dd`: `scripts/convert-shaders.ps1`, `scripts/shader_pack.py`,
  `scripts/shader_fetch_slots.py` (reimplemented in the script, byte-identical output),
  `launcher/src/Launcher.Core/{ShaderPreparation,ArchiveShaders,GameArchive,ShaderContainer,
  ShaderPackFile,XenosRecompConverter,GameModuleShaders}.cs`, `src/module_shaders.{h,cpp}`,
  `src/native/shader_pack.cpp`, `scripts/build-xenosrecomp.ps1`.
- `work/rexglue-sdk` @ `34cd226`: `thirdparty/libmspack` (LZX decoder, LGPL 2.1) and
  `src/system/xex_module.cpp` (the XEX key and block layout).
- `work/xenosrecomp` @ `339af41` + `ts4-port/data/xenosrecomp/*.patch`: MIT, plus fmt, xxHash,
  zstd and smol-v (public domain); licences in that tree and in the reference's release folder.
- `work/build/xenosrecomp/lzxtool/lzxtool.c` and `smolvdump.cpp` are throwaway host tools; the
  extractor's source is embedded in `ts4-port/scripts/build-shaders.sh` so the pack is
  reproducible from the repo.
