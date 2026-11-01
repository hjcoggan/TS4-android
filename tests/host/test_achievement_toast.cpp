// src/achievement_toast_timeline.h: the toast's motion.

#include <catch_amalgamated.hpp>

#include "achievement_toast_timeline.h"

using namespace topspin4::achievement_toast;

TEST_CASE("The toast is hidden before it starts and after it ends", "[achievement_toast]") {
  for (float t : {-1.0f, kDuration, kDuration + 1}) {
    const Frame f = At(t);
    CHECK(f.alpha == 0);
    CHECK(f.badge_scale == 0);
    CHECK(f.open == 0);
    CHECK(f.text_alpha == 0);
  }
}

TEST_CASE("The badge pops in first, then the capsule opens, then the text shows", "[achievement_toast]") {
  const Frame early = At(0.1f);
  CHECK(early.badge_scale > 0);
  CHECK(early.open == 0);
  CHECK(early.text_alpha == 0);

  const Frame opening = At((kOpenStart + kOpenEnd) / 2);
  CHECK(opening.open > 0);
  CHECK(opening.open < 1);

  const Frame held = At(2.0f);
  CHECK(held.alpha == 1);
  CHECK(held.badge_scale == Catch::Approx(1));
  CHECK(held.open == 1);
  CHECK(held.text_alpha == 1);
}

TEST_CASE("The pop overshoots a little and settles", "[achievement_toast]") {
  float peak = 0;
  for (float t = 0; t <= kBadgeIn; t += 0.01f) peak = std::max(peak, At(t).badge_scale);
  CHECK(peak > 1.0f);
  CHECK(peak < 1.2f);
}

TEST_CASE("At the end the text goes, the capsule closes, then the badge", "[achievement_toast]") {
  const Frame closing = At((kCloseStart + kCloseEnd) / 2);
  CHECK(closing.text_alpha == 0);
  CHECK(closing.open < 1);
  CHECK(closing.badge_scale == Catch::Approx(1));

  const Frame last = At((kCloseEnd + kBadgeOutEnd) / 2);
  CHECK(last.open == 0);
  CHECK(last.badge_scale < 1);
  CHECK(last.alpha < 1);
}

// src/achievement_toast_queue.h: the toasts waiting to show.

#include "achievement_toast_queue.h"

namespace {

std::chrono::steady_clock::time_point Seconds(float s) {
  return std::chrono::steady_clock::time_point(
      std::chrono::duration_cast<std::chrono::steady_clock::duration>(std::chrono::duration<float>(s)));
}

}  // namespace

TEST_CASE("Queued toasts show one at a time, each for its full time", "[achievement_toast]") {
  Queue<int> queue(6.0f);
  CHECK_FALSE(queue.At(Seconds(0)));

  queue.Push(1);
  queue.Push(2);
  auto first = queue.At(Seconds(10));
  REQUIRE(first);
  CHECK(first->event == 1);
  CHECK(first->started);
  CHECK(first->age == 0);

  auto later = queue.At(Seconds(13));
  REQUIRE(later);
  CHECK(later->event == 1);
  CHECK_FALSE(later->started);
  CHECK(later->age == Catch::Approx(3));

  // The second starts its own time on the frame after the first's ends.
  auto second = queue.At(Seconds(16.5f));
  REQUIRE(second);
  CHECK(second->event == 2);
  CHECK(second->started);
  CHECK(second->age == 0);

  CHECK_FALSE(queue.At(Seconds(23)));
}
