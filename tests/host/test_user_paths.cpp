// src/user_paths.h: the per-user folder for saves and settings, resolved as the launcher
// resolves its own (launcher/src/Launcher.Core/UserDirectories.cs).

#include <catch_amalgamated.hpp>

#include <map>
#include <optional>
#include <string>

#include "user_paths.h"

using topspin4::user_paths::AppFolder;

namespace {

const std::filesystem::path kAppData = LR"(C:\Users\player\AppData\Roaming)";

auto Environment(std::map<std::string, std::string> variables) {
  return [variables](std::string_view name) -> std::optional<std::string> {
    auto found = variables.find(std::string(name));
    if (found == variables.end()) return std::nullopt;
    return found->second;
  };
}

}  // namespace

TEST_CASE("On Windows saves and settings go under the roaming app data folder") {
  CHECK(AppFolder(Environment({{"HOME", "/home/deck"}}), false, kAppData) ==
        std::filesystem::path(LR"(C:\Users\player\AppData\Roaming\TopSpin4Recomp)"));
}

TEST_CASE("Under Proton saves and settings go under the Linux user's XDG data folder") {
  CHECK(AppFolder(Environment({{"HOME", "/home/deck"}}), true, kAppData) ==
        std::filesystem::path(LR"(Z:\home\deck\.local\share\TopSpin4Recomp)"));
  CHECK(AppFolder(Environment({{"HOME", "/home/deck"}, {"XDG_DATA_HOME", "/run/media/sd/data/"}}),
                     true, kAppData) ==
        std::filesystem::path(LR"(Z:\run\media\sd\data\TopSpin4Recomp)"));
}

TEST_CASE("Under Wine without a Linux home they stay in the prefix") {
  CHECK(AppFolder(Environment({{"XDG_DATA_HOME", "relative/is/ignored"}}), true, kAppData) ==
        std::filesystem::path(LR"(C:\Users\player\AppData\Roaming\TopSpin4Recomp)"));
}
