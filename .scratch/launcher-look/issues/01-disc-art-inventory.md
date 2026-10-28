# 01: Disc art inventory

Type: research

**What to build:** A research note, `docs/research/disc-art.md`, listing the art on the Reference build's disc that the Launcher could show and how to decode it: the images inside the `nxeart` package (it starts with `PIRS`, an STFS package), the title image resource in `default.xex`, and the images in `Media/XUI/Startup.xzp` and `Common.xzp` (they start with `XUIZ`). For each image it records the source, purpose (background, box art, banner, logo, icon), size, format, and the decoding steps, and recommends which one fills each slot in the spec (background, box art, logo, title icon). It cites format sources (Xenia's STFS and XEX code, other STFS/XUI tools) and checks each recommendation by decoding the image once into `analysis/` (git-ignored). No image goes into the repo. Spec: `.scratch/launcher-look/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] The note lists every image found in the three sources, with size and format
- [x] Each of the four slots has a recommended image, decoded once into `analysis/` as evidence
- [x] The decoding steps for each source are concrete enough to implement without further research

## Comments

2026-10-28, research done: `docs/research/disc-art.md`.

- Inventory (§1): every image in `nxeart`, `default.xex`, `Startup.xzp` and `Common.xzp`, plus `Media/XUI/PressStart.xzp` and the loose `Locale/*/Media/XUI/Loading.png`, which a magic-byte census of the prepared disc folder found. The sources were read from both the `.iso` and the prepared folder, and they are byte-identical.
- Slots, each decoded once into `analysis/disc-art/` (git-ignored): background `nxeart/nxebg.jpg`; box art `nxeart/nxeslot.jpg`; logo `xzp-PressStart/Bitmaps/Logo.PNG` (in `PressStart.xzp`, not Startup or Common); title icon `xex-spa/img_8000.png` (XEX XDBF title image). `xex-spa/title-image-8000.dotnet.png` is the same image from a .NET pipeline. The icon fallback is `nxeart/thumbnail.png`. Size comparison: `icon-candidates-16-32-48-64.png`.
- Decoding (§2-§4): checked in .NET. The existing `StfsPackage` reads `nxeart` unchanged. A scratch .NET pipeline (AES, de-blocking, MonoGame `LzxDecoder`) decoded `default.xex` to an image byte-identical to `analysis/xex/default.pe` from xextool. The XUIZ layout was checked against all three packages.
- `scripts/build.ps1 -Action Check`: its Python tests pass (110), then it stops at the missing `.tools/rexglue-0.10.0` in the worktree. The change is a doc and this ticket only.
