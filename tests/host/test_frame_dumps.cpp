// src/frame_dumps.h: the --ts4_dump_frames list and the PNG encoder the frame dumps write with.

#include <catch_amalgamated.hpp>

#include <cstdint>
#include <set>
#include <vector>

#include "frame_dumps.h"

using namespace topspin4::frame_dumps;

namespace {

uint32_t BE32(const std::vector<uint8_t>& b, size_t at) {
  return uint32_t(b[at]) << 24 | uint32_t(b[at + 1]) << 16 | uint32_t(b[at + 2]) << 8 | b[at + 3];
}

}  // namespace

TEST_CASE("a frame list is numbers separated by commas, spaces allowed, kept in order") {
  std::set<uint64_t> frames;
  REQUIRE(ParseFrameList("1400, 480,1050", frames));
  CHECK(frames == std::set<uint64_t>{480, 1050, 1400});
  REQUIRE(ParseFrameList("", frames));
  CHECK(frames.empty());
}

TEST_CASE("a frame list with anything but numbers is rejected and gives no frames") {
  std::set<uint64_t> frames{1};
  CHECK_FALSE(ParseFrameList("480,abc", frames));
  CHECK(frames.empty());
  CHECK_FALSE(ParseFrameList("480,,1050", frames));
  CHECK_FALSE(ParseFrameList("-5", frames));
}

TEST_CASE("dump files are named by the six-digit guest frame") {
  CHECK(DumpFileName(480) == "frame-000480.png");
  CHECK(DumpFileName(1234567) == "frame-1234567.png");
}

TEST_CASE("the PNG is signed, sized, and ends with IEND") {
  const uint32_t w = 3, h = 2;
  const std::vector<uint8_t> rgb(w * h * 3, 0x7F);
  const std::vector<uint8_t> png = EncodePngRgb(rgb.data(), w, h);
  const std::vector<uint8_t> signature = {0x89, 'P', 'N', 'G', '\r', '\n', 0x1A, '\n'};
  REQUIRE(std::vector<uint8_t>(png.begin(), png.begin() + 8) == signature);
  CHECK(BE32(png, 8) == 13);  // IHDR length
  CHECK(std::string(png.begin() + 12, png.begin() + 16) == "IHDR");
  CHECK(BE32(png, 16) == w);
  CHECK(BE32(png, 20) == h);
  CHECK(png[24] == 8);  // bit depth
  CHECK(png[25] == 2);  // RGB
  CHECK(std::string(png.end() - 8, png.end() - 4) == "IEND");
}

TEST_CASE("the image data is stored rows behind filter byte 0, raw") {
  const uint32_t w = 2, h = 1;
  const std::vector<uint8_t> rgb = {1, 2, 3, 4, 5, 6};
  const std::vector<uint8_t> png = EncodePngRgb(rgb.data(), w, h);
  // IDAT follows IHDR (8 + 25 bytes): zlib header, one final stored block, then the row.
  const size_t idat = 33;
  CHECK(std::string(png.begin() + idat + 4, png.begin() + idat + 8) == "IDAT");
  const size_t z = idat + 8;
  CHECK(png[z] == 0x78);
  CHECK(png[z + 2] == 1);  // final stored block
  const std::vector<uint8_t> row(png.begin() + z + 7, png.begin() + z + 7 + 7);
  CHECK(row == std::vector<uint8_t>{0, 1, 2, 3, 4, 5, 6});
}
