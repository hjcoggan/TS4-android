# Top Spin 4 PC recompilation: feasibility

Research date: 2026-10-24. This is a desk study. No game files were downloaded,
dumped or run for it, and no build was attempted. Every factual claim has a URL
or a local path beside it. **[community]** marks forum, review or player claims,
and **[inferred]** marks my own reasoning. Unverified points are collected under
"Open questions" at the end.

## Executive summary

- **Version to target: Xbox 360**, title ID `54540859`, media ID `60819732`,
  base executable version `0.0.0.3`, plus **Title Update 1** (the only TU listed,
  base version `00000003`). The PS3 version is a reasonable fallback on paper, but
  its toolchain (ps3recomp) is far less mature. The Wii version is a different
  game on a different engine.
- **Verdict: feasible, with caveats.** The game boots to gameplay in Xenia-family
  emulators, and its code is smaller than WWE All Stars'. Two things are new
  compared with the user's earlier ports: the game logic lives in **two runtime
  DLL modules** (`Loader_DLL.xex` and `Swing_DLL.xex`), which ReXGlue supports but
  still has open bugs for, and the game has a known **frame-rate-coupled
  simulation** (above 60 fps changes game speed).
- **Approach: recompilation first, on ReXGlue v0.10.0**, with a hybrid layer of
  hand-written C++ hooks for online, the dead title server, timing and
  enhancements. Reuse the WWE All Stars skeleton, its patches and its native
  renderer. Use the existing public Top Spin 4 decompilation
  (`g-guthrie/topspin4-decomp`) as a symbol source, not as the build path.
- **Renderer: Vulkan only**, matching ADR-0003 in WWE All Stars. It is the
  path the user's native renderer already targets, and it covers Steam Deck and
  Linux.

### Were the earlier projects Xbox 360 or PS3?

Checked locally, not assumed. Both are Xbox 360 ReXGlue ports:

- `D:\Projects\WWE_ALLSTARS-RECOMP\README.md`: "An unofficial Windows version of
  WWE All Stars (Xbox 360, 2011) ... translated ahead of time ... with ReXGlue,
  and a native Vulkan renderer draws the game's own Direct3D 9 calls." Its
  `CLAUDE.md` pins ReXGlue v0.10.0 and Title Update 2.
- `D:\Projects\SVR2011-RECOMP\README.md`: "An unofficial Windows version of the
  Xbox 360 game, built by recompiling its code ... with ReXGlue."
- Neither manifest has a `[[modules]]` entry
  (`wweallstars_manifest.toml`, `svr2011_manifest.toml`), so **neither project has
  exercised ReXGlue's multi-module (DLL) path**. That matters for Top Spin 4.
- `D:\Projects\SmackDown2Recomp` is a PS1 port on psxrecomp, so it does not count
  as PS3 experience.

The PS3 path is therefore covered only as a fallback.

---

## 1. Best version to target

### Comparison

| | Xbox 360 | PS3 | Wii |
|---|---|---|---|
| IDs | Title `54540859`, media `60819732`, exe `0.0.0.3` ([decomp targets.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/targets.md)) | Disc `BLUS30427` (US), `BLES00668` (EU); digital `NPEB00830`; demos `NPUB90542`, `NPHB00317` ([RPCS3 compat API](https://rpcs3.net/compatibility?api=v1&g=top+spin)) | `R28E54`, `R28P54` ([Dolphin wiki](https://wiki.dolphin-emu.org/index.php?title=Top_Spin_4)) |
| Executable | XEX2: `default.xex` (639 KB, image `0x82000000`) loads `Loader_DLL.xex` (`0x83000000`) and `Swing_DLL.xex` (`0x88000000`) ([targets.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/targets.md), [analysis-status.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/analysis-status.md)) | SELF/EBOOT.BIN (not inspected) | DOL (not inspected) |
| Encryption and packing | Retail XEX2, LZX-compressed. XEXLoaderWV decrypts it; it warns "out of input bytes (LzxDecompression)" after loading every module ([analysis-status.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/analysis-status.md)) | Retail SELF is encrypted; disc builds decrypt with firmware keys, digital `NP*` builds also need a licence **[inferred, standard PS3 practice]** | Wii discs are encrypted per partition, which Dolphin handles **[inferred]** |
| Symbols or unstripped builds | No PDB or map found. The original PE names survive (`Boot.exe`, `Loader_DLL.dll`, `Swing_DLL.dll`), and the compiler is identified as Xbox 360 MSVC `16.0.10224.0` ([toolchain.json](https://github.com/g-guthrie/topspin4-decomp/blob/main/config/toolchain.json)). Script-command registration tables give high-confidence names ([symbols.csv](https://github.com/g-guthrie/topspin4-decomp/blob/main/config/symbols.csv)) | Nothing found | Nothing found |
| Demos, prototypes, kiosk builds | A public demo shipped on Xbox Live around 2011-03-01 ([TheHDRoom](https://thehdroom.com/news/top-spin-4-and-mlb-2k11-demos-hits-xbox-360-ps3-37400/), [Gameranx](https://gameranx.com/updates/id/1464/article/top-spin-4-demo-now-available/)). No prototype or kiosk build was found | Demos `NPUB90542`, `NPHB00317` exist ([RPCS3](https://rpcs3.net/compatibility?api=v1&g=top+spin)) | None found |
| Updates | **TU1**: version 1, base `00000003`, SHA-1 `95FB3510...C929`, uploaded 2011-06-08 ([XboxUnity API](http://xboxunity.net/Resources/Lib/TitleUpdateInfo.php?titleid=54540859)). The game also had a separate server-delivered data-patch system that is now dead (see §3) | Patch **1.02**, about 8.3 MB, still listed as `status="alive"` on Sony's update server for both disc IDs ([BLUS30427](https://a0.ww.np.dl.playstation.net/tpl/np/BLUS30427/BLUS30427-ver.xml), [BLES00668](https://a0.ww.np.dl.playstation.net/tpl/np/BLES00668/BLES00668-ver.xml)) | None found |
| What the patch changed | May 2011 notes for 360: D-pad player movement, career ranking-point fix, camera settings now saved, World Tour crashes, online lobby and NAT fixes, cheat-profile detection ([Operation Sports, 2011-05-09](https://forums.operationsports.com/forums/forum/other-sports/tennis/421154-top-spin-4-patch-available-now-post-your-impressions-here-ps3-360)) **[community repost of the official notes]** | Same plus PS Move, 4:3 text, 3D icon and save fixes (same source) | n/a |
| DLC | Only an Agassi preorder outfit is known ([Gaming Nexus](https://gamingnexus.com/News/21188/Tennis-Stars-Align-for-Top-Spin-4); a [2019 player](https://forums.operationsports.com/forums/forum/other-sports/tennis/882481-is-there-a-way-i-can-get-the-top-spin-4-patches-dlcs-and-updates) **[community]**) | Same | n/a |
| Content | 25 licensed players, Player Match, World Tour, 2K Open online ([Complex](https://www.complex.com/pop-culture/a/peter-rubin/top-spin-4-review), [Wikipedia](https://en.wikipedia.org/wiki/Top_Spin_4)) | Same, plus PS Move and 3D ([Wikipedia](https://en.wikipedia.org/wiki/Top_Spin_4)) | A different game: no online, motion-only, `.brres` archives ([GameSpot Wii review](https://www.gamespot.com/reviews/top-spin-4-review/1900-6305302/), [ZenHAX](https://www.zenhax.com/viewtopic.php@t=16776.html) **[community]**) |
| Reception | Metacritic 84 | Metacritic 82 | Metacritic 54 ([Wikipedia](https://en.wikipedia.org/wiki/Top_Spin_4)) |
| Emulation | Gameplay or playable, see below | **Playable** for `BLUS30427`/`BLES00668` (2018-02-10), `NPEB00830` (2020-06-12) and both demos ([RPCS3 compat API](https://rpcs3.net/compatibility?api=v1&g=top+spin)) | **Playable** (4 stars). The frame rate drops from 60 to 30 between points; the wiki says it is unclear whether real hardware does the same ([Dolphin wiki](https://wiki.dolphin-emu.org/index.php?title=Top_Spin_4)) |

### Xbox 360 emulator status: Xenia Edge first, then Canary

The user asked for **Xenia Edge** to be the primary 360 reference.

- **What Edge is:** [has207/xenia-edge](https://github.com/has207/xenia-edge) is a
  fork of Xenia Canary. Its README says the focus is "faster iteration, higher
  default game compatibility, usability and platform support". It ships native
  Windows, Linux and macOS builds and recommends native Linux over Wine/Proton.
- **Maintenance:** created 2025-08-23, last push 2026-10-24, 566 stars
  ([API](https://api.github.com/repos/has207/xenia-edge)). It published five
  releases on 2026-10-24 alone ([releases](https://api.github.com/repos/has207/xenia-edge/releases)).
  Its `edge` branch is **2,085 commits ahead of and 3 behind** Canary's
  `canary_experimental` (GitHub compare API, checked 2026-10-24).
- **How it differs from Canary:**
  - GPU backends are `d3d12`, `metal`, `vulkan` and `null`
    ([src/xenia/gpu](https://github.com/has207/xenia-edge/tree/edge/src/xenia/gpu)).
  - CPU backends are `x64` and `a64`
    ([src/xenia/cpu/backend](https://github.com/has207/xenia-edge/tree/edge/src/xenia/cpu/backend)).
  - It adds system link by default and Xlink Kai support ([README](https://github.com/has207/xenia-edge)).
  - Recent releases contain Xenos accuracy fixes: fixed-point blending in FSI/ROV,
    16-bit EDRAM scaling, packed mip tails, tessellated strips, and Vulkan depth
    bias ([releases](https://api.github.com/repos/has207/xenia-edge/releases)).
- **Top Spin 4 data in Edge:**
  - Edge's issue tracker has **no Top Spin 4 report**: a search of
    `repo:has207/xenia-edge "top spin"` returned 0 results.
  - Edge bundles a compatibility database,
    [`assets/game-compatibility/master.json`](https://github.com/has207/xenia-edge/blob/edge/assets/game-compatibility/master.json).
    Its Top Spin 4 entry mirrors Xenia master issue #867: state **Gameplay**,
    labels `gpu-drawing-corrupt`, `marketplace-delisted` and `tech-middleware-havok`.
  - Edge's own README says to prefer the Canary page's information when both
    exist.
- **Falling back to Canary,** because Edge has no game-specific report.
  [xenia-canary/game-compatibility#358](https://github.com/xenia-canary/game-compatibility/issues/358)
  carries the labels `state-gameplay`, `vsync-off-speedup`, `gpu-xps` and
  `tech-middleware-havok`. A collaborator's 2026-07-20 comment on build
  `a635ac6` says:
  - the game runs on default settings and a full match was completed;
  - upscaling crashes it, either as a host crash on the title screen or as
    "graphics device lost" when gameplay loads;
  - unlocking the frame rate above 60 changes game speed.
- **Older Xenia master reports** ([#867](https://github.com/xenia-project/game-compatibility/issues/867)):
  - 2018: menu hang with ring-buffer `ExecutePacketType0/3` overflows, plus
    unimplemented `XamParty*` and `XamShowCommunitySessionsUI` calls.
  - 2020: about 61 fps, with vertex explosions and bad camera angles.
  - 2021: a freeze on the first serve in one build.
  - 2023: heavy flicker on video cross-fades between sets.

**Action:** before codegen, run the exact dump with TU1 on current Edge and
record a baseline (match completes, any flicker, behaviour at 30, 60 and 120 fps).
None of the reports above names the media ID or the TU it was tested with.

### Recommendation

**Xbox 360, title `54540859`, media `60819732`, base `0.0.0.3`, with TU1**
(XboxUnity ID `21675`).

- The 360 build matches the user's toolchain, renderer and launcher, and the
  WWE projects already handle XEX and TU delta patches (`rexglue-tu2-update.patch`
  in WWE All Stars).
- It has the best tooling: a public Ghidra pipeline and symbols in
  `topspin4-decomp`, plus active Xenia forks to use as oracles.
- It has the same content as PS3 apart from PS Move.
- Apply TU1. It enables D-pad movement and fixes career ranking points and the
  camera-settings save bug. Record which modules the TU patches (an open question).

The PS3 version is viable only through ps3recomp, which boots no game on Linux
or macOS yet (§2). The Wii version is out: it is a different, worse-reviewed
game on another engine.

---

## 2. Every tool needed

Repository dates are GitHub `pushed_at` values, queried on 2026-10-24 unless
noted otherwise. The full list is in the "Tool inventory" table at the end.

### Extraction

- **Disc:** the user's own `scripts/inspect_disc.py` from WWE All Stars, or
  [XboxDev/extract-xiso](https://github.com/XboxDev/extract-xiso) (pushed 2026-09-11).
  The Top Spin 4 disc is UDF 1.5 with the usual 360 video partition (`$SystemUpdate`,
  `AUDIO_TS`, `VIDEO_TS`) ([targets.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/targets.md)).
- **XEX:** ReXGlue reads XEX directly. For inspection use
  [XexTool rebuilds](https://github.com/Team-Resurgent/XexTool) (pushed 2026-10-02),
  [mrtitanic777/XexTool-RE](https://github.com/mrtitanic777/XexTool-RE) (a clean-room
  rebuild of xorloser's XexTool, pushed 2026-07-15), or the user's `tools/xexdump`.
- **TU (STFS/LIVE) and DLC:** Xenia Edge can install a TU. For the format, see
  [emoose/xbox-reversing](https://github.com/emoose/xbox-reversing) (STFS/GDFX notes,
  2024-10-22). [Velocity](https://github.com/Gualdimar/Velocity) is archived (2016).
- **PS3 fallback:** RPCS3 installs PKGs and has a binary-decryption utility
  **[inferred from RPCS3's UI, not checked here]**.

### Recompilers

- **[ReXGlue SDK](https://github.com/rexglue/rexglue-sdk), recommended.** Pinned
  release v0.10.0 (2026-08-21), last push 2026-10-02. It generates C++ from PPC
  with a Xenia-derived runtime, D3D12 and Vulkan graphics backends
  ([src/graphics](https://github.com/rexglue/rexglue-sdk/tree/main/src/graphics)),
  and CI for win-amd64, linux-amd64 and linux-arm64 ([README](https://github.com/rexglue/rexglue-sdk)).
  - **Multi-module support:** DLL support landed through
    [#264](https://github.com/rexglue/rexglue-sdk/issues/264) (closed 2026-05-16)
    and [#157](https://github.com/rexglue/rexglue-sdk/issues/157). Each DLL becomes
    a `[[modules]]` entry compiled to a host shared library.
  - **Open bugs on that path:**
    - [#448](https://github.com/rexglue/rexglue-sdk/issues/448): `XexLoadImage`
      secondary module, `XexGetProcedureAddress` fails, and the module image is
      not writable.
    - [#454](https://github.com/rexglue/rexglue-sdk/issues/454): cross-DLL
      ordinal imports are left undefined, and Linux `dlopen` fails.
    - [#499](https://github.com/rexglue/rexglue-sdk/issues/499): a module cannot
      be reloaded after `XexUnloadImage`.
    - [#443](https://github.com/rexglue/rexglue-sdk/issues/443): codegen crashes
      with an entrypoint plus modules (Call of Duty 3).
    - [#421](https://github.com/rexglue/rexglue-sdk/issues/421): modules that
      share an image base fail. **This does not apply to Top Spin 4**, whose
      three modules have distinct bases.
  - FIFA World Cup 2010's multi-XEX crash ([#354](https://github.com/rexglue/rexglue-sdk/issues/354))
    is closed.
- **[XenonRecomp](https://github.com/hedge-dev/XenonRecomp)** (6.6k stars, last
  push 2025-08-04). It is the original recompiler but single-executable, with no
  module manifest. Do not use it here.
- **[ps3recomp](https://github.com/sp00nznet/ps3recomp)**, PS3 fallback only.
  v0.17.0 (2026-10-21) lifts PPU and SPU code to C. Its README says a recompiled
  game runs "in this tree" only on Windows/D3D12; macOS (Metal) and Linux
  (Vulkan) do not boot titles yet. Ports such as
  [allstars-ps3](https://github.com/ColinGamez/allstars-ps3) show the workflow.
  Using it would mean starting over on runtime, renderer and launcher.

### Runtime, HLE and shaders

- **Runtime:** ReXGlue's kernel/XAM HLE, plus the user's 29 local patches in
  `D:\Projects\WWE_ALLSTARS-RECOMP\patches\` (frame pacing, guest vblank, online,
  TU update, resolve readback, XMA fixes and others). These carry over wholesale.
- **Shaders:** [XenosRecomp](https://github.com/hedge-dev/XenosRecomp) (pushed
  2026-03-12), with the user's `xenosrecomp-wweallstars.patch`, for the native
  renderer. ReXGlue's built-in Xenia translators serve the Classic path.
- **Reference emulator and oracle:** [Xenia Edge](https://github.com/has207/xenia-edge),
  as requested. Keep Canary only as a second opinion. Edge's recent Xenos fixes
  can also be compared against ReXGlue's older Xenia-derived GPU code
  **[inferred: ReXGlue says it is "heavily rooted on" Xenia, but which Xenia
  revision it tracks was not checked]**.

### Top Spin 4 archives and assets

Community research is thin.

- **ZenHAX "TOP SPIN 4 (PS3)" thread, 2022** ([link](https://www.zenhax.com/viewtopic.php@t=16776.html))
  **[community]**:
  - The game data is in `.arc` archives.
  - 360 archives carry a `PAM-LZX` signature and are probably Microsoft LZX.
  - PS3 archives look like zlib (`78 9C`).
  - Wii uses `.brres`.
  - No extractor or QuickBMS script was ever posted.
- **Names from the executable** ([title-server-patch.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/title-server-patch.md)):
  `Media\Patch\PatchesFAT.bin`, `Patch.arc`, `PatchDesc.bin`, `PatchData.arc`,
  `UPDATE:\CoreTech_Xenon.bin`, `UPDATE:\Layoutall.bin`. This points to a
  FAT-plus-archive layout and an `UPDATE:` override path that is useful for mods.
- **The `PAM-LZX` tag** suggests the format came from PAM Development's Top Spin 3
  codebase **[inferred]**.
- **Audio and video:** formats not identified. For XMA/FSB, vgmstream is the usual
  reference ([repo](https://github.com/vgmstream/vgmstream)).
- **Middleware:** Havok, per Xenia's `tech-middleware-havok` label
  ([#358](https://github.com/xenia-canary/game-compatibility/issues/358)). Other
  middleware (video, UI) is unverified.
- **Conclusion:** expect to write the `.arc` extractor and repacker yourself. Use
  `inflate`/LZX from ReXGlue or Xenia, and the game's own reader in Ghidra as the
  specification.

### Disassembly and decompilation

- **Ghidra** 12.1.x ([NSA/ghidra](https://github.com/NationalSecurityAgency/ghidra), pushed 2026-10-22).
- **XEX loaders:**
  - [XEXLoaderWV](https://github.com/zeroKilo/XEXLoaderWV): 12.1.3 released
    2026-09-20. Supports XEXP delta patches and PDB/XDB. This is what
    `topspin4-decomp` uses (Ghidra 12.1.2 with `PowerPC:BE:64:A2ALT-32addr`).
  - [Xenon360](https://github.com/EveryoneEverybody/Xenon360): v0.7.0, pushed
    2026-09-30. A loader plus the `Xenon-VMX128-32addr` language, built for Ghidra
    12.1.4 only. The user already uses it, and their notes say it does not decode
    `vpk*128` (`D:\Projects\WWE_ALLSTARS-RECOMP\docs\research\tooling-recommendations.md`).
- **Matching and splitting:** `topspin4-decomp` uses Jeff 0.3.5 (patched),
  objdiff-cli 3.7.1 and the Xbox 360 MSVC 16.00.10224 toolchain
  ([matching-build.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/matching-build.md)).
  [objdiff](https://github.com/encounter/objdiff) was pushed 2026-10-19.
- **Existing project:** [g-guthrie/topspin4-decomp](https://github.com/g-guthrie/topspin4-decomp)
  (MIT, created 2026-08-14, last push 2026-08-31, 3 stars).
  - It imported all three modules: 41,795 Ghidra functions in total, 32,506 of
    them in `Swing_DLL`.
  - It has 280 reviewed names and 245 functions that match exactly (0.16% of
    Swing code bytes).
  - It wrote a repair script for `_savegprlr_*` being marked non-returning,
    which fixed 11,645 call sites ([analysis-status.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/analysis-status.md)).
  - Its `symbols.csv` is directly reusable.
- **PS3 fallback:** [clienthax/Ps3GhidraScripts](https://github.com/clienthax/Ps3GhidraScripts)
  (pushed 2026-07-12).

### Debugging and profiling

These are the user's existing stack, unchanged from the WWE All Stars
recommendations (`tooling-recommendations.md`): WinDbg/TTD, RenderDoc, Vulkan
validation layers, AMD RGP/RGD, Tracy, and the shared Ghidra MCP server.

### Modding

- No source-level or asset modding tools exist for Top Spin 4.
- Old "mod tools" are save and XP editors, for example the
  [WeMod forum XP tool](https://community.wemod.com/t/top-spin-4-mod-tool/884)
  **[community]**.
- The Canary [game-patches](https://github.com/xenia-canary/game-patches) repository
  has no `54540859` file (directory listing checked 2026-10-24).

---

## 3. Feasibility

### Engine

- Wikipedia lists the engine as the **"PAM Engine"** and notes that 2K Czech took
  over after PAM Development closed in 2008 ([Wikipedia](https://en.wikipedia.org/wiki/Top_Spin_4)).
- 2K Czech is the former Illusion Softworks, and Mafia II's engine was
  "Illusion", largely rewritten for that game ([PCGH interview](https://www.pcgameshardware.de/Mafia-2-Spiel-13147/Specials/Mafia-2-2k-Czech-im-Technik-Interview-PC-Spiele-werden-nie-aussterben-765078/)).
- **No source shows Top Spin 4 sharing code with Illusion.** The binary points to
  PAM lineage instead: the main game DLL is `Swing_DLL.dll`, archives are tagged
  `PAM-LZX`, and a `CoreTech_Xenon.bin` is loaded from `UPDATE:`
  ([toolchain.json](https://github.com/g-guthrie/topspin4-decomp/blob/main/config/toolchain.json),
  [ZenHAX](https://www.zenhax.com/viewtopic.php@t=16776.html),
  [title-server-patch.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/title-server-patch.md)).
  Treat the PAM lineage as **[inferred]** until the Ghidra project confirms it.
  Either way, nothing reusable from Mafia II modding applies.
- The game is **script-driven**. `Swing_DLL` registers named script commands
  (`Script_TitleServer_GetPatch`, `Script_IsLobbyFull`, the `TSU_*` set),
  and the 360 build carries PS3-named commands (`Script_OnlinePS3_*`), so one
  codebase served both consoles ([symbols.csv](https://github.com/g-guthrie/topspin4-decomp/blob/main/config/symbols.csv)).
  This helps naming, and it gives mods a data-level hook point.

### Size

| Module | Ghidra functions | Jeff code bytes | Source |
|---|---:|---:|---|
| `default.xex` (`Boot.exe`) | 503 | n/a | [analysis-status.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/analysis-status.md) |
| `Loader_DLL.xex` | 8,786 | 4,685,376 (17,215 Jeff functions) | [loader-analysis.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/loader-analysis.md) |
| `Swing_DLL.xex` | 32,506 | 10,153,700 (47,250 objdiff functions) | [matching-build.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/matching-build.md) |

For comparison, the WWE All Stars TU2 Ghidra project holds 97,925 functions
(`tooling-recommendations.md`). Top Spin 4 is smaller.

### Blockers and risks

| Area | Finding | Risk |
|---|---|---|
| **Multi-module XEX** | `default.xex` is a 639 KB boot stub. The game lives in two DLLs loaded at runtime ([targets.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/targets.md)). ReXGlue supports `[[modules]]`, but the open bugs listed in §2 hit exactly this path. Whether `Swing_DLL` imports from `Loader_DLL`, which would trigger [#454](https://github.com/rexglue/rexglue-sdk/issues/454), has not been checked | **High.** This is new ground for the user. Budget a ReXGlue patch series for module load, procedure lookup, page protection and cross-module imports |
| **Kinect / Move** | The 360 version does not support Kinect; Move is PS3-only ([Wikipedia](https://en.wikipedia.org/wiki/Top_Spin_4)) | None on 360 |
| **Stereoscopic 3D** | The 360 version is "3D compatible" ([Wikipedia](https://en.wikipedia.org/wiki/Top_Spin_4)) | Low. It is an extra render path to keep off or to support later |
| **Online** | Player Match, World Tour and 2K Open ([Wikipedia](https://en.wikipedia.org/wiki/Top_Spin_4)). Outages are reported from 2013. By late 2016 to 2017 players report that only Player Match works and that 2K never announced a shutdown ([TrueAchievements thread](https://www.trueachievements.com/forum/viewthread.aspx?tid=412685)) **[community]**. No official 2K notice was found. Xenia logs unimplemented `XamParty*` and `XamShowCommunitySessionsUI` ([#867](https://github.com/xenia-project/game-compatibility/issues/867)) | Medium. Stub the title server as WWE All Stars ADR-0008 does, and optionally revive Player Match as in ADR-0021 |
| **Dead data-patch server** | `Swing_DLL` contains `CTitleServer_RequestPatch`, `CPatchManager` and `Media\Patch\*`. None of those payloads is on the disc, so retail disc data is the unpatched baseline ([title-server-patch.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/title-server-patch.md)). Players recall "about 4 patches that did some balancing" ([Operation Sports, 2019](https://forums.operationsports.com/forums/forum/other-sports/tennis/882481-is-there-a-way-i-can-get-the-top-spin-4-patches-dlcs-and-updates)) **[community]** | Medium for faithfulness, because the final online balance state is probably lost. Low for running the game |
| **Frame-rate coupling** | Above 60 fps changes game speed ([Canary #358](https://github.com/xenia-canary/game-compatibility/issues/358), label `vsync-off-speedup`) | Medium. High frame rate needs a decoupled simulation or interpolation; ship 60 fps first |
| **Resolution scaling** | Upscaling crashes Xenia Canary (host crash or device lost) ([#358](https://github.com/xenia-canary/game-compatibility/issues/358)) | Medium on the Classic renderer; it goes away with a native renderer that owns render targets **[inferred]** |
| **XPS** | Label `gpu-xps`: "Title uses Xbox Procedural Synthesis for GPU access to L2 memory" ([label](https://github.com/xenia-canary/game-compatibility/labels/gpu-xps)) | Medium for the native renderer, because some geometry is generated by CPU threads. Classic handles it as Xenia does **[inferred]** |
| **Video cross-fades** | Heavy flicker on cross-fading videos and intros ([#867](https://github.com/xenia-project/game-compatibility/issues/867), 2023) | Low to medium. Probably a render-target or resolve ordering issue **[inferred]** |
| **Face rendering** | On PS3 the opponent's face needs "read color buffers" to render correctly ([RPCS3 #8605](https://github.com/RPCS3/rpcs3/issues/8605)) | Medium. The game reads back rendered surfaces. On 360 this is a resolve-to-texture the native renderer must honour (compare the user's `rexglue-resolve-readback.patch`) **[inferred]** |
| **Jump tables** | MSVC byte-indexed switch tables are present. Jeff mistook an in-function jump-table label for a function in `Loader_DLL` (table at `.rdata 0x830847F8`) ([loader-analysis.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/loader-analysis.md)) | Low to medium. Expect manifest seeds and switch-table entries, as in the WWE ports |
| **Register save/restore helpers** | `_savegprlr_14..29` were mis-flagged as non-returning (11,645 call sites) ([analysis-status.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/analysis-status.md)) | Low. ReXGlue already recognises these helpers **[inferred]**. This only matters for Ghidra hygiene |
| **VMX128** | Ghidra reports "unresolved PowerPC/VMX constructors" ([analysis-status.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/analysis-status.md)). How much VMX128 is used was not measured | Low for codegen (ReXGlue translates VMX128). Medium for RE, given the same Xenon360 `vpk*128` gap |
| **Indirect calls and mid-asm hooks** | Script-command dispatch tables and C++ vtables mean heavy indirect calling **[inferred]**. ReXGlue supports `[[entrypoint.midasm_hook]]` (used throughout `wweallstars_manifest.toml`) and `include/rex/hook.h`. Whether module DLLs accept the same hook tables is unverified | Medium |
| **Multithreading** | Havok middleware ([#358](https://github.com/xenia-canary/game-compatibility/issues/358)) plus XPS producer threads | Medium. Expect spin-wait and fence work similar to the user's `spin_waits.h` and `spin-loops.md` |

### Verdict: feasible with caveats

**[inferred, from the points above]**

- The game runs to full matches in the Xenia family on default settings
  ([#358](https://github.com/xenia-canary/game-compatibility/issues/358)), so the
  HLE surface is known to be sufficient.
- It is smaller than WWE All Stars, and a public Ghidra pipeline and symbol set
  already exist.
- The caveats are ReXGlue's DLL path (the main schedule risk), frame-rate coupling
  for any high-frame-rate goal, and resolve, readback and XPS handling in the native
  renderer.

---

## 4. Recomp vs decomp vs hybrid

| | Recomp (ReXGlue) | Matching decomp | Hybrid (recomp plus targeted C++) |
|---|---|---|---|
| Effort to playable | Months, given the user's WWE experience **[inferred]** | Years. The public decomp matches 0.16% after about 2 weeks of activity and has no runnable link ([matching-build.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/matching-build.md)) | Recomp effort plus an incremental cost for each subsystem replaced |
| Accuracy | Instruction-faithful by construction | Faithful only where matched | Faithful except where deliberately replaced |
| Moddability | Data mods plus hooks | Best, with real source | Good. Hooked systems become C++ |
| Legal posture | Same as the WWE ports: ship no game data, users supply a disc and TU, and generated code is built from user files **[inferred, not legal advice]** | Matching decomps are usually published as source only. Note that `topspin4-decomp` calls itself "clean-room" while working from the binary, which is not clean-room in the strict sense **[inferred]** | Same as recomp |
| Reuse from WWE ports | High: skeleton (ADR-0004), ReXGlue pin (ADR-0005), 29 runtime patches, `src/native` Vulkan renderer, launcher (ADR-0018), crash reports, settings | Low | High |

**Recommendation: hybrid, recomp first.**

- Recompile all three modules with ReXGlue v0.10.0, pinned the same way as WWE
  All Stars.
- Replace with hand-written C++, through hooks:
  - the title-server, patch and online layer, stubbed or redirected;
  - the frame and tick clock;
  - the settings and enhancement code;
  - any routine the native renderer needs to intercept.
- Import `topspin4-decomp`'s `symbols.csv` into the Ghidra project for names.
  It is MIT-licensed, and its README excludes 2K's material from that licence.
  Do not depend on it for building.

---

## 5. Vanilla and enhanced modes, and post-1.0 modding

**Can one build offer both modes?** Yes, and the reference projects do.

- **UnleashedRecomp** separates options that restore original behaviour
  (Xbox 360 warm filter, music attenuation, original cutscene aspect ratio) from
  enhancements (60 fps and above, MSAA, ultrawide, D-pad). It states: "Game bugs
  present on the original hardware are intentionally preserved and will not be
  fixed" ([README](https://github.com/hedge-dev/UnleashedRecomp)).
- **UnleashedRecomp's mod support** works through Hedge Mod Manager with file
  replacement. Replacing `default.xex` or shaders is not supported, and "Code
  modding is currently not possible" beyond built-in codes (same README).
- **MarathonRecomp** (Sonic '06) folds PS3-only differences in as options
  ([README](https://github.com/sonicnext-dev/MarathonRecomp)).
- **The user's own pattern:**
  - Scope is an enhanced port: original behaviour plus modern settings, with mod
    hooks designed in but shipped after 1.0 (ADR-0001).
  - Classic and Native renderers are kept side by side, with Classic as the
    reference (ADR-0002).
  - Mods ride the game's title-update path through a launcher-built update folder
    (ADR-0026).

**Fit for Top Spin 4 [inferred]:**

- **Vanilla preset:**
  - 60 fps cap with the original tick;
  - native internal resolution and 16:9;
  - original HUD;
  - TU1 applied;
  - online features stubbed, reporting "server unavailable" the way the dead
    server does.
- **Enhanced preset:**
  - resolution scaling and MSAA in the native renderer;
  - an optional unlocked frame rate, only once the tick is decoupled;
  - XP and career-pace sliders;
  - instant-skip intro and logo videos;
  - optional Player Match revival.
- **Separating the two:** every enhancement is a setting with a vanilla default,
  and the vanilla preset is the definition of "faithful".
- **Mod loader, in this order:**
  1. **`UPDATE:` and patch-path redirection.** The game already looks for
     `UPDATE:\CoreTech_Xenon.bin`, `UPDATE:\Layoutall.bin` and a full
     `Media\Patch\*` FAT-plus-archive set
     ([title-server-patch.md](https://github.com/g-guthrie/topspin4-decomp/blob/main/docs/title-server-patch.md)).
     A launcher-built overlay that feeds the game its own patch format matches the
     ADR-0026 pattern. It needs the patch FAT format to be reverse-engineered.
  2. **A VFS overlay** under `game:` for loose-file replacement, used where the
     game opens files by path rather than through `.arc` entries.
  3. **Script-command hooks.** The command registration table
     (`RegisterOnlineScriptCommands` and siblings) is a natural place to let mods
     add or override commands.
  4. **Code mods** through ReXGlue `midasm_hook` plus named C++ hook headers
     generated from Ghidra (ADR-0013 in WWE All Stars), shipped after 1.0.

---

## 6. QOL fixes

Sources are labelled. Reviews and forums are used only for player-reported
problems.

| Complaint | Evidence | Fix in a recomp **[inferred]** |
|---|---|---|
| Frame rate above 60 breaks game speed | Emulator report ([#358](https://github.com/xenia-canary/game-compatibility/issues/358)) | Ship a locked 60. High frame rate needs a decoupled simulation and is a stretch goal |
| Upscaling crashes | Emulator report ([#358](https://github.com/xenia-canary/game-compatibility/issues/358)) | The native renderer owns render-target scaling |
| Native resolution and aspect ratio | **Not found.** No pixel count for 360 or PS3 was located. The PS3 patch fixed text on 4:3 TVs ([patch notes](https://forums.operationsports.com/forums/forum/other-sports/tennis/421154-top-spin-4-patch-available-now-post-your-impressions-here-ps3-360)) | Measure it in the Edge baseline. Ultrawide would need HUD and camera work |
| Camera settings not saved | Fixed in TU1 ([patch notes](https://forums.operationsports.com/forums/forum/other-sports/tennis/421154-top-spin-4-patch-available-now-post-your-impressions-here-ps3-360)) | Ship with TU1. Extra camera presets are possible later |
| No D-pad movement | Enabled in TU1 (same source) | TU1 |
| Controls are hard to learn (shoulder modifiers) | Game Informer review ([link](https://gameinformer.com/games/top_spin_4/b/xbox360/archive/2011/03/15/top-spin-4-review)) **[review]** | Button remapping |
| No commentary | Game Informer: "Still lacking commentary" **[review]** | Out of scope (content) |
| Wimbledon missing; thin roster (25 players, 7 women) | Game Informer and Complex ([link](https://www.complex.com/pop-culture/a/peter-rubin/top-spin-4-review)) **[review]** | Only content mods could address it, once archive tools exist |
| Early career too easy, then a grind | Game Informer (too easy early); CBS and TrueAchievements describe a grind ([CBS](https://www.cbsnews.com/news/2k-sports-top-spin-4/), [TA reviews](https://www.trueachievements.com/game/Top-Spin-4/reviews)) **[review, community]**. The Wii minigame-grind complaint is Wii-only ([GameSpot Wii](https://www.gamespot.com/reviews/top-spin-4-review/1900-6305302/)) | XP multiplier option, in the enhanced preset only |
| Online dead apart from Player Match; 1000G unobtainable | [TrueAchievements thread](https://www.trueachievements.com/forum/viewthread.aspx?tid=412685) **[community]** | Stub the server so menus fail cleanly. Revive Player Match (LAN, system link or relay). World Tour and 2K Open would need a reimplemented server, which is out of scope for 1.0 |
| "Checking for DLC" then "servers down" messages at boot | [Operation Sports, 2019](https://forums.operationsports.com/forums/forum/other-sports/tennis/882481-is-there-a-way-i-can-get-the-top-spin-4-patches-dlcs-and-updates) **[community]** | Short-circuit the title-server login (`Script_TitleServer_Login`) |
| Online pass or DLC lockouts | **No evidence** that Top Spin 4 had an online pass. Searches found only TopSpin 2K25 store terms | Probably not applicable. Unlocking the Agassi preorder outfit could be an option |
| Online lag | Observer, via Wikipedia ([link](https://en.wikipedia.org/wiki/Top_Spin_4)) **[review]**. Lag-handling fixes are in TU1 | Only relevant if Player Match is revived |
| Video cross-fade flicker | Emulator report ([#867](https://github.com/xenia-project/game-compatibility/issues/867)) | Renderer correctness item |
| Load times | **Not found** | Measure in the baseline. The `faster_starts` and prebuilt pipelines from WWE All Stars carry over |
| Between-point drop to 30 fps | Wii/Dolphin only ([Dolphin wiki](https://wiki.dolphin-emu.org/index.php?title=Top_Spin_4)) | Check whether 360 does the same |

---

## 7. Native renderer: D3D12, Vulkan or both

**What the reference projects do:**

- **UnleashedRecomp and MarathonRecomp** use [plume](https://github.com/renderbag/plume),
  an RHI with `plume_d3d12`, `plume_vulkan` and `plume_metal` backends (pushed
  2026-10-21). Its README says it is "not considered to be production-ready".
  UnleashedRecomp needs D3D12 SM6 or Vulkan 1.2 and has native Linux and Steam
  Deck builds ([README](https://github.com/hedge-dev/UnleashedRecomp)).
  MarathonRecomp also ships macOS ([README](https://github.com/sonicnext-dev/MarathonRecomp)).
- **ReXGlue** ships Xenia-derived D3D12 and Vulkan backends
  ([src/graphics](https://github.com/rexglue/rexglue-sdk/tree/main/src/graphics)),
  with no Metal backend and no macOS CI ([README](https://github.com/rexglue/rexglue-sdk)).
- **Xenia Edge** has D3D12, Vulkan and Metal backends ([tree](https://github.com/has207/xenia-edge/tree/edge/src/xenia/gpu)).
- **ps3recomp** uses D3D12 on Windows, Metal on macOS and opt-in Vulkan on Linux
  ([README](https://github.com/sp00nznet/ps3recomp)).
- **The user's projects:**
  - SVR2011 hit ReXGlue D3D12 `DEVICE_HUNG` on an RX 7900 XT, so WWE All Stars
    went Vulkan-only for both the Classic and Native renderers (ADR-0003).
  - The native renderer is a hybrid of D3D9 hooks and PM4 decoding into Vulkan
    (ADR-0015).
  - WWE All Stars runs at 60 fps on Steam Deck through Proton (its README).

**Tradeoffs for a solo developer [inferred]:**

- **Two backends** double the testing matrix and the shader-path work for little
  gain, because Windows Vulkan drivers are fine and Proton runs Vulkan natively.
- **macOS through MoltenVK** is possible only if ReXGlue itself builds on macOS.
  It has no macOS CI today, so macOS is not a near-term target on either API.
- **Shader maturity:** XenosRecomp emits HLSL, which compiles through DXC to both
  DXIL and SPIR-V, so Vulkan loses nothing.
- **Tooling:** RenderDoc, the Vulkan validation layers and AMD RGP cover Vulkan
  well on the user's AMD hardware.

**Recommendation: Vulkan only.** Reuse `src/native` from WWE All Stars. Revisit
plume's Metal backend only if macOS becomes a goal, which would also require
ReXGlue macOS support.

---

## Tool inventory

| Tool | Role | Repo | Last push / release | Notes |
|---|---|---|---|---|
| ReXGlue SDK | PPC to C++ recompiler and Xenia-derived runtime | [rexglue/rexglue-sdk](https://github.com/rexglue/rexglue-sdk) | push 2026-10-02; v0.10.0 2026-08-21 | DLL modules supported, with open bugs #448, #454, #499, #443 |
| XenonRecomp | Original 360 recompiler | [hedge-dev/XenonRecomp](https://github.com/hedge-dev/XenonRecomp) | 2025-08-04 | Single XEX; not recommended here |
| XenosRecomp | Xenos shaders to HLSL | [hedge-dev/XenosRecomp](https://github.com/hedge-dev/XenosRecomp) | 2026-03-12 | The user carries a local patch |
| plume | D3D12/Vulkan/Metal RHI | [renderbag/plume](https://github.com/renderbag/plume) | 2026-10-21 | Reference only |
| UnleashedRecomp | Reference port (options, mods) | [hedge-dev/UnleashedRecomp](https://github.com/hedge-dev/UnleashedRecomp) | push 2026-06-29; v1.0.3 2025-04-03 | |
| MarathonRecomp | Reference port (macOS, PS3 options) | [sonicnext-dev/MarathonRecomp](https://github.com/sonicnext-dev/MarathonRecomp) | 2026-10-24 | |
| Xenia Edge | **Primary** 360 reference emulator and oracle | [has207/xenia-edge](https://github.com/has207/xenia-edge) | 2026-10-24 (several releases a day) | Canary fork, +2,085 commits; no Top Spin 4 issue |
| Xenia Canary | Fallback compatibility data | [xenia-canary/xenia-canary](https://github.com/xenia-canary/xenia-canary) | 2026-10-24 | [#358](https://github.com/xenia-canary/game-compatibility/issues/358) |
| RPCS3 | PS3 reference, PKG install, decryption | [RPCS3/rpcs3](https://github.com/RPCS3/rpcs3) | 2026-10-24 | Top Spin 4 Playable |
| Dolphin | Wii reference | [dolphin-emu/dolphin](https://github.com/dolphin-emu/dolphin) | 2026-10-24 | Not needed |
| ps3recomp | PS3 recompiler (fallback) | [sp00nznet/ps3recomp](https://github.com/sp00nznet/ps3recomp) | v0.17.0 2026-10-21 | Boots games only on Windows/D3D12 |
| extract-xiso | 360 ISO extraction | [XboxDev/extract-xiso](https://github.com/XboxDev/extract-xiso) | 2026-09-11 | Or the user's `inspect_disc.py` |
| XexTool (rebuilds) | XEX inspection | [Team-Resurgent/XexTool](https://github.com/Team-Resurgent/XexTool), [XexTool-RE](https://github.com/mrtitanic777/XexTool-RE) | 2026-10-02 / 2026-07-15 | |
| xbox-reversing | STFS/GDFX format notes | [emoose/xbox-reversing](https://github.com/emoose/xbox-reversing) | 2024-10-22 | For TU packages |
| Velocity | STFS browser | [Gualdimar/Velocity](https://github.com/Gualdimar/Velocity) | 2016, archived | Legacy |
| Ghidra | Disassembly and decompilation | [NationalSecurityAgency/ghidra](https://github.com/NationalSecurityAgency/ghidra) | 2026-10-22 | |
| XEXLoaderWV | Ghidra XEX/XEXP loader | [zeroKilo/XEXLoaderWV](https://github.com/zeroKilo/XEXLoaderWV) | 12.1.3, 2026-09-20 | Used by `topspin4-decomp` |
| Xenon360 | Ghidra loader and VMX128 language | [EveryoneEverybody/Xenon360](https://github.com/EveryoneEverybody/Xenon360) | 2026-09-30 | Ghidra 12.1.4; no `vpk*128` |
| GhidraXenon | VMX128 language, switch-table script | [freeqaz/ghidra-xenon-extension](https://github.com/freeqaz/ghidra-xenon-extension) | 2026-02-11 | |
| Ps3GhidraScripts | PS3 binary parsing | [clienthax/Ps3GhidraScripts](https://github.com/clienthax/Ps3GhidraScripts) | 2026-07-12 | Fallback |
| topspin4-decomp | Symbols, module map, analysis notes | [g-guthrie/topspin4-decomp](https://github.com/g-guthrie/topspin4-decomp) | 2026-08-31 | MIT; 41,795 functions; 245 matched |
| objdiff | Object diffing (decomp) | [encounter/objdiff](https://github.com/encounter/objdiff) | 2026-10-19 | Only if matching work is done |
| vgmstream | Audio format reference | [vgmstream/vgmstream](https://github.com/vgmstream/vgmstream) | 2026-10-24 | Top Spin 4 audio format unverified |
| JPEXS FFDec | Flash/GFx UI inspection | [jindrapetrik/jpexs-decompiler](https://github.com/jindrapetrik/jpexs-decompiler) | 2026-10-24 | Only if Top Spin 4 uses Scaleform (unverified) |
| RenderDoc | GPU capture | [baldurk/renderdoc](https://github.com/baldurk/renderdoc) | 2026-10-24 | |
| HedgeModManager | Mod manager reference design | [thesupersonic16/HedgeModManager](https://github.com/thesupersonic16/HedgeModManager) | 2026-07-20 | Reference only |
| `.arc` / `PAM-LZX` tool | Archive extract and repack | none exists | n/a | Must be written ([ZenHAX](https://www.zenhax.com/viewtopic.php@t=16776.html)) |

## Open questions and unverified points

1. **Module dependencies:** does `Swing_DLL` import ordinals from `Loader_DLL`
   (which would trigger ReXGlue #454)? Does the game unload and reload either
   module (#499)? Check the import tables locally.
2. **TU1 scope:** which modules does TU1 patch (`default.xexp` only, or the DLL
   `.xexp` files too)? Does it ship `UPDATE:\CoreTech_Xenon.bin` or
   `Layoutall.bin`?
3. **Where D3D lives:** which module statically links the XDK D3D9 library? The
   native-renderer hooks depend on it (ADR-0002/0015 pattern). The XDK version is
   unknown beyond compiler 16.0.10224.
4. **Native resolution and aspect ratio** of the 360 build: no pixel count was
   found.
5. **Load times:** no data.
6. **An Edge baseline** for this exact media ID with TU1 does not exist yet. No
   public report names its media ID or TU.
7. **Engine lineage:** PAM versus Illusion is inferred from module names and
   archive tags only.
8. **Middleware** beyond Havok (video codec, UI framework, audio) is unidentified.
9. **The `.arc` and `Media\Patch` formats** have no public specification.
10. **Server data patches:** do the balance payloads survive anywhere, for example
    in console caches? Without them, "final online balance" cannot be restored.
11. **Server shutdown:** no official 2K notice exists. The date comes from player
    reports (around 2016, Player Match still working afterwards). The
    TrueAchievements, RPCS3 forum and Operation Sports pages needed a real browser,
    and the RPCS3 forum thread was behind a Cloudflare check and was not read.
12. **Online pass:** no evidence either way.
13. **PS3 SPU usage** (relevant only for the fallback) was not checked.
14. **Mid-asm hooks in modules:** whether ReXGlue accepts `midasm_hook` tables for
    `[[modules]]` as it does for `[entrypoint]` is unverified.

## Sources

Local:

- `D:\Projects\WWE_ALLSTARS-RECOMP\README.md`, `CLAUDE.md`, `docs\adr\0001`, `0002`, `0003`, `0005`, `0013`, `0015`, `0026`, `docs\research\tooling-recommendations.md`, `wweallstars_manifest.toml`, `patches\`
- `D:\Projects\SVR2011-RECOMP\README.md`, `docs\feasibility.md`, `svr2011_manifest.toml`
- `D:\Projects\SmackDown2Recomp\README.md`

Primary (repositories, databases, trackers, official servers):

- https://github.com/g-guthrie/topspin4-decomp (README, docs/targets.md, analysis-status.md, loader-analysis.md, title-server-patch.md, matching-build.md, additional-content.md, config/toolchain.json, config/symbols.csv)
- https://github.com/has207/xenia-edge, https://api.github.com/repos/has207/xenia-edge, https://api.github.com/repos/has207/xenia-edge/releases, https://github.com/has207/xenia-edge/blob/edge/assets/game-compatibility/master.json
- https://github.com/xenia-canary/game-compatibility/issues/358, https://github.com/xenia-canary/game-compatibility/labels/gpu-xps
- https://github.com/xenia-project/game-compatibility/issues/867
- https://github.com/xenia-canary/game-patches
- https://rpcs3.net/compatibility?api=v1&g=top+spin
- https://github.com/RPCS3/rpcs3/issues/8605, https://github.com/RPCS3/rpcs3/issues/11680, https://github.com/RPCS3/rpcs3/issues/14933
- https://wiki.dolphin-emu.org/index.php?title=Top_Spin_4
- http://xboxunity.net/Resources/Lib/TitleUpdateInfo.php?titleid=54540859
- https://a0.ww.np.dl.playstation.net/tpl/np/BLUS30427/BLUS30427-ver.xml, https://a0.ww.np.dl.playstation.net/tpl/np/BLES00668/BLES00668-ver.xml
- https://github.com/rexglue/rexglue-sdk (README, src/graphics, issues #157, #264, #354, #421, #443, #448, #454, #499)
- https://github.com/hedge-dev/XenonRecomp, https://github.com/hedge-dev/XenosRecomp, https://github.com/hedge-dev/UnleashedRecomp
- https://github.com/sonicnext-dev/MarathonRecomp
- https://github.com/renderbag/plume
- https://github.com/sp00nznet/ps3recomp, https://github.com/ColinGamez/allstars-ps3
- https://github.com/zeroKilo/XEXLoaderWV, https://github.com/EveryoneEverybody/Xenon360, https://github.com/freeqaz/ghidra-xenon-extension, https://github.com/clienthax/Ps3GhidraScripts
- https://github.com/XboxDev/extract-xiso, https://github.com/Team-Resurgent/XexTool, https://github.com/mrtitanic777/XexTool-RE, https://github.com/emoose/xbox-reversing, https://github.com/Gualdimar/Velocity
- https://github.com/encounter/objdiff, https://github.com/vgmstream/vgmstream, https://github.com/jindrapetrik/jpexs-decompiler, https://github.com/baldurk/renderdoc, https://github.com/thesupersonic16/HedgeModManager

Secondary (encyclopaedia, press, reviews):

- https://en.wikipedia.org/wiki/Top_Spin_4
- https://www.pcgameshardware.de/Mafia-2-Spiel-13147/Specials/Mafia-2-2k-Czech-im-Technik-Interview-PC-Spiele-werden-nie-aussterben-765078/
- https://thehdroom.com/news/top-spin-4-and-mlb-2k11-demos-hits-xbox-360-ps3-37400/, https://gameranx.com/updates/id/1464/article/top-spin-4-demo-now-available/
- https://gamingnexus.com/News/21188/Tennis-Stars-Align-for-Top-Spin-4
- https://www.gamespot.com/reviews/top-spin-4-review/1900-6305302/ (Wii review)
- https://gameinformer.com/games/top_spin_4/b/xbox360/archive/2011/03/15/top-spin-4-review
- https://www.complex.com/pop-culture/a/peter-rubin/top-spin-4-review
- https://www.cbsnews.com/news/2k-sports-top-spin-4/

Community (player-reported only):

- https://forums.operationsports.com/forums/forum/other-sports/tennis/421154-top-spin-4-patch-available-now-post-your-impressions-here-ps3-360 (repost of the official patch notes, 2011-05-09)
- https://forums.operationsports.com/forums/forum/other-sports/tennis/882481-is-there-a-way-i-can-get-the-top-spin-4-patches-dlcs-and-updates
- https://www.trueachievements.com/forum/viewthread.aspx?tid=412685
- https://www.trueachievements.com/game/Top-Spin-4/reviews
- https://www.zenhax.com/viewtopic.php@t=16776.html
- https://community.wemod.com/t/top-spin-4-mod-tool/884
