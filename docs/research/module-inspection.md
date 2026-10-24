# Module inspection: disc, executables and load chain

Results from inspecting the owner's disc image on 2026-10-24. This answers
open questions 1, 2 and 3 in [feasibility.md](feasibility.md). Raw tool output is
in `analysis/xex/` (git-ignored).

## Disc image

- Image: `.iso/Top Spin 4 [RF]/Top Spin 4 [RF].iso`, 7,838,695,424 bytes,
  SHA-1 `221329a26c010cc2d76513837674f74d02be9ca1`, CRC-32 `8788d13d`
  (`analysis/iso-hashes.txt`).
- The matching Redump entry is [disc 57594](http://redump.org/disc/57594/): TT-2137,
  media ID `60819732`, XGD2 (Wave 6), and the same size to the byte. Its SHA-1 is
  `1b2694a0ee39be02a145afd426f082b7210d1278`, **so this image is not a byte-exact
  Redump dump.**
- Where it differs: both security-sector ranges Redump lists (sectors
  108976-113071 and 3719872-3723967) are zero-filled in this image. The video
  partition holds data. The `[RF]` tag usually means "region free". Whether the
  game partition or the XEX files themselves were altered is **unverified**.
  The XEX headers still read `Retail`, `Encrypted`, `All Regions`. A
  region-free patch would normally only touch header fields, not code.
- Extracted with `extract-xiso` 2.7.1 to `gamedata/`: 230 files, 5,737,767,977
  bytes.

## Executables

All three are v0.0.0.3, title ID `54540859`, media ID `60819732`, built with
XDK 2.0.20209 and the VS2010 compiler (C1/C2 16.0.10224). Each is retail,
compressed and encrypted. `XexTool -b` decrypts and decompresses them to
`analysis/xex/*.pe`.

| Module | Original name | Load range | Entry | Built | Static libraries of note |
|---|---|---|---|---|---|
| `default.xex` | `Boot.exe` | `82000000-82180000` | `82019F68` | 2011-01-12 22:40 | LIBCMT and XAPILIB only |
| `Loader_DLL.xex` | `Loader_DLL.dll` | `83000000-835E0000` | `833E0E80` | 2011-01-12 22:05 | D3D9LTCG, D3DX9, XUI (run, render, video), XAUDIO2, XONLINE, XNET, XMP |
| `Swing_DLL.xex` | `Swing_DLL.dll` | `88000000-88DD0000` | `88562DB0` | 2011-01-13 12:55 | D3D9LTCG, D3DX9, XAUDIO2, XAUD, XHV2, XMIC, XPARTY, XONLINE, XNET, VCOMP (OpenMP) |

- **The load ranges do not overlap.** All three images can live in one guest
  address space at their preferred bases, with no relocation.
- `default.xex` also carries two resources, `LAYOUT` and `CORETECH`. They match
  the `Layoutall.bin` and `CoreTech_Xenon.bin` files on the disc.
- Both DLLs keep their PDB paths: `c:\Builds\Xbox 360\_Master\<name>\<name>.pdb`.
  Swing contains `Pam::Game::...` type names, which confirms the PAM engine
  lineage that feasibility.md only inferred.

## Load chain (answers open question 1)

`default.xex` → `Loader_DLL.xex` → `Swing_DLL.xex`

- **Neither DLL imports anything from another title module.** All three import
  only `xam.xex` and `xboxkrnl.exe`. Any calls between modules go through
  `XexLoadImage` plus `XexGetProcedureAddress` at runtime. The resolved import
  lists are in `analysis/xex/<module>.imports.txt`.
- `default.xex` holds the string `game:\Loader_DLL.xex` and imports `XexLoadImage`,
  `XexGetProcedureAddress`, `XexUnloadImage`, `XexLoadImageHeaders` and the
  `XamLoader*LaunchData` set.
- `Loader_DLL` holds `game:\Swing_DLL.xex` and `game:\boot.bin`. It imports
  `XexLoadImage`, `XexGetProcedureAddress` and **`XexUnloadImageAndExitThread`**.
  That import suggests (unverified) that Loader hands off to Swing and unloads
  itself, so the two DLLs may never be resident together.
- `Swing_DLL` also imports `XexLoadImage`, `XexLoadImageHeaders` and
  `XexUnloadImage`, and it holds the string `DLC_RestartGameNewDLL`. A DLC
  package may be able to supply a replacement game DLL. **This needs checking
  before DLC is supported**, because a recompiled build cannot load new guest
  code.
- Each DLL exports three ordinals (1-3), read from the XEX export table:
  - **Loader:** `8348B0E0`, `83492C70`, `83492920`.
  - **Swing:** `885F5218`, `885F5228`, `885F5210`. These are 8 bytes apart, so they're probably data (an interface table or variables), not code.

## Direct3D location (answers open question 3)

**Both DLLs statically link their own copy of D3D9LTCG and XGRAPHC.** There is
no shared D3D module. Renderer hooks that match D3D functions by address must be
generated per module. Loader's copy probably drives the boot and loading
screens (it is the module that links XUI). Swing's copy drives the game. Both of
those roles are unverified until the Ghidra pass.

## Other findings

- **No Kinect:** no `Nui*` imports in any module, so motion input is not a
  factor.
- **Online and voice:** all the online and voice code sits in the two DLLs:
  XNet, XnpLogon, QoS, `XamVoice*` and party.
- **Threading:** Swing links VCOMP, so the game uses OpenMP worker threads.
## Title Update 1 (answers open question 2)

- Package: `TU_1A5822P_000000C000000.0000000000083`, a `LIVE` STFS package, display
  name "Top Spin 4 Title Update #1", title ID `54540859`, media ID `60819732`.
  The owner's copy is kept in `.iso/TU1/`. `scripts/stfs_extract.py` unpacks it to `updatedata/`.
- Contents: `default.xexp`, `Loader_DLL.xexp` and `Swing_DLL.xexp`, plus replacement
  `Boot.bin`, `CoreTech_Xenon.bin` and `Layoutall.bin`, and a new `TitleUpdate.arc`.
- **TU1 patches all three modules.** Each `.xexp` applies cleanly with `XexTool -p`
  and raises the module to v0.0.1.3. The patched images are in `analysis/xex/tu1/`.

| Module | Built | Image size (base → TU1) | Code/data words changed |
|---|---|---|---|
| `default` | 2011-04-04 | `180000` → `130000` | 41.9% |
| `Loader_DLL` | 2011-04-04 | `5E0000` → `5D0000` | 10.0% |
| `Swing_DLL` | 2011-04-07 | `DD0000` → `DD0000` | 78.8% |

- Entry points are unchanged. The export tables moved: Loader's is now at `83517E60`
  and Swing's at `88B02550`.
- **Swing_DLL is effectively a relink:** 198 of its 221 64 KiB pages differ.
  Addresses found in the base image do not carry over to TU1, so **all analysis
  and the recomp should target the TU1 images directly.** WWE All Stars did the
  same with its TU2.
