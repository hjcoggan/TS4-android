// 60 fps cutaways' rules (cutaway_rate.h): what a frame's present interval and world step become,
// which scheduler is skipped, and when the face player holds its frame.
// Apart from the sink, so the host tests can include it.

#pragma once

#include <algorithm>
#include <cstdint>
#include <iterator>

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

// In a cutaway the game steps its cutaway logic twice per frame, so it runs at 60 Hz while mode 1
// presents at 30: a mode-1 scheduler, the extra step, runs the cutaway services a second time
// early in the frame, before the bone palette is composed; the normal pass runs them again late
// in the frame, after the services that read the pose. At 60 the extra step is skipped, so each
// service runs once and in the normal pass's order. (Keeping the extra step instead drew the
// hair one step off the head.) The extra step is known by its entries' ids (name hashes in the
// game's scheduler data, the same every run).
inline constexpr uint32_t kExtraStepIds[] = {0x7e3baa14, 0x4583a710, 0xd40a9431, 0xbbf63e79,
                                             0x6d4f45ae};
inline constexpr size_t kExtraStepEntries = std::size(kExtraStepIds);

inline bool IsExtraStep(const uint32_t* ids, size_t count) {
  return count == kExtraStepEntries && std::equal(ids, ids + count, kExtraStepIds);
}

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
