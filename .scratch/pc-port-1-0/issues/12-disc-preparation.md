# 12: Disc and TU1 preparation

**What to build:** The launcher takes the player's disc image and TU1 package, checks they are the Reference build, prepares the game data and caches it per user.

**Blocked by:** 03 (Skeleton and pinned toolchain bootstrap)

**Status:** ready-for-agent

- [x] A disc image and TU1 package are accepted as input
- [x] Media ID, module versions and TU1 package identity are checked, and a wrong disc, missing TU1 or wrong TU each give a clear, distinct error
- [x] Region-free or security-sector-stripped images with the right game partition are accepted
- [x] Extraction and TU1 patching produce the prepared data, cached per user
- [x] Later launches reuse the cache without redoing the work
- [x] Unit tests cover the validation outcomes, using synthetic headers rather than game data

## Comments

**2026-10-24 (Claude):** Done in `launcher/` (C#, .NET SDK 10.0.401 pinned by `launcher/global.json`, fetched or linked by `scripts/bootstrap.ps1`). The XDVDFS and STFS readers and the cache scheme are copied from WWE All Stars' launcher (ADR-0004); validation is new.

- **Where:** `launcher/src/Launcher.Core` (`Preparation` is the entry point; `DiscSetup`, `TitleUpdateSetup`, `PreparedFolder`, `XexHeader`, `ReferenceBuild`, `UserDirectories`), console front end `launcher/src/Launcher.Cli` (`topspin4-setup --disc <iso> --tu <package> [--cache <dir>]`), tests `launcher/tests/Launcher.Tests` (53, all synthetic). `pwsh scripts/build.ps1 -Action LauncherTest` runs them; `-Action Launcher` publishes `out/launcher/publish/topspin4-setup.exe`.
- **Manifests:** `launcher/disc-manifest.tsv` (230 files, 5,737,767,977 bytes, game partition only) and `launcher/tu1-manifest.tsv` (7 files), written by `scripts/write_manifests.py` from the owner's ISO and `updatedata/`. Module hashes agree with `scripts/build.ps1`.
- **Checks:** disc: each Module's XEX execution info (`0x00040006`; the brief's `0x00018002` is the checksum/timestamp header) must be title 54540859, media 60819732, v0.0.0.3, then the file list and sizes, then the three Modules' SHA-256. TU1: STFS title update for that title and media ID, every block against its hash, each `.xexp`'s delta descriptor (`0x000005FF`) must target v0.0.1.3, then every file against the manifest. Errors: `WrongDisc`, `NoTitleUpdate` (none chosen, not found, or not a package), `WrongTitleUpdate`, plus `NotAnXboxImage`, `ImageNotFound`, `DamagedFile`, `DiskFull`, `NoPermission`; the CLI exits with 10 + the error's number.
- **Cache:** `%LOCALAPPDATA%\TopSpin4Recomp\gamedata` and `\updatedata`, the layout `--game_data_root` / `--update_data_root` expect. Under Wine/Proton it is `$XDG_DATA_HOME` (else `~/.local/share`) `/TopSpin4Recomp` through the Z: drive. Each folder is written as `<folder>.partial`, every file verified, then marked (`.topspin4-prepared`, naming the manifest version) and renamed; a later launch is "done" from the marker plus file sizes and reads neither input. "TU1 patching" here means unpacking TU1 into `updatedata/`: the runtime applies each `.xexp` from `update:` at load, as `scripts/smoke.ps1` already runs it.
- **Manual end-to-end run (owner's ISO and TU1, scratch cache):** first run prepared in ~8 s (warm file cache), every file byte-identical to `gamedata/` and `updatedata/` by SHA-256 (plus the marker); second run "Reused the prepared data" in 91 ms with the marker untouched, also with no inputs given. A run killed after 3 s left only `gamedata.partial` (110 files), a no-input run then refused (`ImageNotFound`, not complete), and the next run resumed and finished with identical files.
- **Open:** the region-free claim is tested on bytes outside the game partition (zeroed or filled); a dump whose XEX region field was patched would fail the Module hash check, and whether the owner's XEX are themselves patched is still unverified (module-inspection.md). The Wine detection and XDG path are unit-tested but not yet run on a Steam Deck. No smoke run or Oracle comparison applies: the runtime is unchanged and reads the same bytes as before.
