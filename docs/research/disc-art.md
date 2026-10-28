# Disc art: what the Reference build's disc holds and how to decode it

Research date: 2026-10-28, for ticket `.scratch/launcher-look/issues/01-disc-art-inventory.md`
and the spec `.scratch/launcher-look/spec.md`. It lists every image in the sources the spec
names, plus one it doesn't (`Media/XUI/PressStart.xzp`). For each it gives the decoding steps
and recommends an image for each of the four Launcher slots. Every recommendation was decoded
once into `analysis/disc-art/` (git-ignored). No image or game byte is in this note or the
repository (ADR-0006).

Inputs: the Reference build's disc, `.iso/Top Spin 4 [RF]/Top Spin 4 [RF].iso`, and the
prepared disc folder `%LOCALAPPDATA%\TopSpin4Recomp\gamedata`. The five source files were
read from both. They are byte-identical (SHA-256), and they sit at the same relative paths.

**[inferred]** marks my own reasoning. Every other claim comes from a decode run, a source
file cited by path, or a URL under Sources.

## Summary and recommendation

| Slot | Image | Source | Size, format | Decoding needed |
|---|---|---|---|---|
| Background | `nxebg.jpg` | `nxeart` (STFS) | 1920x1080 JPEG, no alpha | STFS file read (existing `StfsPackage`) |
| Box art | `nxeslot.jpg` | `nxeart` (STFS) | 420x320 JPEG, no alpha, logo baked in | STFS file read (existing `StfsPackage`) |
| Logo | `Bitmaps\Logo.PNG` | `Media/XUI/PressStart.xzp` (XUIZ) | 559x172 PNG, RGBA | XUIZ directory walk (new, about 40 lines) |
| Title icon | XDBF image `0x8000` | resource `54540859` in `default.xex` | 64x64 PNG, RGB | AES-128-CBC, de-blocking, LZX, XDBF (new; LZX is one ported file) |

Fallback for the title icon: the package thumbnail in the `nxeart` STFS header (64x64 PNG,
RGBA, at header offset `0x171A`). It needs no new decoding, but at 16 and 32 px it is a
blur of a face. The XEX image is a white "4" on a blue tile and is still legible at 16 px
(`analysis/disc-art/icon-candidates-16-32-48-64.png`, top row XEX, bottom row thumbnail).

All images are plain PNG or baseline JPEG. Nothing is DDS, DXT, tiled or byte-swapped, so
the only image work in .NET is resizing for the `.ico`.

What ticket 02 must know (details in §5):

1. The logo is in `PressStart.xzp`, not in `Startup.xzp` or `Common.xzp`. `Startup.xzp`
   holds no image at all, and `Common.xzp` holds only a 40x40 "B" button glyph.
2. There is no portrait box cover on the disc. The nearest thing is the dashboard slot tile
   `nxeslot.jpg`: 420x320 landscape, with the Top Spin 4 logo baked in over the same three
   players as the background.
3. The art is red. `nxebg.jpg` is a red arena with three players' faces in the middle third.
   There is no empty area for text, so the scrim matters. The current navy, ball-yellow and
   court-blue accents will clash. `PressStart.PNG` (1280x720, plain blue, no people) is a
   calmer alternative for the subdued Setup and Settings background.
4. The title icon is 64x64 at most. A 256 px `.ico` entry would be an upscale.

## 1. Inventory

Every image found in the sources the ticket names (`nxeart`, `default.xex`, `Startup.xzp`,
`Common.xzp`), plus `PressStart.xzp` and the loose PNGs that a census of the disc found
(`census.py` in the session scratchpad: every file's first four bytes against the PNG,
JPEG, DDS, XUIZ, STFS, XPR2 and XDBF magics). No DDS, XPR2 or other texture container
turned up outside the `.arc` archives, which the spec puts out of scope.

| Source | Entry | Purpose | Size | Format | Bytes |
|---|---|---|---|---|---|
| `nxeart` file | `nxebg.jpg` | NXE dashboard background | 1920x1080 | JPEG, baseline, YCbCr 4:4:4, 72 dpi | 642,844 |
| `nxeart` file | `nxeslot.jpg` | NXE dashboard slot (game tile, acts as box art) | 420x320 | JPEG | 188,982 |
| `nxeart` file | `DashStyle` | not an image: 4 zero bytes, NXE style flag **[inferred]** | — | — | 4 |
| `nxeart` header | thumbnail (`0x171A`) | package icon | 64x64 | PNG, RGBA | 13,176 |
| `nxeart` header | title thumbnail (`0x571A`) | title icon; same bytes as the thumbnail | 64x64 | PNG, RGBA | 13,176 |
| `default.xex` XDBF | image `0x8000` | title image (dashboard and Guide title icon) | 64x64 | PNG, RGB | 8,211 |
| `default.xex` XDBF | images `0x1`–`0x32` (40 of them) | achievement icons | 64x64 each | PNG, RGBA | 7,750–11,305 |
| `Startup.xzp` | none | two scenes (`S4Legals.xur`, `VideoScreen.xur`), no bitmaps | — | — | — |
| `Common.xzp` | `Skins\b.png` | Xbox "B" button glyph | 40x40 | PNG, RGBA | 2,388 |
| `PressStart.xzp` | `Bitmaps\PressStart.PNG` | press-start screen background | 1280x720 | PNG, RGB | 839,151 |
| `PressStart.xzp` | `Bitmaps\Logo.PNG` | Top Spin 4 logo ("TOPSPIN 4", "2K SPORTS") | 559x172 | PNG, RGBA | 79,075 |
| `PressStart.xzp` | `Bitmaps\LoadingIcon.PNG` | loading spinner (white) | 32x32 | PNG, RGBA | 3,208 |
| loose files | `Locale/{Eng,Fre,Ger,Ita,Spa}/Media/XUI/Loading.png` | localised "Loading" text | 160x25 | PNG, RGBA | 1,611–1,826 |

Also on the disc and not art for the Launcher: `AvatarAssetPack` and
`$SystemUpdate/su20076000_00000000` (both STFS, avatar items and a dashboard update), the
attract videos (`Media/XUI/*.wmv`, out of scope) and the `.arc` archives (out of scope).

`PressStartScreen.xur` names its bitmaps `..\Bitmaps\PressStart.PNG` (element
`ImageBackground`) and `..\Bitmaps\Logo.PNG` (element `XuiImageLogo`). That confirms their
purpose. `DownloadableContent.xur` also names `..\Bitmaps\ENLoading.PNG`, which is not in
the package. The game evidently loads the localised `Loading.png` instead **[inferred]**.

## 2. Getting the source files: from the `.iso` or the prepared folder

Reuse `launcher/src/Launcher.Core/XdvdfsImage.cs`. `XdvdfsImage.TryOpen(iso).Files()` lists
every file with its absolute offset, and `ReadStart(file, (int)file.Size)` reads a whole
small file. The four sources total about 2.5 MB, so read each one into a `byte[]`:

| Disc path (as `Files()` names it) | Size | Offset in this `.iso` |
|---|---|---|
| `nxeart` | 901,120 | `0x7ED3C800` |
| `default.xex` | 638,976 | `0x7CB77800` |
| `Media/XUI/PressStart.xzp` | 928,447 | `0x7C345800` |
| `Media/XUI/Common.xzp` | 71,895 | `0x76FFA800` |
| `Media/XUI/Startup.xzp` | 1,239 | `0x7BD51800` |

The game partition starts at `0xFD90000` (XGD2), which `XdvdfsImage` already tries. The
offsets are for this image only. Look files up by name, never by offset. In the prepared
folder the same files sit at the same relative paths (`File.ReadAllBytes`).

Path matching: `Files()` sorts case-insensitively and rejects duplicates. Compare paths with
`OrdinalIgnoreCase`, because the XUIZ names use mixed case (`Logo.PNG`).

## 3. Decoding each source

All multi-byte fields are big-endian unless marked LE.

### 3.1 `nxeart`: STFS (`PIRS`) package

The existing `launcher/src/Launcher.Core/StfsPackage.cs` already reads this package
unchanged. Run against the prepared copy, `StfsPackage.Open` + `Verify` + `ReadAll` gave:

```
PIRS type 00030000 title 54540859 name 'nxeart' header AD0E
verified
DashStyle 4 blocks 1+1 contiguous True
nxebg.jpg 642844 blocks 2+157 contiguous True
nxeslot.jpg 188982 blocks 159+47 contiguous True
```

The output is byte-identical to `scripts/stfs_extract.py`. The fields that matter:

- Magic `PIRS`. Header size (`+0x340`) `0xAD0E`, which passes `StfsHeader.Parse` (it needs
  `0x971A`–`0x10000`). Content type (`+0x344`) `0x00030000`, which is Theme in Xenia's
  content types and not in our `StfsContentType` enum (that is harmless: it is only cast).
  Metadata version (`+0x348`) 2. Title ID (`+0x360`) `54540859`. Display name (`+0x411`,
  UTF-16BE) `nxeart`. Description (`+0xD11`) "New Xbox Experience Game Assets". Title name
  (`+0x1691`) "Top Spin 4".
- Volume descriptor at `+0x379`: `24 00 01 01 00 00 00 00 …`. Size `0x24`; flags byte
  (`+0x37B`) `0x01`, so one hash table per level (read-only). File table: 1 block at block
  0. Total blocks (`+0x395`, BE) `0xCE` = 206, so there is a single level-0 hash table. Data
  starts at `align(0xAD0E, 0x1000) = 0xB000`. Every file is contiguous, so a decoder that
  ignored the hash chain would also work, but `StfsPackage` already handles both cases.
- Thumbnails (Free60 STFS page): thumbnail size `+0x1712`, title thumbnail size `+0x1716`,
  thumbnail data at `+0x171A`, title thumbnail data at `+0x571A`. Both sizes are 13,176 and
  both blobs are the same PNG. They lie within the header, so they need no block reads:
  `raw.Slice(0x171A, size)`.

**What to change in `StfsPackage`:** it opens only a path (`File.OpenHandle` +
`RandomAccess.Read`). To read `nxeart` straight from the `.iso`, add a constructor over a
`byte[]` (or a `Stream`) and route the private `Read(offset, count)` through it. Writing the
bytes to a temp file and calling `Open(path)` also works but leaves a file to clean up. Call
`Verify` before reading. On a hash failure, log it and treat the package as missing, as the
spec's testing section asks.

**Sources:** Free60 STFS format; Xenia `stfs_container_device.cc` and `stfs_xbox.h` (header
and descriptor layout, content types). The ReXGlue copy is in
`.tools/rexglue-source/src/filesystem/devices/stfs_container_device.cpp`.

### 3.2 `default.xex`: title image in the XDBF resource

The resource lives inside the encrypted, LZX-compressed PE image. The steps below are
ReXGlue's `XexModule::ReadImageCompressed`
(`.tools/rexglue-source/src/system/xex_module.cpp:623-723`), which is Xenia's. They were
verified end to end in .NET. A scratch console app (`xexcheck/Program.cs` in the session
scratchpad, about 90 lines plus MonoGame's `LzxDecoder.cs`) decoded `default.xex` to a
`0x180000`-byte image **byte-identical to `xextool -b`'s `analysis/xex/default.pe`**. It then
wrote the `0x8000` PNG, which is identical to the one the Python XDBF walk produced.

1. **XEX header.** Magic `XEX2` at `+0`. `+0x08` PE data offset (here `0x2000`). `+0x10`
   security info offset (here `0x90`). `+0x14` optional header count (here 15). From `+0x18`,
   that many (key u32, value u32) pairs. This is what `XexHeader.Parse` already walks
   (`launcher/src/Launcher.Core/XexHeader.cs`); extend it with two keys:
   - `0x000002FF`, resource info: value is an offset. At it: u32 total size, then
     `(size - 4) / 16` entries of `char name[8]` (NUL-padded ASCII), u32 virtual address, u32
     size. Here there are three: `54540859` at `0x82033E00` (size `0x7B336`), `LAYOUT` and
     `CORETECH`. The title resource is the one named by the title ID in upper-case hex.
   - `0x000003FF`, file format info: value is an offset. At it: u32 info size, u16
     encryption type (0 none, 1 normal), u16 compression type (0 none, 1 basic, 2 normal).
     For normal compression, `+8` u32 window size (here `0x8000`), `+0xC` the first block
     descriptor: u32 block size (here `0xF800`) and 20-byte SHA-1. This disc has encryption
     1 and compression 2.
2. **Security info** (at the `+0x10` offset; `xex2_info.h` `xex2_security_info`): `+0x4` u32
   image size (`0x180000`), `+0x110` u32 load address (`0x82000000`), `+0x150` 16-byte
   encrypted image key.
3. **Session key.** AES-128-CBC decrypt the 16 bytes at security `+0x150` with IV = 0 and the
   retail key `20 B1 85 A5 9D 28 FD C3 40 58 3F BB 08 96 BF 91`. Fall back to the devkit key
   (16 zero bytes) when the first block's hash fails. Here the result is
   `06 BF 87 5F 4D 2A E9 F6 7C 0B CB 99 ED 91 80 03`, the "Encryption Key" that
   `analysis/xex/default.xextool.txt` prints. .NET: `Aes.Create()`, `Key = retail`,
   `DecryptCbc(span, new byte[16], PaddingMode.None)`.
4. **Decrypt the image data.** Everything from the PE data offset to the end of the file,
   rounded down to 16 bytes, AES-128-CBC with the session key and IV = 0, in one pass (the
   CBC chain runs across blocks).
5. **De-block.** Start with the first block descriptor from the file format info. While the
   block size is not 0: hash `block_size` bytes at the current position, and stop as damaged
   when the hash differs. The block's first 24 bytes are the *next* block's size (u32) and
   SHA-1. After them come chunks: a u16 BE length, then that many bytes of LZX data, until a
   length of 0. Then jump to the current position + block size. Here there are 12 blocks,
   all hashes match, and 48 chunks hold 615,580 bytes of LZX data.
6. **LZX.** 48 chunks for a `0x180000` image is exactly one chunk per 32 KiB output frame.
   Xenia hands the joined chunks to libmspack `lzxd_init(window_bits = log2(window_size),
   reset_interval 0, input_buffer 0x8000, output_length = image_size)`
   (`.tools/rexglue-source/src/system/lzx.cpp`, `lzx_decompress`). In .NET, MonoGame's
   `LzxDecoder.cs` (a C# port of libmspack `lzxd.c`, dual-licensed MS-PL / LGPL 2.1) does the
   same. Create **one** `new LzxDecoder(15)` for the whole image, then call
   `Decompress(chunkStream, chunkLength, output, min(0x8000, imageSize - output.Length))`
   once per chunk, in order. This is how XNA's XNB reader drives it, and it gave the
   byte-identical image above. Compression 0 (none) is a plain copy. Compression 1 (basic)
   is a list of (data size, zero size) pairs after the info header (Xenia
   `ReadImageBasicCompressed`). This disc needs neither, but both are a few lines if the
   module should stay general.
7. **Find the resource.** Image offset = resource address − load address; here `0x33E00`,
   which starts with `XDBF`.
8. **XDBF** (ReXGlue `include/rex/system/util/xdbf_utils.h`, after FreeStyle Dash's `SPA.h`).
   Header, 24 bytes: magic `XDBF`, u32 version (`0x10000`), u32 entry table length (58), u32
   entries used (58), u32 free table length (30), u32 free used (1). Entries start at
   `+0x18`, 18 bytes each: u16 section (1 metadata, 2 image, 3 string table), u64 id, u32
   offset, u32 size. Data starts at `0x18 + entry_table_length * 18 + free_table_length * 8`
   (here `0x51C`), and each entry's offset is relative to it. The title image is section 2,
   id `0x8000` (ReXGlue `kXdbfIdTitle`): 8,211 bytes of PNG, 64x64 RGB.

**Cost [inferred]:** about 150 lines of new code, plus one third-party file whose licence
header must stay in place (MS-PL allows that; Launcher.Core has no package references today,
and keeping it so means vendoring the file rather than adding a NuGet). The retail key is a
published constant (it is in Xenia and ReXGlue), not game data. A test fixture can be built
with encryption 0 and compression 0 to avoid LZX in the tests. One LZX test should still run
a short real stream; MonoGame's tests or a hand-made stored-block stream can supply one.
If that cost is judged too high for 1.0, ship the `nxeart` thumbnail as the icon and add the
XEX path later. The slot interface stays the same.

**Sources:** Xenia `xex_module.cc` and `xex2_info.h` (ReXGlue copies cited above); libmspack
`lzxd.c`; MonoGame `LzxDecoder.cs`; Free60 XEX page; FreeStyle Dash `SPA.h`.

### 3.3 `Startup.xzp`, `Common.xzp`, `PressStart.xzp`: XUIZ packages

I found no public byte-level specification of the 360 `XUIZ` package. Valve's `.xzp` is an
unrelated format. The layout below was read off the three files, and every entry it yields
starts with the expected magic (`XUIB`, `XUIS`, PNG). Every PNG decodes fully and ends at its
stated size with `IEND`.

| Offset | Type | Meaning | Startup / Common / PressStart |
|---|---|---|---|
| `0x00` | char[4] | magic `XUIZ` | — |
| `0x04` | u32 | version **[inferred]** | 1 / 1 / 1 |
| `0x08` | u32 | total file length | `0x4D7` / `0x118D7` / `0xE2ABF` |
| `0x0C` | u32 | unknown, 1 in all three | 1 / 1 / 1 |
| `0x10` | u32 | directory length in bytes | `0x64` / `0x183` / `0x27C` |
| `0x14` | u16 | entry count | 2 / 7 / 10 |
| `0x16` | entries | `count` entries of: u32 size, u32 offset, u8 name length in UTF-16 units, then the name, UTF-16BE, `\` between folders, no terminator | — |

Data starts at `0x16 + directory length`. Each entry's offset is relative to it. Entries need
not be adjacent: in `Startup.xzp` the second entry starts `0x62` bytes after the first one
ends, so always use the stated offset. Checks for the decoder: magic, `0x08` equals the
length read, the directory fits, every name length is non-zero and `offset + size` stays
inside the file. Report anything else as damaged.

Entries in `PressStart.xzp` (absolute offset = `0x292` + offset):

| Name | Size | Offset |
|---|---|---|
| `Scenes\PressStartScreen.xur` | `0x52E` | `0x0` |
| `Bitmaps\PressStart.PNG` | `0xCCDEF` | `0x5A0` |
| `Bitmaps\Logo.PNG` | `0x134E3` | `0xCD3F7` |
| `Bitmaps\LoadingIcon.PNG` | `0xC88` | `0xE0936` |
| `Scenes\DownloadableContent.xur` | `0x481` | `0xE1628` |
| `Scenes\{Eng,Fre,Ita,Ger,Spa}\PressStartScreen.xus` | `0x200`–`0x240` | `0xE1B21`… |

`Common.xzp` holds `Skins\simple_skin.xur`, `Skins\b.png` and five
`Scenes\<lang>\StringTable.xus`. `Startup.xzp` holds `Scenes\S4Legals.xur` and
`Scenes\VideoScreen.xur`. `S4Legals.xur` has an `ImageBackground` element with no bitmap
path; the game must set it at run time **[inferred]**.

**Source:** filext.com lists the `XUIZ` magic and describes "a header, one or more directory
entries, and the file data". The rest is the observation above.

## 4. Writing the art in .NET

- **No pixel decoding is needed to store the art.** The chosen images are already PNG
  (logo, icon) or JPEG (background, box art). The spec says "writes them as PNG". Re-encoding
  the JPEGs as PNG gains nothing, and Launcher.Core has no image codec (System.Drawing is
  Windows-only and not referenced). I recommend writing the bytes unchanged, as
  `background.jpg`, `boxart.jpg`, `logo.png` and `icon.png`, and recording the file names in
  the index **[inferred]**. Avalonia's `Bitmap` (Skia) loads both formats, and so does
  `WindowIcon` from a PNG stream. If the spec's "PNG" must stand, transcode in Launcher.App
  with `new Bitmap(stream).Save(path)` (Avalonia always writes PNG).
- **Check before writing.** Accept an entry only when its magic is right (PNG
  `89 50 4E 47 0D 0A 1A 0A`; JPEG `FF D8 FF`) and the size the header states (PNG IHDR at
  `+16`: u32 width, u32 height; JPEG: the SOF0 marker) is plausible. Expected sizes are in
  §1.
- **The `.ico`.** An ICO file can hold PNG entries directly (Windows Vista and later). The
  layout is ICONDIR (u16 0, u16 type 1, u16 count; LE), then one ICONDIRENTRY of 16 bytes
  per size (u8 width, u8 height, 0 = 256, u8 0, u8 0, u16 planes 1, u16 bit count 32, u32
  byte count, u32 offset; LE), then the PNG data. Writing the single 64x64 PNG as the only
  entry needs no codec at all, and Windows scales it down for 16, 24, 32 and 48 px. For
  sharper small sizes, resize in Launcher.App (Avalonia `Bitmap.CreateScaledBitmap` with
  `BitmapInterpolationMode.HighQuality`, then `Save` to a stream) and write 16, 24, 32, 48
  and 64. Pillow's preview with those sizes is
  `analysis/disc-art/title-icon-preview.ico`. Leave out 256: the source is 64 px.

## 5. Notes for the look (ticket 02)

- **Background, `nxebg.jpg`.** A saturated red arena with stadium lights at top right and
  red light streaks at the lower left. Agassi is in the middle, Nadal on the left and
  Serena Williams on the right, from about x = 250 to 1850. No logo, no text. Every region
  is busy, so text needs a panel or a strong scrim, as the spec already requires. Covering
  with `UniformToFill` on a 16:10 window crops the sides a little and keeps the faces.
- **Box art, `nxeslot.jpg`.** The same three players with the full Top Spin 4 logo across
  the bottom third. It is 420x320 (21:16 landscape), not a portrait cover. Next to PLAY it
  is a landscape card. Shown together with `Logo.PNG`, the logo appears twice on the
  page **[inferred]**. The marketplace box art (portrait) comes from Xbox Live servers, not
  from the disc.
- **Logo, `Logo.PNG`.** White and silver lettering, a red "4", a dark outline and a soft
  drop shadow on a transparent background. It reads on both dark and red backgrounds. Its
  aspect is 3.25:1 (559x172), so it scales down well for the small header logo.
- **Calmer background, `PressStart.PNG`.** 1280x720, soft blue light, no people. It suits
  the "more subdued" Setup and Settings pages better than a darkened `nxebg.jpg`
  **[inferred]**. It is 1280x720, so it upscales on large windows, but it is so soft that
  this will hardly show.
- **Accent colours.** The art is red, white and silver (`nxebg`, `nxeslot`, the logo's
  "4") with blue in the icon and the press-start screen. The current ball-yellow and
  court-blue accents will need retuning toward red and white.
- **Likenesses.** The background and box art show real players. They are shown unchanged
  from the player's disc, which is what the spec's credit line already covers.

## Evidence

All of it is in `analysis/disc-art/` (git-ignored, on the owner's machine):

- `nxeart/`: `nxebg.jpg`, `nxeslot.jpg`, `DashStyle` (via `scripts/stfs_extract.py`, and
  matched byte for byte by `StfsPackage`), `thumbnail.png`, `title_thumbnail.png` (header).
- `xex-spa/`: `img_8000.png` (title image) and the 40 achievement icons `img_<id>.png`, from
  the XDBF in `analysis/xex/default.pe`, plus `title-image-8000.dotnet.png` from the .NET
  pipeline (same bytes).
- `xzp-Startup/`, `xzp-Common/`, `xzp-PressStart/`: every entry of each package, at its path.
- `icon-candidates-16-32-48-64.png`: the two icon candidates at the four sizes.
- `title-icon-preview.ico`: a multi-size `.ico` from the XEX title image.

The scratch scripts (`stfs_header.py`, `xex_headers.py`, `xex_decrypt.py`, `xdbf.py`,
`xzp.py`, `census.py`, `iso_check.py` and the `xexcheck` and `stfscheck` .NET projects) are
in the session scratchpad and are not kept. The ticket 02 implementer writes the real code in
Launcher.Core, so none of them is worth committing.

## Sources

- Free60, STFS: <https://free60.org/System-Software/Formats/STFS/>
- Free60, XEX: <https://free60.org/System-Software/Formats/XEX/>
- Xenia, `src/xenia/vfs/devices/stfs_container_device.cc`, `src/xenia/cpu/xex_module.cc`,
  `src/xenia/kernel/util/xex2_info.h`, `src/xenia/kernel/util/xdbf_utils.h`:
  <https://github.com/xenia-project/xenia>. Local ReXGlue copies:
  `.tools/rexglue-source/src/system/xex_module.cpp:35-57, 623-723`,
  `.tools/rexglue-source/src/system/lzx.cpp`,
  `.tools/rexglue-source/include/rex/system/util/xex2_info.h:564-575`,
  `.tools/rexglue-source/include/rex/system/util/xdbf_utils.h:28-56`,
  `.tools/rexglue-source/src/filesystem/devices/stfs_container_device.cpp`
- libmspack `lzxd.c`: <https://github.com/kyz/libmspack/blob/master/libmspack/mspack/lzxd.c>
- MonoGame `LzxDecoder.cs` (MS-PL / LGPL 2.1):
  <https://github.com/MonoGame/MonoGame/blob/develop/MonoGame.Framework/Content/LzxDecoder.cs>
- FreeStyle Dash `SPA.h` (XDBF): <https://github.com/oukiar/freestyledash/blob/master/Freestyle/Tools/XEX/SPA.h>
- XZP magic: <https://filext.com/file-extension/XZP>
- ICO with PNG entries: <https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10)>
- This repo: `launcher/src/Launcher.Core/XdvdfsImage.cs`, `StfsPackage.cs`, `XexHeader.cs`;
  `scripts/stfs_extract.py`; `analysis/xex/default.xextool.txt`
