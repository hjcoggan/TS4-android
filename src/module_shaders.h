// The game's Module mode (--ts4_prepare_modules), the Launcher's shader step for the shaders no archive holds:
// about 90 shader containers sit in the data of the Loader and Swing Modules, passed to Create*Shader as they are. The mode loads both Modules as play does (Title
// Update 1 applied) without running any guest code, writes each container it finds into the shader top-up
// folder under its dump name (native/shader_topup.h), prints a header and one result line, and exits:
//
//   topspin4-prepare-modules 1
//   result status=ok containers=91 ms=2400
//   result status=failed reason=module_load ms=40        (exit code 2; ok is exit code 0)
//
// The Launcher reads these lines (GameModuleShaders.cs). The two containers Swing writes word by word are not
// in any Module's data; live top-up converts those.

#pragma once

#include <cstddef>
#include <cstdint>
#include <span>
#include <vector>

namespace rex {
class Runtime;
}

namespace topspin4::module_shaders {

// --ts4_prepare_modules was given.
bool Requested();

// Runs the mode on a runtime that has set up and loaded Boot but not launched it, and exits the process.
[[noreturn]] void WriteAndExit(rex::Runtime& runtime);

struct Found {
  size_t offset = 0;
  size_t size = 0;
  bool pixel = false;
};

namespace detail {
inline uint32_t Be32(const uint8_t* p) {
  return uint32_t(p[0]) << 24 | uint32_t(p[1]) << 16 | uint32_t(p[2]) << 8 | p[3];
}
}  // namespace detail

// A container is big-endian 0x102A1100 (pixel) or 0x102A1101 (vertex), then the header size and the microcode
// size, which together are the container's length. Keep this equal to ShaderContainer.IsContainer in the
// Launcher (ShaderContainer.cs); it also bounds a container as the game's ValidContainerHeader does.
inline constexpr size_t kMaxContainerSize = 1u << 20;
inline constexpr size_t kMinHeaderSize = 12;

// The containers in memory, at 4-byte-aligned offsets, in order. A container's own bytes are not searched again.
inline std::vector<Found> Scan(std::span<const uint8_t> memory) {
  std::vector<Found> found;
  for (size_t at = 0; at + kMinHeaderSize <= memory.size(); at += 4) {
    const uint8_t* p = memory.data() + at;
    if (p[0] != 0x10 || p[1] != 0x2A || p[2] != 0x11 || p[3] > 1) {
      continue;
    }
    const uint64_t header = detail::Be32(p + 4);
    const uint64_t microcode = detail::Be32(p + 8);
    const uint64_t size = header + microcode;
    if (header < kMinHeaderSize || microcode == 0 || size > kMaxContainerSize ||
        at + size > memory.size()) {
      continue;
    }
    found.push_back({at, size_t(size), p[3] == 0});
    at = ((at + size_t(size) + 3) & ~size_t(3)) - 4;  // the loop adds 4: on to the next aligned word
  }
  return found;
}

}  // namespace topspin4::module_shaders
