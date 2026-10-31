#include <catch_amalgamated.hpp>

#include "load_flash_hold.h"

using topspin4::load_flash::Hold;

TEST_CASE("load flash hold skips nothing until a loading screen ends", "[load_flash]") {
  Hold hold;
  CHECK_FALSE(hold.TakePresent());
  CHECK_FALSE(hold.TakePresent());
}

TEST_CASE("load flash hold skips the armed presents, then lets them through", "[load_flash]") {
  Hold hold;
  hold.Arm(3);
  CHECK(hold.TakePresent());
  CHECK(hold.TakePresent());
  CHECK(hold.TakePresent());
  CHECK_FALSE(hold.TakePresent());
  CHECK_FALSE(hold.TakePresent());
}

TEST_CASE("load flash hold armed with zero (the fix off) skips nothing", "[load_flash]") {
  Hold hold;
  hold.Arm(0);
  CHECK_FALSE(hold.TakePresent());
}

TEST_CASE("load flash hold re-armed by the next loading screen", "[load_flash]") {
  Hold hold;
  hold.Arm(1);
  CHECK(hold.TakePresent());
  CHECK_FALSE(hold.TakePresent());
  hold.Arm(2);
  CHECK(hold.TakePresent());
  CHECK(hold.TakePresent());
  CHECK_FALSE(hold.TakePresent());
}
