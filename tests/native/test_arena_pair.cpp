// native/arena_pair.h: the geometry cache switches between two arenas allocated once, instead
// of freeing a full 128 MB arena and allocating another: that allocation, on Windows, delayed the
// next flip by 3-4 ms, the last gameplay stutters at 3x Resolution scale with 8x MSAA.

#include <catch_amalgamated.hpp>

#include "native/arena_pair.h"

using topspin4::native::ArenaPair;

TEST_CASE("a full arena switches to the other one when no frame in flight can still use it") {
  ArenaPair pair(3);
  CHECK(pair.Current() == 0);
  CHECK(pair.Switch(1000));
  CHECK(pair.Current() == 1);
  CHECK(pair.Switch(2000));
  CHECK(pair.Current() == 0);
}

TEST_CASE("a second switch within the frames in flight is refused") {
  // The other arena may still be read by a frame on the GPU: the caller replaces it instead.
  ArenaPair pair(3);
  CHECK(pair.Switch(1000));
  CHECK_FALSE(pair.Switch(1002));
  CHECK(pair.Current() == 1);
  CHECK(pair.Switch(1003));
  CHECK(pair.Current() == 0);
}
