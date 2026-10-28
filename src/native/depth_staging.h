// Whether the depth resolve staging buffer still holds a depth target's current contents. Top
// Spin 4 resolves the same depth twice in a row each frame (to two textures); each resolve made a
// single-sample twin of the MSAA target and copied it to the staging buffer, ~1.6 ms of GPU time
// at 4x Resolution scale with 8x MSAA (docs/research/native-draw-cost.md). With no write to the
// target in between, the second resolve copies the staging buffer to its texture only. A target
// is named by its address and a count of the passes begun with it attached (each may write it).
// Pure.

#pragma once

#include <cstdint>

namespace topspin4::native {

class DepthStaging {
 public:
  bool Holds(const void* target, uint64_t writes) const {
    return target_ && target_ == target && writes_ == writes;
  }
  void Copied(const void* target, uint64_t writes) {
    target_ = target;
    writes_ = writes;
  }
  // A new (larger) staging buffer, or the target's image was replaced.
  void Forget() { target_ = nullptr; }

 private:
  const void* target_ = nullptr;
  uint64_t writes_ = 0;
};

}  // namespace topspin4::native
