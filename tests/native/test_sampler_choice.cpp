#include <catch_amalgamated.hpp>

#include "native/sampler_choice.h"

using rex::graphics::xenos::TextureFilter;
using rex::graphics::xenos::xe_gpu_texture_fetch_t;
using topspin4::native::ChooseSampler;

namespace {

// A linear, mipmapped texture: the kind Texture filtering applies to.
xe_gpu_texture_fetch_t LinearMipmapped() {
  xe_gpu_texture_fetch_t fetch = {};
  fetch.mag_filter = fetch.min_filter = TextureFilter::kLinear;
  fetch.mip_filter = TextureFilter::kLinear;
  return fetch;
}

constexpr float kDeviceMax = 16.0f;

}  // namespace

// Live settings 03: a change reaches the next lookup as a new cache key, so the
// cache creates a sampler with the new anisotropy beside the old one.
TEST_CASE("a texture filtering change gives a new sampler with the new anisotropy") {
  const xe_gpu_texture_fetch_t fetch = LinearMipmapped();
  const auto at_4x = ChooseSampler(fetch, 0, 5, /*texture_filtering=*/3, kDeviceMax);
  const auto at_16x = ChooseSampler(fetch, 0, 5, /*texture_filtering=*/5, kDeviceMax);
  CHECK(at_4x.anisotropy == 4.0f);
  CHECK(at_16x.anisotropy == 16.0f);
  CHECK(at_4x.key != at_16x.key);
  const auto off = ChooseSampler(fetch, 0, 5, /*texture_filtering=*/0, kDeviceMax);
  CHECK(off.anisotropy == 1.0f);
  CHECK(off.key != at_4x.key);
}

TEST_CASE("the same texture filtering finds the cached sampler again") {
  const xe_gpu_texture_fetch_t fetch = LinearMipmapped();
  CHECK(ChooseSampler(fetch, 0, 5, 3, kDeviceMax).key == ChooseSampler(fetch, 0, 5, 3, kDeviceMax).key);
}

TEST_CASE("textures that Texture filtering skips keep their own sampler") {
  // Point-sampled, or a single level: the fetch constant's own setting (none) wins.
  xe_gpu_texture_fetch_t point = LinearMipmapped();
  point.min_filter = TextureFilter::kPoint;
  CHECK(ChooseSampler(point, 0, 5, 5, kDeviceMax).anisotropy == 1.0f);
  CHECK(ChooseSampler(point, 0, 5, 5, kDeviceMax).key == ChooseSampler(point, 0, 5, 3, kDeviceMax).key);
  const xe_gpu_texture_fetch_t single = LinearMipmapped();
  CHECK(ChooseSampler(single, 0, 0, 5, kDeviceMax).anisotropy == 1.0f);
}

TEST_CASE("the device caps anisotropy, and a device without it gets none") {
  CHECK(ChooseSampler(LinearMipmapped(), 0, 5, 5, 8.0f).anisotropy == 8.0f);
  CHECK(ChooseSampler(LinearMipmapped(), 0, 5, 5, /*device_max=*/0.0f).anisotropy == 1.0f);
}

TEST_CASE("the global sharpness bias adds to the fetch's own, within the device's limit") {
  xe_gpu_texture_fetch_t fetch = LinearMipmapped();
  fetch.lod_bias = -32;  // -1 mip in the fetch constant (5 fractional bits)
  const auto own = ChooseSampler(fetch, 0, 5, -1, kDeviceMax, 0.0f, 2.0f);
  CHECK(own.lod_bias == -1.0f);
  const auto sharper = ChooseSampler(fetch, 0, 5, -1, kDeviceMax, -0.5f, 2.0f);
  CHECK(sharper.lod_bias == -1.5f);
  CHECK(sharper.key != own.key);
  CHECK(ChooseSampler(fetch, 0, 5, -1, kDeviceMax, -1.5f, 2.0f).lod_bias == -2.0f);
  // A single level has no mips to bias.
  CHECK(ChooseSampler(fetch, 0, 0, -1, kDeviceMax, -0.5f, 2.0f).lod_bias == 0.0f);
}
