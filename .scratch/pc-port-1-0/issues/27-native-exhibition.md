# 27: Native Exhibition parity

**What to build:** The full Exhibition scene passes on Native, including readback-dependent faces and XPS geometry.

**Blocked by:** 26 (Native renderer boots to title), 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [ ] The Exhibition scene passes FLIP against the Classic envelope
- [ ] Opponent faces render correctly (resolve-to-texture readback)
- [ ] XPS CPU-generated geometry draws correctly
- [ ] No draw skips or fallbacks are reported
