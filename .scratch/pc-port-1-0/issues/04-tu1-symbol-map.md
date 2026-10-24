# 04: TU1 symbol map

**What to build:** The topspin4-decomp names, made for the base build, are carried over to the TU1 images and loaded into the Ghidra project, so hooks and seeds use readable names.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] A Ghidra project of the three TU1 Modules exists locally, with the register save/restore helper repair applied
- [x] topspin4-decomp symbols are matched from base to TU1 by function similarity (not by address, because Swing was relinked)
- [x] The match rate is reported, and unmatched names are listed
- [x] Script-command names (title server, online, TSU set) are matched in TU1 Swing
- [x] The resulting symbol map is kept in a form manifests and hooks can consume, containing names and addresses only

## Comments

- 2026-10-24 (Claude): Findings are in `docs/research/tu1-symbol-map.md`. `scripts/tu_symbol_map.py` maps topspin4-decomp's base-build names to TU1 through `scripts/tu_addrmap.py`, copied from WWE and made per-Module and relink-tolerant. `.pdata` functions match 100% for Boot, 100% for Loader and 99.5% for Swing. 553 of 586 Swing names are mapped, and the 33 unmatched are listed in the report. 55 script commands are mapped, including every title-server and online command; 49 of them are paired with their command string by a registration function in both images. `symbols/tu1-symbols.csv` holds names and addresses only, in the decomp's `module,address,name,confidence,evidence` columns. The Ghidra project is local and git-ignored (`ghidra/TopSpin4TU1`). `scripts/ghidra-project.ps1` imports the three TU1-patched XEX images through the installed Xenon360 loader and analyzes them. It then repairs the register save helpers in each Module (178 / 4,816 / 11,786 calls) and applies the CSV to Swing: 553 names, 73 of which needed a function created first. Loader has no decomp names, so it carries only its matched addresses.
