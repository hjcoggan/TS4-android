# Patches to upstream tools

Each folder is one numbered series, applied in file-name order by `scripts/apply-patches.ps1`
(run by `scripts/bootstrap.ps1`, and checked again by `scripts/build.ps1`). A stamp in the tool
tree records the series it carries. A patch that fails resets the tree and stops with its name.

| Folder | Applied to | Upstream revision |
|---|---|---|
| `rexglue/` | `.tools/rexglue-source` | `f5337cd` (v0.10.0) |
| `xenosrecomp/` | `.tools/xenosrecomp` | `339af41` (re:Blue fork) |

To add one, edit `.tools/rexglue-source` and run `pwsh scripts/new-patch.ps1 <name>` (for
XenosRecomp, edit `.tools/xenosrecomp` and add `-Tool xenosrecomp`): it writes
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
| `0031-frame-shown-observer` | Calls a host observer on the GPU thread after each swap's image is the presenter's newest guest output, numbered on the `GuestSwapCount` clock, so scene runs dump exactly the frame the scripted pad's `at <frame>` names (`src/frame_dumps.h`). |
| `0032-spirv-fetch-exp-adjust-word` | The SPIR-V translator read a texture fetch's exponent adjust from fetch-constant dword 4 (where bits 12:21 are the LOD bias) instead of dword 3, so every fetch was scaled by 2^(LOD-bias bits): the practice-ground player rendered black and the court lines vanished. The DXBC translator already read dword 3. |
| `0033-gpu-debug-ps-probe` | `--gpu_debug_ps_hash/_register/_components/_after`: one pixel shader's colour 0 shows a register's components, optionally right after a chosen ucode instruction, for shaders RenderDoc cannot step (FSI). Adds a translator hook after each ALU or fetch instruction. Off unless the hash is set. |
| `0034-mask-fetch-addresses` | Masks vertex-fetch, index-base and memexport addresses to physical (`& 0x1FFFFFFF`) on the CPU side (the Vulkan and the unused D3D12 command processors) and in the SPIR-V shaders, as Xenia Edge `2fce2d8`. A fetch through a mirror window (Top Spin 4's `0xC0100100`, probably XPS) was rejected by shared memory and the draw dropped. Bumps the SPIR-V translator version. The DXBC translator is not masked (Vulkan only, ADR-0005). |
| `0035-message-box-log` | Logs each `XamShowMessageBoxUI`'s title, text, buttons and focused button at info, so a headless run's auto-answered dialogs show in the log. |
| `0036-message-box-rewriter` | `rex/kernel/xam/message_box.h`: `SetMessageBoxRewriter` lets the host change a title's message box before it shows (or before headless answers it) and fix the button the title gets. Logs each rewrite. Top Spin 4 uses it for its Xbox LIVE prompt (`src/online_unavailable.h`). |
| `0037-signin-ui-closes-unchanged` | `signin_ui_closes_unchanged` (off by default; Top Spin 4 turns it on): `XamShowSigninUI` acts as a picker the player closed. The call returns; the kernel's dispatch thread (new `KernelState::Dispatch`) waits up to 2 s for the title's next notification listener (new `notify_listeners_created`), then broadcasts `XN_SYS_UI` 1 and, 100 ms later, 0, with no `XN_SYS_SIGNINCHANGED`. The stub broadcast a sign-in change and UI 0 inside the call, before the title's listener existed, so Top Spin 4's Swing `0x888C1570` (waits for UI 1, then 0) and `0x8877B998` (waits for UI 1) never returned. The dispatch thread is joined at shutdown and runs one sign-in at a time. |
| `0038-window-hidden` | `window_hidden` (off by default): the window is never shown or raised, while the presenter and its guest output (frame dumps, patch 0031) work as usual. Smoke, scene and RenderDoc runs pass it, so a run never pops up on the owner's desktop or takes focus. |

## Carried over from WWE All Stars (ADR-0004)

`rexglue/0001` to `0026` are WWE All Stars' runtime patches at `36f5bfd`, in WWE's order, with
these left out. All of them apply cleanly to `f5337cd` without the three below.

| Left out | Why |
|---|---|
| `rexglue-online.patch` | WWE's revived online play (its game server, relay and UPnP). Top Spin 4's online is stubbed in 1.0, so nothing would use it, and it pulls in libcurl, rapidjson and miniupnpc. |
| `rexglue-net-waits.patch` | Wakes guest socket waits for WWE's online sessions. Belongs with the online patch; offline modes make no socket waits that need it. |
| `rexglue-vibration.patch` | WWE's host-side rumble mixer (WWE ADR-0017). Its rules are tuned to WWE's game state. Top Spin 4's rumble goes through the SDK's own `XamInputSetState` path; revisit if rumble turns out wrong. |

Kept despite being WWE-flavoured: `0020-tu2-update` loads a Title Update's `.xexp` from `update:\`
for every Module (TU1 here), `0021-content` keeps the save root name alive across an overlapped
`XamContentCreate`, and `0026-headless-keyboard` answers Player Creator name prompts in scene runs.

## XenosRecomp (ticket 26)

The shader pack's converter id is the hash of this series (`CMakeLists.txt`,
`scripts/convert-shaders.ps1`), so a change here makes existing packs stale.

| Patch | What |
|---|---|
| `0001-wweallstars-native` | WWE All Stars' `xenosrecomp-wweallstars.patch` at `36f5bfd`, unchanged: SvR 2011's mode (`SVR2011_RECOMP`: 32 sampler slots, COLOR0-7 interpolators, the Xenos boolean file) plus WWE's fixes for the Native renderer. |
| `0002-topspin4-inputs` | Vertex inputs POSITION2-4 and NORMAL1-4 (Top Spin 4's morph-target streams) get locations 16-22, which the Native renderer's `kInputLocations` matches; without them DXC rejected 28 vertex shaders for partial explicit locations. A 1D texture fetch becomes a 2D fetch at v = 0.5 of a width x 1 texture; `tfetch1D` and its descriptor index did not exist, so 36 pixel shaders failed. |
| `0003-dec3n-normals` | `SPEC_CONSTANT_DEC3N_NORMAL` (bit 4): normals, tangents and binormals in Xenos `k_2_10_10_10` signed normalized (D3DDECLTYPE `0x2A2187`, Top Spin 4's) decode as three signed 10-bit fields. They reached the shader as raw bits read as a float, a near-zero normal, so the match lost all sunlight. The Native renderer sets the bit for that type. |
| `0004-blend-weight-halves` | 16-bit blend weights (USHORT4N skin weights) get their half pairs swapped back after the host's 32-bit vertex swap (`tfetchBlendWeight`, mask bit 8 + index), as texcoords already were. With the halves swapped, the wrong bone dominated and the ball kids, umpire and CPU player were drawn twisted. |
| `0005-alpha-test-function` | The alpha test applies RB_COLORCONTROL's compare function (`g_AlphaFunc`, shared constants +568: bit 1 less, 2 equal, 4 greater) instead of always greater-or-equal (`clip(oC0.w - g_AlphaThreshold)`). Top Spin 4 draws hair in two passes, the second keeping the fragments below the threshold; with greater-or-equal, Federer's headband showed through his hair. |
