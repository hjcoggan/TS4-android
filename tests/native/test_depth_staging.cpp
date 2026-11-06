// native/depth_staging.h: a depth resolve reuses the staging copy of the depth target when nothing
// has written the target since. Top Spin 4 resolves the same depth twice in a row each frame; at
// 4x Resolution scale with 8x MSAA each resolve cost ~1.6 ms of GPU time.

#include <catch_amalgamated.hpp>

#include "native/depth_staging.h"

using topspin4::native::DepthStaging;

TEST_CASE("the first resolve of a target copies it") {
  DepthStaging staging;
  int target;
  CHECK_FALSE(staging.Holds(&target, 7));
  staging.Copied(&target, 7);
  CHECK(staging.Holds(&target, 7));
}

TEST_CASE("a second resolve with no write between reuses the copy") {
  DepthStaging staging;
  int target;
  staging.Copied(&target, 7);
  CHECK(staging.Holds(&target, 7));
}

TEST_CASE("a write to the target, or another target, needs a new copy") {
  DepthStaging staging;
  int target, other;
  staging.Copied(&target, 7);
  CHECK_FALSE(staging.Holds(&target, 8));
  CHECK_FALSE(staging.Holds(&other, 7));
}

TEST_CASE("a new staging buffer holds nothing") {
  DepthStaging staging;
  int target;
  staging.Copied(&target, 7);
  staging.Forget();
  CHECK_FALSE(staging.Holds(&target, 7));
}
