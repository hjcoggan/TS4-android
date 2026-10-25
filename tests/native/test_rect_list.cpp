// Rectangle lists drawn as triangle lists (src/native/rect_list.h).
#include <catch_amalgamated.hpp>

#include <cstdint>
#include <cstring>
#include <vector>

#include "native/rect_list.h"

using namespace topspin4::native;

namespace {

uint32_t Bits(float f) {
  uint32_t u;
  std::memcpy(&u, &f, 4);
  return u;
}

float Float(uint32_t u) {
  float f;
  std::memcpy(&f, &u, 4);
  return f;
}

}  // namespace

TEST_CASE("a rectangle becomes two triangles with the fourth corner opposite v0") {
  // Vertex: x, y (floats), colour (packed).
  const uint32_t colour = 0xFF8040C0;
  const std::vector<uint32_t> rect = {Bits(0), Bits(0), colour, Bits(4), Bits(0), colour,
                                      Bits(0), Bits(2), colour};
  const auto out = rect_list::Expand(rect.data(), 3, 3, 0b011);
  REQUIRE(out.size() == 6 * 3);
  // Order: v0 v1 v2, v1 v3 v2.
  CHECK(Float(out[0]) == 0.0f);
  CHECK(Float(out[3]) == 4.0f);
  CHECK(Float(out[7]) == 2.0f);
  CHECK(Float(out[9]) == 4.0f);
  CHECK(Float(out[12]) == 4.0f);  // v3.x = 4 + 0 - 0
  CHECK(Float(out[13]) == 2.0f);  // v3.y = 0 + 2 - 0
  CHECK(out[14] == colour);       // a packed word copies v0
  CHECK(Float(out[16]) == 2.0f);
}

TEST_CASE("vertices past the last whole rectangle are dropped") {
  const std::vector<uint32_t> words(4, 0);
  CHECK(rect_list::Expand(words.data(), 1, 4, 1).size() == 6);
}
