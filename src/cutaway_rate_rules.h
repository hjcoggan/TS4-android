// 60 fps cutaways' rules (cutaway_rate.h): what a frame's present interval and world step become.
// Apart from the sink, so the host tests can include it.

#pragma once

#include <cstdint>

namespace topspin4::cutaway_rate {

inline constexpr uint32_t kCutawayMode = 1;
// The world step's two literals, as float bits.
inline constexpr uint32_t kCutawayStepBits = 0x3d08850a;   // 0.03333f
inline constexpr uint32_t kGameplayStepBits = 0x3c887a8d;  // 0.01666f

// The present interval the frame uses: the game's own, or 1 for a cutaway at 60.
inline uint32_t PresentInterval(uint32_t game_interval, uint32_t mode, bool sixty) {
  return sixty && mode == kCutawayMode ? 1 : game_interval;
}

// The world step the frame uses (float bits): the game's own, or the 60 fps step for a cutaway
// at 60. Only the cutaway literal is replaced; a step from anywhere else passes unchanged.
inline uint32_t StepBits(uint32_t step_bits, uint32_t mode, bool sixty) {
  return sixty && mode == kCutawayMode && step_bits == kCutawayStepBits ? kGameplayStepBits
                                                                        : step_bits;
}

}  // namespace topspin4::cutaway_rate
