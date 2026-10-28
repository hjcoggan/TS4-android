# 02: Disc art module

**What to build:** The Disc art module in Launcher.Core. Given a disc `.iso` or the prepared disc folder, it decodes the images ticket 01 recommends and writes them as PNG to an `art` folder in the per-user folder, with a multi-size `.ico` from the title icon and a small index naming what was found and the disc it came from. When the setup page accepts a disc, the art is read from the `.iso`; a failure is logged and never fails the disc row. Spec: `.scratch/launcher-look/spec.md` (Implementation Decisions "Disc art module", "Setup hook"); format details in `docs/research/disc-art.md`.

**Blocked by:** 01 (Disc art inventory); `.scratch/launcher-ux/issues/04-setup-page-check-on-pick.md` (the disc row's accept step)

**Status:** resolved

- [x] Launcher.Tests: fixture packages generated in the tests (no game art) give the PNGs, the `.ico` and the index; a package without the image gives no file and no error; a damaged package gives a logged reason and no file
- [x] Launcher.Tests: accepting a disc on the setup page writes the art; an art failure leaves the disc row accepted
- [x] Run once against the owner's `.iso`: the art folder holds the four images, each matching ticket 01's evidence (five, after the spec amendment)
- [x] `pwsh scripts/build.ps1 -Action Check` passes

## Comments

2026-10-28, implemented (spec Amendments applied: five slots, title icon from the XEX, images stored as they come):

- Module: `launcher/src/Launcher.Core/DiscArt.cs` (`DiscArt.FromIso`, `DiscArt.FromFolder`), `DiscArtFolder.cs` (writes the folder, and `DiscArtFolder.Open` reads it back), `XuizPackage.cs`, `XexTitleImage.cs` (AES, hash-chained blocks, LZX, XDBF), `ArtImageFile.cs` (PNG/JPEG header and end checks), `DiscFileSource.cs` (iso or prepared folder), `Vendor/LzxDecoder.cs` (MonoGame, MS-PL header kept, origin noted beside it). `StfsPackage` gained `Open(byte[], name)`. Setup hook: `SetupFlow` takes `readDiscArt`; `MainWindow` passes `DiscArt.FromIso` with `<per-user folder>/art`.
- Box 1: `DiscArtTests` (23 cases): generated nxeart STFS, XUIZ and XEX (plain, encrypted, encrypted and LZX-compressed with real stored LZX blocks over two frames and two hash-chained blocks, devkit key) give the images byte for byte, the `.ico` and `index.txt`; a missing source, a package without the image or a Module without a resource gives no file and no error; a damaged nxeart (block hash), XUIZ (truncated) or Module (block hash) gives one logged line and no file for it while the other sources still count; a cut-off PNG is logged and left out; a second disc replaces the first's files, also across formats; `DiscArtFolder.Open` never follows an index entry out of the folder.
- Box 2: `SetupFlowArtTests` (6 tests): accepting a disc `.iso` writes a complete art folder before the row reports the change; a throwing hook and a damaged source inside the disc both leave the row `Accepted` with a log line; a refused disc reads no art.
- Box 3: run once against `.iso/Top Spin 4 [RF]/Top Spin 4 [RF].iso` into a scratch folder (not the real per-user one) with a scratch console app: 92 ms, no problems, all five files SHA-256-identical to `analysis/disc-art/` (`nxeart/nxebg.jpg`, `nxeslot.jpg`, `xzp-PressStart/Bitmaps/Logo.PNG`, `PressStart.PNG`, `xex-spa/img_8000.png` and `title-image-8000.dotnet.png`); `topspin4.ico` is 8,233 bytes (22 + the 8,211-byte PNG); `DiscArtFolder.Open(...).IsComplete` is true.
- Box 4: `pwsh scripts/build.ps1 -Action Check` passed: pytest 110, host tests 403 cases, Launcher.Tests 313 passed, 1 skipped (the existing gui-exe pipe test). The worktree had no `.tools` (junctioned to the main checkout's) and the freshly built host-test exe needs the main checkout's `out/build/win-amd64-relwithdebinfo` DLLs on `PATH` (exit -1073741515 without); nothing in the repo changed for that.

For ticket 03: `DiscArt.FolderName` is `art`, kept in the per-user folder that holds the settings (`Path.Combine(userRoot, DiscArt.FolderName)` in `MainWindow`). `DiscArtFolder.Open(folder)` gives `ImagePath(slot)` (null when absent), `Has(slot)`, `Found`, `IconPath` (the `.ico`), `IsComplete`, `Source`. Files are `background`, `boxart`, `logo`, `icon`, `setup-background` with `.jpg` or `.png` as the disc had them (ask `ImagePath`, never assume), plus `topspin4.ico` and `index.txt`. To re-read from the prepared folder: `DiscArt.FromFolder(Path.Combine(preparation.GameDataRoot), artFolder, log)`; not wired yet (ticket 03: "if the art folder is missing or incomplete").
