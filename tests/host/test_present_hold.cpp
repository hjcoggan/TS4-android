// rex/ui/present_hold.h (patches/rexglue 0042, 0044): on a variable-refresh display a frame is
// shown when its present arrives, so the presenter presents each frame once its GPU work is done,
// at a hold point after the guest vblank that is the same for every frame. A frame finishing
// after the hold is shown late (a stutter); the hold rises to cover it at once and comes down
// slowly, up to a margin before the next vblank.

#include <catch_amalgamated.hpp>

#include <cstdint>
#include <random>
#include <vector>

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

TEST_CASE("a frame finishing after the hold raises it to cover that frame at once") {
  PresentHold hold;
  hold.Completed(6000, kOffsetUs, kPeriodUs);
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) >= 6000 + PresentHold::kMarginUs);
  hold.Completed(11000, kOffsetUs, kPeriodUs);
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) >= 11000 + PresentHold::kMarginUs);
}

TEST_CASE("frames finishing within the period are late only as new records") {
  // At 3x Resolution scale with 8x MSAA most frames finished ~6 ms after the vblank and ~1 % up
  // to 9-12 ms: the percentile hold (p95 + 1 ms) left those late, the last gameplay stutters.
  std::mt19937 random(5);
  std::uniform_int_distribution<int64_t> usual(5500, 7000), slow(7000, 12500);
  std::vector<int64_t> done;
  for (int i = 0; i < 3600; ++i) done.push_back(i % 90 == 0 ? slow(random) : usual(random));
  PresentHold hold;
  int late = 0;
  for (size_t i = 0; i < done.size(); ++i) {
    if (i >= 600) late += done[i] > hold.HoldUs(kOffsetUs, kPeriodUs);
    hold.Completed(done[i], kOffsetUs, kPeriodUs);
  }
  CHECK(late <= 3);
}

TEST_CASE("it comes down slowly in a lighter scene, and never below the offset") {
  PresentHold hold;
  hold.Completed(11000, kOffsetUs, kPeriodUs);
  const int64_t high = hold.HoldUs(kOffsetUs, kPeriodUs);
  hold.Completed(6000, kOffsetUs, kPeriodUs);
  // One light frame moves it by a fraction of a millisecond, so presents shift imperceptibly.
  CHECK(high - hold.HoldUs(kOffsetUs, kPeriodUs) <= 100);
  for (int i = 0; i < 10000; ++i) hold.Completed(1000, kOffsetUs, kPeriodUs);
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) == kOffsetUs);
}

TEST_CASE("a frame finishing past the next vblank raises nothing") {
  // The pacing thread holds, presents and paints the next frame in turn: a hold past the next
  // vblank delayed every later frame. A frame that late is late under any hold (a load).
  PresentHold hold;
  hold.Completed(6000, kOffsetUs, kPeriodUs);
  const int64_t before = hold.HoldUs(kOffsetUs, kPeriodUs);
  hold.Completed(17500, kOffsetUs, kPeriodUs);
  hold.Completed(41000, kOffsetUs, kPeriodUs);
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) <= before);
}

TEST_CASE("it never holds past the next vblank less a margin") {
  PresentHold hold;
  hold.Completed(kPeriodUs - 1500, kOffsetUs, kPeriodUs);
  CHECK(hold.HoldUs(kOffsetUs, kPeriodUs) == kPeriodUs - PresentHold::kMarginUs);
}
