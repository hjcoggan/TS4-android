# TU1 symbol map

Ticket 04, 2026-10-24. topspin4-decomp (`553f783`) names Swing's functions in the base
(disc, v0.0.0.3) image. TU1 relinks Swing (ADR-0002), so the names were carried to the TU1
image by function similarity, not by address. The result is `symbols/tu1-symbols.csv`
(names and addresses only), in the decomp's own `module,address,name,confidence,evidence`
columns, so its Ghidra script `ApplySymbolMap.java` reads it unchanged.

Regenerate with `python scripts/tu_symbol_map.py --report analysis/tu1-symbol-report.md`. It
needs the base and TU1 memory images in `analysis/xex/` (XexTool, module-inspection.md).

## Method

`scripts/tu_addrmap.py`, copied from WWE All Stars (ADR-0004), pairs each image's `.pdata`
functions by their code with branch and address immediates masked. Unique matches anchor
the pairing, and `difflib` fills the gaps between anchors. Two changes made it work on a
relink:

- Unique matches that fall outside the in-order chain are kept. A relink moves whole
  objects, so many correct matches are out of order.
- Gaps larger than 4×10⁸ (base × TU functions) are not diffed. One such gap made the
  original run for more than 10 minutes. The unique matches cover it. The whole run now
  takes about 5 seconds per Module.

A name maps through its function's match, or failing that through a local or table
match (WWE's methods). Script-command handlers are often one-line stubs (`li r3,N; blr`)
with copies everywhere. The registration functions (`RegisterOnlineScriptCommands` and
the like) pair each handler with its command-name string, and those strings survive the
relink. So a handler is placed in TU1 by finding the same string in the matched
registration function.

## Results

| | Matched | Notes |
|---|---|---|
| Boot `.pdata` functions | 334 of 334 (100%) | |
| Loader `.pdata` functions | 8,918 of 8,918 (100%) | No decomp names exist for Loader. |
| Swing `.pdata` functions | 33,712 of 33,887 (99.5%) | 185 paired by position with changed code. |
| Swing names | 553 of 586 (94.4%) | 198 by function, 315 by local code, 14 by table, 11 by registration, 14 `function*` (same shape, a constant or struct offset differs: check before hooking), 1 data-ref. |
| Script commands | 55 mapped | 49 paired with their command string by a registration function in both images. |

`high` confidence in the CSV means a whole-function or registration match on a name the
decomp reviewed; everything else is `medium`.

### Online and title-server commands (for tickets 08 and 15)

| Name | TU1 |
|---|---|
| `RegisterOnlineScriptCommands` | `887B20B8` (`function*`) |
| `Script_TitleServer_Login` | `88795B60` |
| `Script_TitleServer_LoginUserOnly` | `88793178` |
| `Script_TitleServer_Logout` | `88793360` |
| `Script_TitleServer_GetPatch` | `88795B28` |
| `Script_TitleServer_DownloadPlayerInfo` | `88793218` |
| `Script_TitleServer_QueryRoundInfo` | `88795A18` |
| `Script_TitleServerFeatureUnavailable` | `881D35B8` (a stub the TU1 link shares) |
| `Script_OnlineTrueStub`, `Script_OnlineFalseStubA`, `Script_OnlineFalseStubB` | `88778FE0`, `88778FF0`, `88778FF8` |
| `CTitleServer_RequestPatch`, `CTitleServer_QueueRequest` | `889B4E40`, `889B6FE0` |

### Not mapped (33)

- 26 `ts4_runtime_type_code_*` getters: 8-byte stubs whose code repeats throughout Swing,
  so no match is unique.
- `memcpy`, `_RtlCheckStack12`: CRT routines with no unique match.
- `ts4_get_loaded_command_line`, `ts4_call_runtime_a1e0`, `ts4_forward_runtime_b3360`,
  `CPatchManager_FinalizeLoad`: changed in TU1 beyond the masked-code match.
- `0000` (base `88B00780`): a decomp symbol with no real name.

Six script commands are mapped by code but not paired by string, because their
registration function has no decomp name: `Script_Minigame_ChangeDisplayGameMode`,
`Script_SetPlayerBodyVisibility`, `Script_SetPlayerVisibility`,
`Script_Minigame_PrepareInGame`, `Script_Minigame_HideUnusedTracksides`,
`Script_KingOfTheCourt_PrepareIngame`.

## Ghidra project

`scripts/ghidra-project.ps1` imports the three TU1-patched XEX images through the
Xenon360 loader into `ghidra/TopSpin4TU1` (git-ignored) and analyzes them. It then runs
`tools/ghidra/FixXbox360SaveHelpers.java` on each Module with that Module's
`__savegprlr_14` address, and `tools/ghidra/ApplySymbolMap.java` with the CSV on Swing.
Both scripts are adapted from topspin4-decomp (MIT). The save helpers sit at the same
addresses in TU1 as the code-gen found: Boot `82010C60`, Loader `833E3130`, Swing `8856AC40`.
