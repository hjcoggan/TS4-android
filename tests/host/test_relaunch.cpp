// src/relaunch_plan.h: going back to the title screen relaunches the game.

#include <catch_amalgamated.hpp>

#include "relaunch_plan.h"

using namespace topspin4::relaunch;

TEST_CASE("Only a launch of the game's own executable is a relaunch", "[relaunch]") {
  CHECK(IsOwnTitle("game:\\default.xex"));
  CHECK(IsOwnTitle("\\Device\\Harddisk0\\Partition1\\default.xex"));
  CHECK(IsOwnTitle("default.xex"));
  CHECK(IsOwnTitle("GAME:\\DEFAULT.XEX"));
  CHECK_FALSE(IsOwnTitle(""));  // the dashboard
  CHECK_FALSE(IsOwnTitle("game:\\notdefault.xex"));
  CHECK_FALSE(IsOwnTitle("game:\\other.xex"));
}

TEST_CASE("The new copy keeps this run's arguments, with the hand-over settings replacing theirs",
          "[relaunch]") {
  const std::vector<std::wstring> arguments = {
      L"--user_data_root=D:\\userdata", L"--ts4_launch_data=C:\\old.bin",
      L"--ts4_input_script=C:\\script.txt", L"--fullscreen=true", L"--headless",
      L"--ts4_launch_database=keep"};
  const std::vector<std::wstring> settings = {L"--ts4_launch_data=C:\\new.bin",
                                              L"--window_hidden=true", L"--fullscreen=false"};

  CHECK(ChildArguments(arguments, settings) ==
        std::vector<std::wstring>{L"--user_data_root=D:\\userdata", L"--headless",
                                  L"--ts4_launch_database=keep", L"--ts4_launch_data=C:\\new.bin",
                                  L"--window_hidden=true", L"--fullscreen=false"});
}

TEST_CASE("The old window's rectangle reads back, and a malformed one is refused", "[relaunch]") {
  Rect rect;
  REQUIRE(ParseRect("-8,120,1296,759", rect));
  CHECK(rect.x == -8);
  CHECK(rect.y == 120);
  CHECK(rect.width == 1296);
  CHECK(rect.height == 759);

  for (const char* bad : {"", "1,2,3", "1,2,3,4,5", "1,2,0,4", "a,b,c,d", "1, 2,3,4"}) {
    CHECK_FALSE(ParseRect(bad, rect));
  }
}

TEST_CASE("A test's script for the relaunched copy becomes its scripted pad", "[relaunch]") {
  const std::vector<std::wstring> arguments = {L"--ts4_input_script=C:\\first.txt",
                                               L"--ts4_relaunch_input_script=C:\\second.txt"};
  CHECK(ChildArguments(arguments, {}) ==
        std::vector<std::wstring>{L"--ts4_input_script=C:\\second.txt"});
}
