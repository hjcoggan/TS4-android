// 60 fps cutaways' rules (cutaway_rate.h): what a frame's present interval and world step become,
// which scheduler runs a frame keeps, and when the face player holds its frame.
// Apart from the sink, so the host tests can include it.

#pragma once

#include <algorithm>
#include <cstdint>
#include <vector>

namespace topspin4::cutaway_rate {

inline constexpr uint32_t kCutawayMode = 1;
// The world step's two literals, as float bits.
inline constexpr uint32_t kCutawayStepBits = 0x3d08850a;   // 0.03333f
inline constexpr uint32_t kGameplayStepBits = 0x3c887a8d;  // 0.01666f

// Whether the frame is a cutaway at 60: the only frames these rules change.
inline bool SixtyCutaway(uint32_t mode, bool sixty) { return sixty && mode == kCutawayMode; }

// The present interval the frame uses: the game's own, or 1 for a cutaway at 60.
inline uint32_t PresentInterval(uint32_t game_interval, uint32_t mode, bool sixty) {
  return SixtyCutaway(mode, sixty) ? 1 : game_interval;
}

// The world step the frame uses (float bits): the game's own, or the 60 fps step for a cutaway
// at 60. Only the cutaway literal is replaced; a step from anywhere else passes unchanged.
inline uint32_t StepBits(uint32_t step_bits, uint32_t mode, bool sixty) {
  return SixtyCutaway(mode, sixty) && step_bits == kCutawayStepBits ? kGameplayStepBits
                                                                     : step_bits;
}

// In a cutaway the game reaches its cutaway services twice per frame (a scheduler list with two
// parents), so the cutaway logic steps at 60 Hz while mode 1 presents at 30. At 60 each
// scheduler or list runs once per game frame: the first run of an instance in a frame is
// admitted, a second one is not. A new frame number starts a new frame.
class OncePerFrame {
 public:
  bool Admit(uint32_t frame, uint32_t instance) {
    if (frame != frame_) {
      frame_ = frame;
      ran_.clear();
    }
    if (std::find(ran_.begin(), ran_.end(), instance) != ran_.end()) return false;
    ran_.push_back(instance);
    return true;
  }

 private:
  uint32_t frame_ = ~0u;
  std::vector<uint32_t> ran_;
};

// The face player steps its clip one frame per game frame, which is 30 Hz in a cutaway at 30.
// At 60 it holds on odd game frames: the hook winds each playing slot's frame back by one
// before the call, which adds one. A slot at frame 0 cannot be wound back without the player
// taking it as the clip's end, so a frame where one is at 0 skips the call instead.
enum class FaceStep { kRun, kHold, kSkip };

inline FaceStep FaceStepFor(uint32_t mode, bool sixty, uint32_t game_frame, bool slot_at_zero) {
  if (!SixtyCutaway(mode, sixty) || (game_frame & 1) == 0) return FaceStep::kRun;
  return slot_at_zero ? FaceStep::kSkip : FaceStep::kHold;
}

}  // namespace topspin4::cutaway_rate
