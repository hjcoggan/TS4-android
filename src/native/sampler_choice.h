// The sampler a texture fetch gets: its anisotropy, LOD bias and the key the
// native sampler cache stores it under. Anisotropic filtering and the global
// texture sharpness bias (image_quality.h) are part of the key, so a change
// during the session makes the next lookup create a sampler with the new values;
// the old samplers stay valid for frames still in flight (live-settings 03).

#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>

#include <rex/graphics/xenos.h>

namespace topspin4::native {

struct SamplerChoice {
  float anisotropy = 1.0f;  // 1 = off
  float lod_bias = 0.0f;    // the sampler's mipLodBias
  uint64_t key = 0;
};

// texture_filtering follows the emulated path's anisotropic_override: it
// applies to linear, mipmapped textures (-1 = the game's setting, 0 = off,
// 1..5 = 1x..16x); other textures keep the fetch constant's own setting.
// device_max: the device's maxSamplerAnisotropy, 0 without samplerAnisotropy.
// global_lod_bias: the image-quality plan's texture sharpness bias, added to the fetch
// constant's own (Swing sharpens foliage with -1 and -2); the sum stays within max_lod_bias
// (the device's maxSamplerLodBias).
inline SamplerChoice ChooseSampler(const rex::graphics::xenos::xe_gpu_texture_fetch_t& fetch,
                                   uint32_t min_level, uint32_t max_level,
                                   int32_t texture_filtering, float device_max,
                                   float global_lod_bias = 0.0f, float max_lod_bias = 2.0f) {
  using rex::graphics::xenos::TextureFilter;
  SamplerChoice choice;
  if (device_max >= 1.0f) {
    uint32_t aniso = uint32_t(fetch.aniso_filter);
    if (texture_filtering >= 0 && texture_filtering <= 5 && max_level > min_level &&
        fetch.mag_filter == TextureFilter::kLinear && fetch.min_filter == TextureFilter::kLinear &&
        uint32_t(fetch.mip_filter) <= 1) {
      aniso = uint32_t(texture_filtering);
    }
    if (aniso >= 1 && aniso <= 5) {
      choice.anisotropy = std::min(float(1u << (aniso - 1)), device_max);
    }
  }
  // kBaseMap (mip filter 2) and single-level images sample one level: nothing to bias.
  const bool mipmapped = max_level > min_level && uint32_t(fetch.mip_filter) != 2;
  // The global bias in the key in 1/32 steps, as the fetch's own.
  const int32_t global_steps = int32_t(std::lround(global_lod_bias * 32.0f));
  if (mipmapped) {
    choice.lod_bias = std::clamp(float(fetch.lod_bias) / 32.0f + float(global_steps) / 32.0f,
                                 -max_lod_bias, max_lod_bias);
  }
  const uint32_t fields = uint32_t(fetch.clamp_x) | uint32_t(fetch.clamp_y) << 3 |
                          uint32_t(fetch.clamp_z) << 6 | uint32_t(fetch.mag_filter) << 9 |
                          uint32_t(fetch.min_filter) << 11 | uint32_t(fetch.mip_filter) << 13 |
                          uint32_t(fetch.border_color) << 15 | min_level << 17 | max_level << 21;
  // The fetch's own LOD bias (dword 4, 5 fractional bits), which the sampler applies.
  choice.key = uint64_t(fields) | uint64_t(choice.anisotropy) << 32 |
               uint64_t(uint32_t(fetch.lod_bias) & 0x3FF) << 40 |
               uint64_t(uint32_t(global_steps) & 0xFF) << 50;
  return choice;
}

}  // namespace topspin4::native
