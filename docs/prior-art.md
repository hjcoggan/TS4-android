# Prior art: other Xbox 360 recomps on Android

Checked 2026-11-03. Clones are in `../_prior-art/` (untrusted: read only, never built or run).
The full reports are `../_prior-art/report-*.md`.

## What to take from where

| Repo | Base | Runs on | License | Use for |
| --- | --- | --- | --- | --- |
| `codepdbh/nfsmw-android` `sdk/` | ReXGlue v0.10.0, close to our `f5337cd` (about 165 files differ: Switch, GLES, game code) | NFS MW, and NFS Carbon at about 60 fps on Adreno 830 | SDK: BSD-3. `android/`: **GPL-3.0** | **Main SDK patch source.** Fibers, PageProtectionTable, signal fallback, plugin soname, surface lifecycle, SDL_main, REX_APP_FOLDER, adrenotools |
| `santiquiroz/gears-of-war-2-pc-port` `sdk/patches/0007` | ReXGlue `923c1a5` | Gears 2 gameplay on S25 Ultra | BSD-3 | SEH chaining + SIGBUS, memfd fallback (already ported), 0015 old-kernel mappings, 0016 Turnip no-sparse, vulkan-android-* |
| `Buku313/rexglue-skate3-android` (`android-arm64` @ `edd4344`) | ReXGlue 0.8.0 | Skate 3 on Retroid Pocket 6 (**Adreno 740**) | BSD-3 | `main_android.*`, content-URI `detachFd`, thread placement for 740, SPIR-V optimizer workaround |
| `andrewnakas/skate3-android` | app shell only | | **MIT** | Gradle/manifest, keep-alive service, storage fallback, 16 KB LOAD check |
| `Buku313/Skate3-Mobile` | app on the fork above | Skate 3 | **none** | Ideas only. Its `docs/RP6_INTEGRATION.md` has Adreno 740 settings |
| `vaduur/LibertyRecompAndroid` | forked ReXGlue 0.9.0 in-tree | GTA IV on RP5 at 720p/30 | **GPL-3.0** | Ideas only (surface lifecycle, Turnip proxy) |
| `codepdbh/NFS-CARBON-360-ANDROID-RECOMP` | nfsmw `sdk/` | | GPL-3.0 | Ideas: `set_property(TARGET rexui PROPERTY SOURCES ...)` override |
| `Innersargento/Renut-Android` | same `f5337cd`, overlay headers, Plume renderer | | **none** | Ideas only |

Rule: SDK-side BSD-3 code can be copied with attribution (`patches/rexglue-android/THIRD_PARTY.md`).
Do not copy app-layer Java/JNI from the GPL or unlicensed repos. Note that the TS4 reference repo
itself has no LICENSE file either.

## Corrections to our earlier docs

- Bionic has no `makecontext`/`swapcontext`. Fibers need a real aarch64 context switch
  (patch 0011). Check that it saves x19–x28, fp, lr, d8–d15 and sp.
- `<linux/ashmem.h>` still ships in the NDK. The device refuses `/dev/ashmem`, so use
  `ASharedMemory` / `memfd` (done).

## Not solved anywhere: 16 KB pages

On a 16 KB-page kernel, `PhysicalHeap` maps the `0xE0000000` heap at `+0x1000`. The generated
code (`REX_PHYS_HOST_OFFSET` in `resources/templates/codegen/pch_h.inja`) and `GuestPtr` still
assume 0 on Android, so memory gets corrupted. Nobody has fixed both ends. Check
`adb shell getconf PAGE_SIZE` on the Retroid Pocket Nova. If it reports 4096, assert
`host_address_offset == 0` at startup and move on.

## Device-specific

- Adreno 740 is proven by Skate 3 on the RP6. OEM blob drivers vary, so plan for an adrenotools
  or Turnip loader (nfsmw `android_gpu_driver.cpp`, BSD-2/3).
- Lifecycle: without the background/foreground surface restore, the game dies the first time the
  notification shade is pulled down.
