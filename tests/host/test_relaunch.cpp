// src/relaunch_plan.h: going back to the title screen relaunches the game.

#include <catch_amalgamated.hpp>

#include "relaunch_plan.h"

using namespace topspin4::relaunch;

TEST_CASE("Only a launch of the game's own executable is a relaunch", "[relaunch]") {
  CHECK(IsOwnTitle("game:\\default.xex"));
  CHECK(IsOwnTitle("default.xex"));
  CHECK(IsOwnTitle("GAME:\\DEFAULT.XEX"));
  CHECK_FALSE(IsOwnTitle(""));  // the dashboard
  CHECK_FALSE(IsOwnTitle("game:\\notdefault.xex"));
  CHECK_FALSE(IsOwnTitle("game:\\other.xex"));
}

TEST_CASE("The new copy keeps this run's arguments and gets the launch data file",
          "[relaunch]") {
  const std::vector<std::wstring> arguments = {
      L"--user_data_root=D:\\userdata", L"--ts4_launch_data=C:\\old.bin",
      L"--ts4_input_script=C:\\script.txt", L"--headless", L"--ts4_launch_database=keep"};

  CHECK(ChildArguments(arguments, L"C:\\new.bin") ==
        std::vector<std::wstring>{L"--user_data_root=D:\\userdata", L"--headless",
                                  L"--ts4_launch_database=keep", L"--ts4_launch_data=C:\\new.bin"});
}
