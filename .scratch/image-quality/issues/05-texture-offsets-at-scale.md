# 05: Texture offsets at scale

**What to build:** At any Resolution scale, blur, bloom and shadow filtering cover the same guest area as at 1x. Converted shaders measure a fetch's texel offset in guest texels for resolved (scaled) textures, using a shader-converter patch plus a per-texture scale from the image-quality plan. Edge's `draw_resolution_scaled_texture_offsets` is the reference behaviour. This ticket also settles which render target is the shadow map, and pins any target that still breaks when scaled to 1x with a per-target rule. It can be developed with the existing debug scale option, so it doesn't wait for ticket 04. Spec: `../spec.md`.

**Blocked by:** 01 (Image-quality plan prefactor)

**Status:** ready-for-agent

- [ ] At 1x, converted shaders sample exactly as before (dual-mode scenes still pass against the Oracle)
- [ ] At 2x, a RenderDoc `history` on a bloom pixel and a shadow-edge pixel shows the same footprint in guest texels as at 1x
- [ ] At 2x, the match frame shrunk to 1280x720 shows no area differences in glow or shadow softness against the 1x frame
- [ ] The shadow map target is identified and recorded in `docs/research/native-renderer.md`
- [ ] Any target pinned to 1x is named, with the frame evidence for why
- [ ] The plan's texture-offset scale is unit-tested for resolved and ordinary textures
