// Frame dumps for scene runs (ticket 09), adapted from WWE All Stars' frame dumps and
// --wwe_exit_after_frame (ADR-0004).
//
//   --ts4_dump_frames=300,1100   guest frames to save, each as <dump_dir>/frame-NNNNNN.png
//   --ts4_dump_dir=<dir>         where they go (created if missing)
//   --ts4_dump_presented=WxH     also save each dumped frame as the presenter shows it in an
//                                output of that size, as frame-NNNNNN-presented.png, and name it
//                                in the report's dump entry (presented_file, presented_width,
//                                presented_height). Needs rex::ui::Presenter::
//                                CapturePresentedOutput(RawImage&, width, height): the guest
//                                output painted as PaintAndPresent would for a width x height
//                                window (fit, bilinear or CAS effect, no UI), read back as
//                                RGBA8. Without it the run logs an error and dumps no such file.
//   --ts4_exit_after_frame=N     after frame N is shown (and dumped), write the session report
//                                and exit with code 0
//   --ts4_session_report=<file>  the session report (JSON), written on that exit
//   --ts4_renderdoc_frame=N      under RenderDoc, capture exactly guest frame N
//   --ts4_fps_log                log "fps: <rate> (guest swaps <n>)" once a second
//   --ts4_frame_times_csv=<file> the host time of every frame shown, for scripts/frame_pacing.py
//
// A frame number counts the swaps before it, the clock the scripted pad's `at <frame>` uses
// (src/script_input.h). Each dump is the presenter's guest output of that swap, taken on the
// GPU thread as the swap is processed (patch 0031), so it is the frame the player saw, after
// the gamma ramp, whichever Module's Direct3D drew it.

#pragma once

#include <algorithm>
#include <array>
#include <charconv>
#include <cstdint>
#include <set>
#include <string>
#include <string_view>
#include <vector>

#include <fmt/format.h>

namespace rex {
class Runtime;
}

namespace topspin4::frame_dumps {

// "300,1100, 2400"; false (and no frames) when an entry is not a number.
inline bool ParseFrameList(std::string_view list, std::set<uint64_t>& out) {
  out.clear();
  std::set<uint64_t> frames;
  size_t start = 0;
  while (start <= list.size() && !list.empty()) {
    const size_t end = std::min(list.find(',', start), list.size());
    std::string_view item = list.substr(start, end - start);
    while (!item.empty() && item.front() == ' ') item.remove_prefix(1);
    while (!item.empty() && item.back() == ' ') item.remove_suffix(1);
    uint64_t frame = 0;
    const auto [ptr, error] = std::from_chars(item.data(), item.data() + item.size(), frame);
    if (item.empty() || error != std::errc() || ptr != item.data() + item.size()) {
      return false;
    }
    frames.insert(frame);
    start = end + 1;
  }
  out = std::move(frames);
  return true;
}

inline std::string DumpFileName(uint64_t frame) { return fmt::format("frame-{:06}.png", frame); }

// The same frame as the presenter shows it in the output (--ts4_dump_presented).
inline std::string PresentedFileName(uint64_t frame) {
  return fmt::format("frame-{:06}-presented.png", frame);
}

struct OutputSize {
  uint32_t width = 0;
  uint32_t height = 0;
};

// "2560x1440" (either case of x, spaces around allowed); an empty text is no size (0x0). False,
// and no size, when it is not two positive numbers of at most 16384.
inline bool ParseOutputSize(std::string_view text, OutputSize& out) {
  out = {};
  while (!text.empty() && text.front() == ' ') text.remove_prefix(1);
  while (!text.empty() && text.back() == ' ') text.remove_suffix(1);
  if (text.empty()) return true;
  const size_t x = text.find_first_of("xX");
  if (x == std::string_view::npos) return false;
  uint32_t parts[2] = {};
  const std::string_view pieces[2] = {text.substr(0, x), text.substr(x + 1)};
  for (int i = 0; i < 2; ++i) {
    const std::string_view piece = pieces[i];
    const auto [ptr, error] =
        std::from_chars(piece.data(), piece.data() + piece.size(), parts[i]);
    if (piece.empty() || error != std::errc() || ptr != piece.data() + piece.size() ||
        parts[i] == 0 || parts[i] > 16384) {
      return false;
    }
  }
  out = {parts[0], parts[1]};
  return true;
}

// An 8-bit RGB PNG with uncompressed (stored) deflate blocks: no compression library needed,
// and the files are only screenshots. Copied from WWE All Stars' frame_capture.h.
inline std::vector<uint8_t> EncodePngRgb(const uint8_t* rgb, uint32_t width, uint32_t height) {
  static const std::array<uint32_t, 256> crc_table = [] {
    std::array<uint32_t, 256> table{};
    for (uint32_t n = 0; n < 256; ++n) {
      uint32_t c = n;
      for (int k = 0; k < 8; ++k) c = c & 1 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
      table[n] = c;
    }
    return table;
  }();
  std::vector<uint8_t> out = {0x89, 'P', 'N', 'G', '\r', '\n', 0x1A, '\n'};
  auto be32 = [&](uint32_t v) {
    for (int s = 24; s >= 0; s -= 8) out.push_back(uint8_t(v >> s));
  };
  auto chunk = [&](const char* type, const std::vector<uint8_t>& data) {
    be32(uint32_t(data.size()));
    const size_t start = out.size();
    out.insert(out.end(), type, type + 4);
    out.insert(out.end(), data.begin(), data.end());
    uint32_t crc = 0xFFFFFFFFu;
    for (size_t i = start; i < out.size(); ++i) crc = crc_table[(crc ^ out[i]) & 0xFF] ^ (crc >> 8);
    be32(crc ^ 0xFFFFFFFFu);
  };

  std::vector<uint8_t> ihdr;
  for (uint32_t v : {width, height}) {
    for (int s = 24; s >= 0; s -= 8) ihdr.push_back(uint8_t(v >> s));
  }
  ihdr.insert(ihdr.end(), {8, 2, 0, 0, 0});  // 8-bit RGB, no interlace
  chunk("IHDR", ihdr);

  // Each row behind filter byte 0, in a zlib stream of stored blocks.
  std::vector<uint8_t> raw;
  raw.reserve(size_t(height) * (size_t(width) * 3 + 1));
  for (uint32_t y = 0; y < height; ++y) {
    raw.push_back(0);
    raw.insert(raw.end(), rgb + size_t(y) * width * 3, rgb + size_t(y + 1) * width * 3);
  }
  std::vector<uint8_t> z = {0x78, 0x01};
  for (size_t at = 0; at < raw.size() || raw.empty(); at += 65535) {
    const size_t n = std::min<size_t>(65535, raw.size() - at);
    z.push_back(at + n == raw.size() ? 1 : 0);
    z.insert(z.end(), {uint8_t(n), uint8_t(n >> 8), uint8_t(~n), uint8_t(~n >> 8)});
    z.insert(z.end(), raw.begin() + at, raw.begin() + at + n);
    if (raw.empty()) break;
  }
  uint32_t a = 1, b = 0;
  for (uint8_t v : raw) {
    a = (a + v) % 65521;
    b = (b + a) % 65521;
  }
  for (int s = 24; s >= 0; s -= 8) z.push_back(uint8_t((b << 16 | a) >> s));
  chunk("IDAT", z);
  chunk("IEND", {});
  return out;
}

// Called on the --ts4_exit_after_frame exit, before the session report is written: the
// session's last logs, and further members for the report's top-level object ("name": value,
// comma separated, no trailing comma), or an empty string.
void SetSessionEnd(std::string (*session_end)());

// Reads the cvars and, if any is set, watches every shown frame. Call once the runtime is set
// up (the app's OnPostSetup).
void Install(rex::Runtime* runtime);

}  // namespace topspin4::frame_dumps
