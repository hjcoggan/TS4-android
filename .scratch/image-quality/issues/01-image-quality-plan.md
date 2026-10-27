# 01: Image-quality plan prefactor

**What to build:** One pure decision function, the image-quality plan, makes every image-quality choice the Native renderer applies. Inputs: the image settings, the output (window) size, the device limits (maximum image size, maximum sampler anisotropy, maximum LOD bias, supported sample counts) and the guest's requested sample count for a target. Outputs: the effective Resolution scale and whether it was clamped, the sample count for a target, the global texture sharpness bias, the anisotropy level and the texture-offset scale. The renderer reads these values instead of deciding them where they are used. This ticket changes no behaviour: the plan reproduces today's rules exactly, so later tickets only change rules inside the plan. Spec: `../spec.md`.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] The renderer takes scale, sample count, sharpness bias and anisotropy from the plan, not from separate decisions in the renderer
- [x] Unit tests cover the plan's outputs for today's settings (they pin current behaviour)
- [x] The existing dual-mode scenes still pass against the Oracle with no image change

## Comments

- 2026-10-25 (Claude): `src/native/image_quality.h` holds the plan: `MakePlan(settings, window, device limits)` gives the scale and whether it was clamped, the output plan, the global texture sharpness bias, the texture filtering (in `anisotropic_override`'s form) and CAS; `Plan::SamplesFor(guest samples)` gives a target's sample count; `TextureOffsetScale` the texel-offset scale. The renderer makes the plan as each frame begins (`MakeImagePlan`) and reads it for target creation (`TargetSamples`), the sampler cache (`textures::BeginFrame`), the output and the presenter's CAS. Device limits come from the Vulkan properties (`LimitsOf`). The prefactor reproduced the old rules exactly (bias -0.5 at 2x, -1 at 3x+, the guest's samples, Classic's filtering); `tests/native/test_image_quality.cpp` pinned them, and later tickets changed the rules inside the plan. On the prefactor build, all six dual-mode scenes pass (`dual_all.sh iq01`, `analysis/scenes/iq01/*/result.json`), dual worst tiles (K = 50) and mean FLIP as before the change: boot-main-menu 0/0, xbox-live-offline 0/0, exhibition-match 0, 0, 0, 0 (mean 0.0075 in the match, as in ticket 27), career-start 0 throughout, academy-lesson 13, 9, 0, 0, 0 (18, 10 before), king-of-the-court 0, 0, 0, 0, 40, 0, 0 (43 at 4300 before).
