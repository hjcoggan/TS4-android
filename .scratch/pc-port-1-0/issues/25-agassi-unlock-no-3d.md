# 25: Agassi outfit unlock and 3D forced off

**What to build:** An option unlocks the Agassi preorder outfit, and stereoscopic 3D can never turn on.

**Blocked by:** 10 (Settings system and renderer switch), 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [ ] The unlock setting makes the outfit selectable, and the default leaves it as on the original disc
- [ ] The 3D switch is forced off whatever the in-game options or display say

## Comments
- 2026-10-25 (Claude), ticket audit: no box is met. The starting state: the main menu offers a STEREOSCOPIC 3D entry in both the port and Edge on TU1 (`analysis/edge-menu/compare-menu.png`), so the switch is reachable today.
- 2026-10-25 (Claude), reverse engineering only, no game run (owner's request). Ghidra, Swing_DLL (TU1):
  - **3D state.** Swing registers UI callbacks by name in `0x887C1768`, each as a name, a handler and the shared adaptor `0x881D35B8`.
    - `Is3DOn` (string `0x880ED0C0`) is handled by `0x8877A328`: `lwz r11,[0x88D45A48]; lbz r3,0x71(r11)`.
    - `Is3DAccessible` (`0x880ED0B0`) is handled by `0x8877A338`, which reads byte `0x70` of the same object.
    - The object behind `0x88D45A48` is written at `0x88211688`, `0x88211728` and `0x887C8A50`. Thirteen functions read its `+0x71` (or `+0x70`) directly, not through the accessors: `887330B8`, `88737D50`, `886F6040`, `887357A8`, `88915D90`, `8875EDF8`, `8877DC90`, `88906308`, `888E53B8`, `8890BD70`, `887295D0`, `889C8998`, `8872EBF8`.
    - So forcing 3D off has to hold those bytes at 0 where they are written (or where the object is filled), not hook the two accessors. The writers are not identified yet: a plain search for `stb` to `0x70`/`0x71` gives 214 hits, mostly stack frames.
    - Next step: the constructor or loader behind the writes at `0x88211688`/`0x88211728`, then a `[[modules.midasm_hook]]` after it. Whether midasm hooks in a runtime-DLL Module link (their function must reach `topspin4_Swing_DLL.dll`) is unproven, and so is the hook.
  - **Agassi outfit.** No string names Agassi or a preorder in Swing. Outfits go through a DLC layer (`DLC_EnumerateOutfits`, `DLC_UseOutfit`, `DLC_OpenOutfitArchive`, `TCR_OutfitContainsDLC`), which suggests the preorder outfit was a downloadable package. The disc has `Baked_Agassi0-2.arc`, but every player has three baked archives, so that is no sign of an extra outfit on the disc.
  - Open question for the owner, before any unlock is built: is the outfit's data on the disc at all? If it was a download, there is nothing to unlock, and story 45 in spec.md needs rewording.
- 2026-10-25 (Claude): the owner doesn't know whether the Agassi outfit is on the disc. The unlock half of this ticket waits on evidence from the outfit DLC path (`DLC_EnumerateOutfits` and its callers). If the outfit turns out to be download-only, the 1.0 goal in docs/goals.md and story 45 in spec.md are for the owner to drop. The 3D half does not depend on this.
