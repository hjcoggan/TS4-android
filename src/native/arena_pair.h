// Which of the geometry cache's two arenas is in use. A full arena used to be freed (retired)
// and a new 128 MB one allocated; on Windows that allocation delayed the next flip by 3-4 ms,
// the last gameplay stutters at 3x Resolution scale with 8x MSAA (docs/research/
// native-draw-cost.md). Both arenas are allocated once and the cache switches between them: the
// other one was last written a whole fill earlier (seconds), so no frame on the GPU still reads
// it, unless two fills fall within the frames in flight; Switch refuses then and the caller
// replaces the arena as before. Pure.

#pragma once

#include <cstdint>

namespace topspin4::native {

class ArenaPair {
 public:
  explicit ArenaPair(uint64_t frames_in_flight) : frames_in_flight_(frames_in_flight) {}

  uint32_t Current() const { return current_; }

  // The current arena is full at `frame`: use the other one if no frame in flight can still read
  // it. False leaves the current one in use.
  bool Switch(uint64_t frame) {
    if (switched_ && frame < last_switch_ + frames_in_flight_) {
      return false;
    }
    current_ ^= 1;
    switched_ = true;
    last_switch_ = frame;
    return true;
  }

 private:
  uint64_t frames_in_flight_;
  uint32_t current_ = 0;
  bool switched_ = false;
  uint64_t last_switch_ = 0;
};

}  // namespace topspin4::native
