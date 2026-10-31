// The load flash fix's present count (load_flash.h), apart from the sink so host tests build it.

#pragma once

#include <atomic>
#include <cstdint>

namespace topspin4::load_flash {

// Armed by the loading screen's end (any thread), spent by the presents (the render thread).
class Hold {
 public:
  void Arm(uint32_t presents) { remaining_.store(presents, std::memory_order_relaxed); }
  // True for each of the armed presents, then false until armed again.
  bool TakePresent() {
    uint32_t left = remaining_.load(std::memory_order_relaxed);
    while (left && !remaining_.compare_exchange_weak(left, left - 1, std::memory_order_relaxed)) {
    }
    return left != 0;
  }

 private:
  std::atomic<uint32_t> remaining_{0};
};

}  // namespace topspin4::load_flash
