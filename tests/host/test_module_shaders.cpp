#include <catch_amalgamated.hpp>

#include <algorithm>
#include <cstdint>
#include <span>
#include <utility>
#include <vector>

#include "module_shaders.h"

using topspin4::module_shaders::Found;
using topspin4::module_shaders::Scan;

namespace {

// flags, header size, microcode size, then the header's remaining bytes and the microcode.
std::vector<uint8_t> Container(bool vertex, uint32_t header, uint32_t microcode, uint8_t fill = 0xAB) {
  std::vector<uint8_t> bytes(header + microcode, fill);
  bytes[0] = 0x10;
  bytes[1] = 0x2A;
  bytes[2] = 0x11;
  bytes[3] = vertex ? 1 : 0;
  for (int i = 0; i < 4; ++i) {
    bytes[4 + i] = uint8_t(header >> (24 - 8 * i));
    bytes[8 + i] = uint8_t(microcode >> (24 - 8 * i));
  }
  return bytes;
}

// Module memory: zeros, with each part put at its offset.
std::vector<uint8_t> Memory(size_t size,
                            const std::vector<std::pair<size_t, std::vector<uint8_t>>>& parts) {
  std::vector<uint8_t> memory(size, 0);
  for (const auto& [offset, bytes] : parts) {
    std::copy_n(bytes.begin(), std::min(bytes.size(), size - offset), memory.begin() + offset);
  }
  return memory;
}

}  // namespace

TEST_CASE("module scan finds pixel and vertex containers at their offsets") {
  const auto memory = Memory(256, {{16, Container(false, 16, 8)}, {64, Container(true, 20, 12)}});

  const std::vector<Found> found = Scan(memory);

  REQUIRE(found.size() == 2);
  CHECK(found[0].offset == 16);
  CHECK(found[0].size == 24);
  CHECK(found[0].pixel);
  CHECK(found[1].offset == 64);
  CHECK(found[1].size == 32);
  CHECK_FALSE(found[1].pixel);
}

TEST_CASE("module scan ignores the magic without sizes that add up inside the memory") {
  // Whole, it is found; cut short by the end of the memory it is not.
  CHECK(Scan(Memory(64, {{0, Container(false, 16, 8)}})).size() == 1);
  CHECK(Scan(std::span<const uint8_t>(Memory(64, {{0, Container(false, 16, 8)}})).first(20)).empty());
  // No microcode, a header too small to hold the sizes, and a size over the game's limit.
  CHECK(Scan(Memory(64, {{0, Container(false, 16, 0)}})).empty());
  CHECK(Scan(Memory(64, {{0, Container(false, 8, 16)}})).empty());
  CHECK(Scan(Memory(64, {{0, Container(false, 16, (1u << 20) + 4)}})).empty());
  // Another flag byte: not a pixel or vertex container.
  auto other = Container(false, 16, 8);
  other[3] = 2;
  CHECK(Scan(Memory(64, {{0, other}})).empty());
}

TEST_CASE("module scan reads aligned words only and does not search inside a container") {
  CHECK(Scan(Memory(64, {{18, Container(false, 16, 8)}})).empty());

  // A second container's bytes inside the first are part of the first.
  auto outer = Container(false, 16, 24);
  const auto inner = Container(true, 16, 8);
  std::copy(inner.begin(), inner.end(), outer.begin() + 16);
  const auto found = Scan(Memory(128, {{32, outer}}));
  REQUIRE(found.size() == 1);
  CHECK(found[0].offset == 32);
}

TEST_CASE("module scan finds containers back to back") {
  const auto first = Container(false, 16, 8);
  const auto second = Container(true, 16, 8, 0x11);
  const auto found = Scan(Memory(96, {{0, first}, {first.size(), second}}));

  REQUIRE(found.size() == 2);
  CHECK(found[1].offset == first.size());
}
