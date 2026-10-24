# 04: TU1 symbol map

**What to build:** The topspin4-decomp names, made for the base build, are carried over to the TU1 images and loaded into the Ghidra project, so hooks and seeds use readable names.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A Ghidra project of the three TU1 Modules exists locally, with the register save/restore helper repair applied
- [ ] topspin4-decomp symbols are matched from base to TU1 by function similarity (not by address, because Swing was relinked)
- [ ] The match rate is reported, and unmatched names are listed
- [ ] Script-command names (title server, online, TSU set) are matched in TU1 Swing
- [ ] The resulting symbol map is kept in a form manifests and hooks can consume, containing names and addresses only
