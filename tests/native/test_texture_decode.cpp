// k_CTX1 decoding (src/native/texture_decode.h), checked against the SDK's ConvertTexelCTX1ToR8G8
// layout: endpoints g0, r0, g1, r1, then 2-bit indices, texel (x, y) at bit 2 * (x + 4 y).
#include <catch_amalgamated.hpp>

#include <cstdint>
#include <cstring>
#include <vector>

#include "native/texture_decode.h"

using namespace topspin4::native::texture_decode;

namespace {

std::vector<uint8_t> Block(uint8_t g0, uint8_t r0, uint8_t g1, uint8_t r1, uint32_t indices) {
  std::vector<uint8_t> block = {g0, r0, g1, r1, 0, 0, 0, 0};
  std::memcpy(block.data() + 4, &indices, 4);
  return block;
}

}  // namespace

TEST_CASE("each CTX1 index picks an endpoint or a third of the way between them") {
  // Texels 0..3 of row 0 use indices 0, 1, 2, 3.
  const auto block = Block(30, 90, 0, 0, 0b11100100);
  uint8_t out[4 * 4 * 2] = {};
  DecodeCtx1Block(block.data(), out, 8);
  CHECK(out[0] == 90);  // R of index 0: r0
  CHECK(out[1] == 30);  // G of index 0: g0
  CHECK(out[2] == 0);   // index 1: r1
  CHECK(out[3] == 0);
  CHECK(out[4] == 60);  // index 2: 2/3 r0 + 1/3 r1
  CHECK(out[5] == 20);
  CHECK(out[6] == 30);  // index 3: 1/3 r0 + 2/3 r1
  CHECK(out[7] == 10);
}

TEST_CASE("a CTX1 index's row comes from bits 8 y onward") {
  // Only texel (2, 3) uses index 1: bit 2 * (2 + 12) = 28.
  const auto block = Block(0, 0, 200, 100, 1u << 28);
  uint8_t out[4 * 4 * 2] = {};
  DecodeCtx1Block(block.data(), out, 8);
  CHECK(out[3 * 8 + 2 * 2 + 0] == 100);
  CHECK(out[3 * 8 + 2 * 2 + 1] == 200);
  CHECK(out[3 * 8 + 1 * 2 + 0] == 0);
}

TEST_CASE("a CTX1 level decodes block by block into packed rows") {
  std::vector<uint8_t> blocks = Block(0, 10, 0, 0, 0);
  const auto second = Block(0, 20, 0, 0, 0);
  blocks.insert(blocks.end(), second.begin(), second.end());
  std::vector<uint8_t> out(8 * 4 * 2);
  DecodeCtx1Level(blocks.data(), 2, 1, out.data());
  CHECK(out[0] == 10);            // texel (0, 0), first block
  CHECK(out[4 * 2] == 20);        // texel (4, 0), second block
  CHECK(out[3 * 16 + 7 * 2] == 20);  // texel (7, 3)
}
