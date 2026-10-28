// native/clear_plan.h: which guest clears cover their whole target, so Native clears them as the
// pass's load operation (a fast clear) instead of drawing the clear over every sample.

#include <catch_amalgamated.hpp>

#include "native/clear_plan.h"

using topspin4::native::ClearRect;

TEST_CASE("a clear of the guest's whole target covers the scaled target") {
  const int32_t rect[4] = {0, 0, 1280, 720};
  const ClearRect r = ClearRect::Of(rect, 4, 5120, 2880);
  CHECK(r.covers_target);
  CHECK(r.width == 5120);
  CHECK(r.height == 2880);
}

TEST_CASE("a clear past the target's edge still covers it") {
  const int32_t rect[4] = {0, 0, 1296, 736};  // the guest's tile-padded surface
  CHECK(ClearRect::Of(rect, 1, 1280, 720).covers_target);
}

TEST_CASE("a clear of part of the target does not") {
  const int32_t rect[4] = {0, 0, 640, 720};
  const ClearRect r = ClearRect::Of(rect, 2, 2560, 1440);
  CHECK_FALSE(r.covers_target);
  CHECK(r.x == 0);
  CHECK(r.width == 1280);
  CHECK(r.height == 1440);
  const int32_t inset[4] = {8, 0, 1280, 720};
  CHECK_FALSE(ClearRect::Of(inset, 1, 1280, 720).covers_target);
}

TEST_CASE("an empty or negative rectangle clears the whole target, as before") {
  const int32_t empty[4] = {0, 0, 0, 0};
  CHECK(ClearRect::Of(empty, 1, 1280, 720).covers_target);
  const int32_t negative[4] = {-4, 0, 1280, 720};
  CHECK(ClearRect::Of(negative, 1, 1280, 720).covers_target);
}
