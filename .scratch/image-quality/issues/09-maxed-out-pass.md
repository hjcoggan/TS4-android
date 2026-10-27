# 09: Maxed-out pass and docs

**What to build:** With every image setting at its maximum (Auto or 4x scale, 8x MSAA, 16x anisotropic filtering, High CAS), every 1.0 scene runs stably and shows no new artifacts. The docs reflect the shipped Enhancements. Spec: `../spec.md`.

**Blocked by:** 02 (Anisotropic filtering), 03 (CAS sharpening), 05 (Texture offsets at scale), 06 (Auto Resolution scale), 08 (MSAA 8x)

**Status:** ready-for-agent

- [ ] All six scenes run at the maximum settings without device loss, and the frames show only edge differences against the 1x frames of the same run
- [ ] With every setting at its default, the dual-mode scenes still pass against the Oracle
- [ ] The GPU frame cost at the maximum settings is measured on the desktop machine and recorded in `docs/research/image-quality.md`
- [ ] Edge is tried at `draw_resolution_scale` 2, and the result (works or crashes) is recorded; where it works, a scaled frame is compared with ours
- [ ] The Enhancements line in `docs/goals.md` lists resolution scaling, MSAA, anisotropic filtering and CAS sharpening
- [ ] Pc-port ticket 28 is resolved with a pointer to these tickets
