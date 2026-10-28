// The host rectangle of a guest clear, and whether it covers the whole target. A clear of the
// whole target is made as the pass's load operation (VK_ATTACHMENT_LOAD_OP_CLEAR), which AMD
// GPUs do as a fast clear; vkCmdClearAttachments inside the pass cleared every sample, ~1.6 ms per
// 8x MSAA target at 4x Resolution scale (RenderDoc; docs/research/native-draw-cost.md). Pure.

#pragma once

#include <algorithm>
#include <cstdint>

namespace topspin4::native {

struct ClearRect {
  int32_t x = 0, y = 0;
  uint32_t width = 0, height = 0;
  bool covers_target = false;

  // rect: the guest's left, top, right, bottom; scale: the target's Resolution scale. An empty
  // or negative rectangle clears the whole target.
  static ClearRect Of(const int32_t rect[4], uint32_t scale, uint32_t target_width,
                      uint32_t target_height) {
    const int32_t s = int32_t(scale);
    const int32_t left = rect[0] * s, top = rect[1] * s;
    const int32_t right = std::min<int32_t>(rect[2] * s, int32_t(target_width));
    const int32_t bottom = std::min<int32_t>(rect[3] * s, int32_t(target_height));
    ClearRect r;
    if (right > left && bottom > top && left >= 0 && top >= 0) {
      r = {left, top, uint32_t(right - left), uint32_t(bottom - top)};
    } else {
      r = {0, 0, target_width, target_height};
    }
    r.covers_target = r.x == 0 && r.y == 0 && r.width == target_width && r.height == target_height;
    return r;
  }
};

}  // namespace topspin4::native
