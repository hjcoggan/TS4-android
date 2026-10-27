# 08: MSAA 8x

**What to build:** An 8x choice for the MSAA setting. The game's alpha-to-mask shader patch is extended to an 8-sample case, so alpha-masked surfaces keep their look at 8x. Spec: `../spec.md`.

**Blocked by:** 07 (MSAA 4x)

**Status:** ready-for-agent

- [ ] 8x is accepted in the settings file and on the command line
- [ ] At 8x, the match pass renders at 8 samples on a device that supports it, and falls back on one that doesn't
- [ ] Academy foliage at 8x matches its look at Original (no holes or solid quads)
- [ ] The match frame at 8x is compared with the Original frame of the same run: only edge differences
