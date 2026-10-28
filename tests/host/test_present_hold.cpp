// rex/ui/present_hold.h (patches/rexglue 0042): on a variable-refresh display a frame is shown
// when its GPU work completes, so frames finishing at different times after the guest vblank
// reached the screen unevenly (7.8-14 % of display changes off the 60 Hz step at 4x with 8x
// MSAA). The presenter now presents each frame once it is done, at a hold point after the vblank
// that this picks from the completion times it has seen.

#include <catch_amalgamated.hpp>

#include <cstdint>
#include <random>

#include <rex/ui/present_hold.h>

using rex::ui::pacing::PresentHold;

namespace {
constexpr int64_t kPeriodUs = 16667;
constexpr int64_t kOffsetUs = 5000;
}  // namespace

TEST_CASE("before it has seen frames it holds to the present offset") {
  PresentHold hold;
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) == kOffsetUs);
}

TEST_CASE("it holds past nearly every frame's completion, with a margin") {
  PresentHold hold;
  std::mt19937 random(3);
  // Completions 9-13 ms after the vblank, as measured at 4x with 8x MSAA.
  std::uniform_int_distribution<int64_t> done(9000, 13000);
  for (int i = 0; i < 300; ++i) hold.Completed(done(random));
  const int64_t chosen = hold.HoldUs(kOffsetUs, kPeriodUs);
  CHECK(chosen >= 13000);
  CHECK(chosen <= 14500);
}

TEST_CASE("an occasional very late frame does not move the hold to the end of the period") {
  PresentHold hold;
  for (int i = 0; i < 300; ++i) hold.Completed(i % 100 == 0 ? 30000 : 8000);
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) < 10500);
}

TEST_CASE("it never holds past the period less a millisecond, nor before the offset") {
  PresentHold heavy;
  for (int i = 0; i < 300; ++i) heavy.Completed(16500);
  CHECK(heavy.HoldUs(kOffsetUs, kPeriodUs) == kPeriodUs - 1000);
  PresentHold light;
  for (int i = 0; i < 300; ++i) light.Completed(1000);
  CHECK(light.HoldUs(kOffsetUs, kPeriodUs) == kOffsetUs);
}

TEST_CASE("it follows a scene that gets lighter") {
  PresentHold hold;
  for (int i = 0; i < 300; ++i) hold.Completed(13000);
  for (int i = 0; i < 300; ++i) hold.Completed(7000);
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) < 9000);
}
