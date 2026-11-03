// Guest texture formats Vulkan has no format for, decoded on the CPU as they are uploaded
// (texture_formats.h, FormatInfo::decode). Each decoder takes the guest's endian-swapped blocks.

#pragma once

#include <cstddef>
#include <cstdint>
#include <cstring>

namespace topspin4::native::texture_decode {

// k_CTX1: a 4x4 block of two 8-bit channels in 8 bytes: two endpoints (g0, r0, g1, r1; R is in
// the higher bits) and 2-bit indices, texel (x, y) at bit 2 * (x + 4 y), choosing an endpoint
// or one of two blends between them. Decoded to R8G8, as the SDK's ConvertTexelCTX1ToR8G8 does.
// out_pitch: bytes from one output row to the next.
inline void DecodeCtx1Block(const uint8_t block[8], uint8_t* out, size_t out_pitch) {
  const uint8_t g0 = block[0], r0 = block[1], g1 = block[2], r1 = block[3];
  uint32_t indices;
  std::memcpy(&indices, block + 4, 4);
  const uint8_t r[4] = {r0, r1, uint8_t(2.f / 3.f * r0 + 1.f / 3.f * r1),
                        uint8_t(1.f / 3.f * r0 + 2.f / 3.f * r1)};
  const uint8_t g[4] = {g0, g1, uint8_t(2.f / 3.f * g0 + 1.f / 3.f * g1),
                        uint8_t(1.f / 3.f * g0 + 2.f / 3.f * g1)};
  for (uint32_t y = 0; y < 4; ++y) {
    for (uint32_t x = 0; x < 4; ++x) {
      const uint32_t i = (indices >> ((x + y * 4) * 2)) & 3;
      out[y * out_pitch + x * 2 + 0] = r[i];
      out[y * out_pitch + x * 2 + 1] = g[i];
    }
  }
}

// A level of CTX1 blocks (bx x by, rows packed) to R8G8 texels (bx*4 x by*4, rows packed).
inline void DecodeCtx1Level(const uint8_t* blocks, uint32_t bx, uint32_t by, uint8_t* out) {
  const size_t out_pitch = size_t(bx) * 4 * 2;
  for (uint32_t y = 0; y < by; ++y) {
    for (uint32_t x = 0; x < bx; ++x) {
      DecodeCtx1Block(blocks + (size_t(y) * bx + x) * 8, out + y * 4 * out_pitch + x * 4 * 2,
                      out_pitch);
    }
  }
}

}  // namespace topspin4::native::texture_decode
