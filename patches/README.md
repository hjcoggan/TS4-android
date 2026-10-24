# Patches to upstream tools

Each folder is one numbered series, applied in file-name order by `scripts/apply-patches.ps1`
(run by `scripts/bootstrap.ps1`, and checked again by `scripts/build.ps1`). A stamp in the tool
tree records the series it carries. A patch that fails resets the tree and stops with its name.

| Folder | Applied to | Upstream revision |
|---|---|---|
| `rexglue/` | `.tools/rexglue-source` | `f5337cd` (v0.10.0) |

To add one, edit `.tools/rexglue-source` and run `pwsh scripts/new-patch.ps1 <name>`: it writes
the next-numbered patch from your edits and reapplies the series to prove it. A patch is never
renumbered once committed, so a fix to an earlier patch is a new patch on top.
`pwsh scripts/apply-patches.ps1 -Reset` discards the tree's edits and reapplies the series.

## Our own

| Patch | What |
|---|---|
| `0027-refuse-unrecompiled-modules` | In a multi-module project, `LoadUserModule` refuses a module with no recompiled library (ticket 07), so `XexLoadImage` fails cleanly instead of the game faulting on unrecompiled code. |
| `0028-modules-from-own-folder` | 0027 matches by file name, so a module of the same name from another device (a DLC package's) would be wired to this build's code; modules now load only from the executable's own folder, compared as resolved by the VFS. |
| `0029-guest-swap-count` | Counts `VdSwap` calls (`rex::system::GuestSwapCount`): the guest's frame count across both Modules' Direct3D copies, which the scripted pad times its steps by. |
| `0030-io-trace` | `--io_trace` logs every successful file open and every guest thread start and exit, so a run lines up against the Oracle's log (`.tools/xenia-edge/xenia.log`). Off by default. |

## Carried over from WWE All Stars (ADR-0004)

`rexglue/0001` to `0026` are WWE All Stars' runtime patches at `36f5bfd`, in WWE's order, with
these left out. All of them apply cleanly to `f5337cd` without the three below.

| Left out | Why |
|---|---|
| `rexglue-online.patch` | WWE's revived online play (its game server, relay and UPnP). Top Spin 4's online is stubbed in 1.0, so nothing would use it, and it pulls in libcurl, rapidjson and miniupnpc. |
| `rexglue-net-waits.patch` | Wakes guest socket waits for WWE's online sessions. Belongs with the online patch; offline modes make no socket waits that need it. |
| `rexglue-vibration.patch` | WWE's host-side rumble mixer (WWE ADR-0017). Its rules are tuned to WWE's game state. Top Spin 4's rumble goes through the SDK's own `XamInputSetState` path; revisit if rumble turns out wrong. |
| `xenosrecomp-wweallstars.patch` | The shader converter changes for the Native renderer. Deferred to ticket 26, where they are adapted to Top Spin 4's shaders. |

Kept despite being WWE-flavoured: `0020-tu2-update` loads a Title Update's `.xexp` from `update:\`
for every Module (TU1 here), `0021-content` keeps the save root name alive across an overlapped
`XamContentCreate`, and `0026-headless-keyboard` answers Player Creator name prompts in scene runs.
