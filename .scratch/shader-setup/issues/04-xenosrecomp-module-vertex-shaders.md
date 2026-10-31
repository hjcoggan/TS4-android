# 04: XenosRecomp converts the 64 Module vertex shaders

**What to build:** XenosRecomp cannot convert 64 of the 92 vertex shaders in the Loader and Swing Modules' data, so setup leaves them out of the shader pack (ticket 01). Draws that use one skip with `no_shader` on Native. Seen causes: registers past r31 and an undeclared `iPosition0` input. A trial patch raising the register count cut the failures from 64 to 55 only. Fix them as patches in `patches/xenosrecomp/` (this changes the converter id, so every pack is rebuilt at the next Launcher start).

**Blocked by:** 01

**Status:** needs-triage

- [ ] `topspin4-setup` on a fresh install reports no shaders left out
