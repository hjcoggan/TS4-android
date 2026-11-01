// src/cutaway_rate_rules.h: 60 fps cutaways change only a cutaway's interval and world step.
// The game's literals are rounded (0.03333, 0.01666), so they match 1/30 and 1/60 to 0.1 %.

#include <catch_amalgamated.hpp>

#include <bit>
#include <cstdint>

#include "cutaway_rate_rules.h"

using namespace topspin4::cutaway_rate;

TEST_CASE("the literals are the game's 1/30 and 1/60 steps") {
  CHECK(std::bit_cast<float>(kCutawayStepBits) == Catch::Approx(1.0 / 30).epsilon(1e-3));
  CHECK(std::bit_cast<float>(kGameplayStepBits) == Catch::Approx(1.0 / 60).epsilon(1e-3));
}

TEST_CASE("off keeps the game's interval and step") {
  CHECK(PresentInterval(2, kCutawayMode, false) == 2);
  CHECK(StepBits(kCutawayStepBits, kCutawayMode, false) == kCutawayStepBits);
}

TEST_CASE("on runs a cutaway at interval 1 with the 60 fps step") {
  CHECK(PresentInterval(2, kCutawayMode, true) == 1);
  CHECK(StepBits(kCutawayStepBits, kCutawayMode, true) == kGameplayStepBits);
}

TEST_CASE("on leaves 60 fps scenes and other steps alone") {
  CHECK(PresentInterval(1, 0, true) == 1);
  CHECK(StepBits(kGameplayStepBits, 0, true) == kGameplayStepBits);
  // A step that is not the cutaway literal, even in mode 1, passes unchanged.
  const uint32_t other = std::bit_cast<uint32_t>(0.5f);
  CHECK(StepBits(other, kCutawayMode, true) == other);
}

TEST_CASE("once per frame admits an instance's first run in a frame only") {
  OncePerFrame once;
  CHECK(once.Admit(10, 0xb1e80600));
  CHECK(once.Admit(10, 0xb1e7fd80));
  CHECK_FALSE(once.Admit(10, 0xb1e80600));
  CHECK_FALSE(once.Admit(10, 0xb1e7fd80));
}

TEST_CASE("once per frame starts over on a new frame number") {
  OncePerFrame once;
  CHECK(once.Admit(10, 0xb1e80600));
  CHECK(once.Admit(11, 0xb1e80600));
  CHECK_FALSE(once.Admit(11, 0xb1e80600));
  // The game frame keeps stepping while swaps stop (the match load): any change is a new frame.
  CHECK(once.Admit(9, 0xb1e80600));
}

TEST_CASE("the face player runs every frame unless it is a cutaway at 60") {
  for (uint32_t frame : {0u, 1u, 2u, 3u}) {
    CHECK(FaceStepFor(kCutawayMode, false, frame, false) == FaceStep::kRun);
    CHECK(FaceStepFor(0, true, frame, true) == FaceStep::kRun);
  }
}

TEST_CASE("at 60 the face player holds on odd game frames") {
  CHECK(FaceStepFor(kCutawayMode, true, 100, false) == FaceStep::kRun);
  CHECK(FaceStepFor(kCutawayMode, true, 101, false) == FaceStep::kHold);
  CHECK(FaceStepFor(kCutawayMode, true, 102, true) == FaceStep::kRun);
  // A slot at frame 0 cannot be wound back: that hold frame skips the call.
  CHECK(FaceStepFor(kCutawayMode, true, 103, true) == FaceStep::kSkip);
}
