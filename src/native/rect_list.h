// Xenos rectangle lists (primitive 8): each rectangle is three vertices, corners v0, v1 and v2,
// and the GPU makes the fourth, opposite v0, as v1 + v2 - v0. Vulkan has no such primitive, so
// a rectangle list is drawn as a triangle list of (v0, v1, v2) and (v1, v3, v2), with v3 made
// here: float words get v1 + v2 - v0, other words (packed colours, integers) copy v0, as a
// rectangle's colour is the same at every corner in the draws seen so far (Swing's mip chain).

#pragma once

#include <cstdint>
#include <cstring>
#include <vector>

namespace topspin4::native::rect_list {

// words: host-endian vertex data, stride_words per vertex, rect_vertices a multiple of 3.
// float_words: bit i set when word i of a vertex is a 32-bit float. Returns 6 vertices per
// rectangle.
inline std::vector<uint32_t> Expand(const uint32_t* words, uint32_t stride_words,
                                    uint32_t rect_vertices, uint64_t float_words) {
  const uint32_t rects = rect_vertices / 3;
  std::vector<uint32_t> out(size_t(rects) * 6 * stride_words);
  for (uint32_t r = 0; r < rects; ++r) {
    const uint32_t* v0 = words + size_t(r) * 3 * stride_words;
    const uint32_t* v1 = v0 + stride_words;
    const uint32_t* v2 = v1 + stride_words;
    uint32_t* o = out.data() + size_t(r) * 6 * stride_words;
    std::vector<uint32_t> v3(v0, v0 + stride_words);
    for (uint32_t w = 0; w < stride_words && w < 64; ++w) {
      if (float_words & (uint64_t(1) << w)) {
        float a, b, c;
        std::memcpy(&a, v0 + w, 4);
        std::memcpy(&b, v1 + w, 4);
        std::memcpy(&c, v2 + w, 4);
        const float d = b + c - a;
        std::memcpy(&v3[w], &d, 4);
      }
    }
    const uint32_t* order[6] = {v0, v1, v2, v1, v3.data(), v2};
    for (uint32_t i = 0; i < 6; ++i) {
      std::memcpy(o + size_t(i) * stride_words, order[i], size_t(stride_words) * 4);
    }
  }
  return out;
}

}  // namespace topspin4::native::rect_list
