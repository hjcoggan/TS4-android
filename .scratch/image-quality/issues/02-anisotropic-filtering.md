# 02: Anisotropic filtering setting

**What to build:** A player setting for anisotropic filtering on both renderers: Original (the game's own filtering), 4x, 8x or 16x. The default becomes Original, so both renderers stop forcing 4x and the default look matches the Oracle again. A forced level applies only to linear, mipmapped fetches and is clamped to the device maximum. It changes live. Spec: `../spec.md`.

**Blocked by:** 01 (Image-quality plan prefactor)

**Status:** ready-for-agent

- [ ] The setting loads from the settings file and the command line, and an invalid value falls back to Original
- [ ] At Original, neither Classic nor Native forces anisotropy
- [ ] Default frames on both renderers are compared with the Oracle once and match within the dual-run tolerance
- [ ] At 16x, a court crop (far baseline and banners) is compared with Edge at `anisotropic_override = 5`
- [ ] The setting is listed in the session report's enhancements and in the scene runner's list, so a parity run with it on fails
- [ ] Unit tests cover the plan's anisotropy level and its clamp
