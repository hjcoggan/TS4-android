// The core dynamic state every Native pipeline leaves dynamic (pipelines.cpp: viewport, scissor,
// blend constants, stencil reference and masks), set on the command buffer only when a draw
// changes it. Setting all of it before every draw made each small draw cost ~3 us of GPU time on
// an RX 7900 XT whatever the resolution: the crowd shots of a match (~3000 draws) took ~9 ms at
// 720p (docs/research/native-draw-cost.md). A new command buffer starts with nothing set (Reset).
// Pure: no Vulkan calls; the renderer emits what Update reports changed.

#pragma once

#include <array>
#include <cstdint>
#include <cstring>
#include <limits>

namespace topspin4::native {

struct CoreDynamicState {
  struct Viewport {
    float x = 0, y = 0, width = 0, height = 0, min_depth = 0, max_depth = 1;
  };
  struct Scissor {
    int32_t x = 0, y = 0;
    uint32_t width = 0, height = 0;
  };
  struct Stencil {
    uint32_t reference = 0, compare_mask = 0, write_mask = 0;
  };
  Viewport viewport;
  Scissor scissor;
  std::array<float, 4> blend{};
  Stencil stencil[2];  // front, back
};

class CoreDynamicStateCache {
 public:
  struct Changes {
    bool viewport = false, scissor = false, blend = false;
    bool reference[2] = {}, compare_mask[2] = {}, write_mask[2] = {};
    bool Any() const {
      return viewport || scissor || blend || reference[0] || reference[1] || compare_mask[0] ||
             compare_mask[1] || write_mask[0] || write_mask[1];
    }
  };

  // A new command buffer: nothing is set on it yet.
  void Reset() { set_ = false; }

  // What of `next` differs from what is set (everything after a Reset); `next` is then set.
  Changes Update(const CoreDynamicState& next) {
    Changes c;
    const bool all = !set_;
    c.viewport = all || !Same(next.viewport, last_.viewport);
    c.scissor = all || !Same(next.scissor, last_.scissor);
    c.blend = all || !Same(next.blend, last_.blend);
    for (int face = 0; face < 2; ++face) {
      c.reference[face] = all || next.stencil[face].reference != last_.stencil[face].reference;
      c.compare_mask[face] =
          all || next.stencil[face].compare_mask != last_.stencil[face].compare_mask;
      c.write_mask[face] = all || next.stencil[face].write_mask != last_.stencil[face].write_mask;
    }
    last_ = next;
    set_ = true;
    return c;
  }

 private:
  // By bits: a NaN equals the same NaN, and -0 differs from +0, as the GPU would see them.
  template <typename T>
  static bool Same(const T& a, const T& b) {
    return std::memcmp(&a, &b, sizeof(T)) == 0;
  }

  bool set_ = false;
  CoreDynamicState last_;
};

}  // namespace topspin4::native
