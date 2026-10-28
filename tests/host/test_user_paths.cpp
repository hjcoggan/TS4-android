// src/user_paths.h: the per-user folder for saves and settings, resolved as the launcher
// resolves its own (launcher/src/Launcher.Core/UserDirectories.cs).

#include <catch_amalgamated.hpp>

#include <fstream>
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

TEST_CASE("The prepared data folders are named as the Launcher names them") {
  // launcher/src/Launcher.Core/Preparation.cs: GameDataFolderName and UpdateDataFolderName.
  CHECK(topspin4::user_paths::kGameDataFolder == "gamedata");
  CHECK(topspin4::user_paths::kUpdateDataFolder == "updatedata");
}

namespace {

namespace fs = std::filesystem;
using topspin4::user_paths::PreparedFolder;
using topspin4::user_paths::ReadLocationFile;

// A scratch folder with a default prepared-data root and another place, each optionally holding
// a prepared "gamedata" folder.
struct Places {
  fs::path root = fs::temp_directory_path() / "ts4-user-paths-test";
  fs::path default_root = root / "default";
  fs::path other_root = root / "other";
  fs::path command_line = root / "dev" / "gamedata";

  Places() { fs::remove_all(root); fs::create_directories(root); }
  ~Places() { std::error_code error; fs::remove_all(root, error); }
  // A folder the Launcher finished: its completion marker is there (PreparedFolder.MarkerFileName).
  static void Finished(const fs::path& folder) {
    fs::create_directories(folder);
    std::ofstream(folder / ".topspin4-prepared", std::ios::binary) << "manifest_version=1\n";
  }
  static void Prepared(const fs::path& place) { Finished(place / "gamedata"); }
};

}  // namespace

TEST_CASE("The prepared-data lookup prefers the command line") {
  Places places;
  Places::Prepared(places.default_root);
  Places::Prepared(places.other_root);

  CHECK(PreparedFolder(places.command_line, places.other_root, places.default_root, "gamedata") ==
        places.command_line);
}

TEST_CASE("Without a command line the location file's place comes before the default folder") {
  Places places;
  Places::Prepared(places.default_root);
  Places::Prepared(places.other_root);

  CHECK(PreparedFolder({}, places.other_root, places.default_root, "gamedata") ==
        places.other_root / "gamedata");
}

TEST_CASE("Without a command line or a location file the default folder is used") {
  Places places;
  Places::Prepared(places.default_root);

  CHECK(PreparedFolder({}, std::nullopt, places.default_root, "gamedata") ==
        places.default_root / "gamedata");
}

TEST_CASE("A place with no prepared folder gives nothing, so setup is seen to be missing") {
  Places places;
  Places::Prepared(places.default_root);

  // The recorded place is the one the Launcher uses, so a stale default copy is not picked up.
  CHECK(PreparedFolder({}, places.other_root, places.default_root, "gamedata").empty());
  CHECK(PreparedFolder({}, std::nullopt, places.other_root, "gamedata").empty());
}

TEST_CASE("A prepared folder without the Launcher's completion marker is not set up") {
  using topspin4::user_paths::SetupNeededReason;
  Places places;
  fs::create_directories(places.default_root / "gamedata");
  fs::create_directories(places.default_root / "updatedata");

  CHECK(PreparedFolder({}, std::nullopt, places.default_root, "gamedata").empty());
  CHECK(SetupNeededReason({}, {}, std::nullopt, places.default_root) == "disc-and-title-update");

  Places::Finished(places.default_root / "gamedata");
  CHECK(SetupNeededReason({}, {}, std::nullopt, places.default_root) == "title-update");
  // A marker that does not name a manifest version is not the Launcher's.
  std::ofstream(places.default_root / "updatedata" / ".topspin4-prepared", std::ios::binary) << "junk";
  CHECK(SetupNeededReason({}, {}, std::nullopt, places.default_root) == "title-update");
  CHECK(topspin4::user_paths::kPreparedMarkerFile == ".topspin4-prepared");
}

TEST_CASE("A location file in UTF-8 gives a place whose name is outside the ANSI code page") {
  Places places;
  fs::create_directories(places.root);
  const auto file = places.root / std::string(topspin4::user_paths::kLocationFileName);
  // The Launcher writes UTF-8 (GameFilesLocation.cs). Bytes spelled out so the source encoding
  // does not matter: "D:\Spiele\a-umlaut" then a Japanese "ni" character (U+65E5).
  const std::string utf8 = "D:\\Spiele\\\xC3\xA4\xE6\x97\xA5\n";
  std::ofstream(file, std::ios::binary) << utf8;

  const auto place = ReadLocationFile(file);

  REQUIRE(place);
  CHECK(*place == fs::path(L"D:\\Spiele\\\u00E4\u65E5"));
}

TEST_CASE("The location file holds one absolute path, which the Launcher writes") {
  Places places;
  fs::create_directories(places.root);
  const auto file = places.root / std::string(topspin4::user_paths::kLocationFileName);

  CHECK_FALSE(ReadLocationFile(file));

  const auto write = [&](const std::string& text) { std::ofstream(file, std::ios::binary) << text; };
  write(R"(D:\Games\TopSpin4)" "\n");
  REQUIRE(ReadLocationFile(file));
  CHECK(*ReadLocationFile(file) == fs::path(LR"(D:\Games\TopSpin4)"));
  write("\r\n  " R"(D:\Games\TopSpin4)" "  \r\nignored\r\n");
  CHECK(*ReadLocationFile(file) == fs::path(LR"(D:\Games\TopSpin4)"));
  write(R"(relative\place)" "\n");
  CHECK_FALSE(ReadLocationFile(file));
  write("");
  CHECK_FALSE(ReadLocationFile(file));
  CHECK(topspin4::user_paths::kLocationFileName == "game-files-location.txt");
}

namespace {

using topspin4::user_paths::SetupNeededReason;

}  // namespace

TEST_CASE("Started with nothing prepared and no folders given, the reason is both") {
  Places places;

  const auto reason = SetupNeededReason({}, {}, std::nullopt, places.default_root);

  REQUIRE(reason);
  CHECK(*reason == "disc-and-title-update");
}

TEST_CASE("A missing disc or a missing TU1 each name themselves as the reason") {
  Places places;
  Places::Finished(places.default_root / "gamedata");
  CHECK(SetupNeededReason({}, {}, std::nullopt, places.default_root) == "title-update");

  fs::remove_all(places.default_root);
  Places::Finished(places.default_root / "updatedata");
  CHECK(SetupNeededReason({}, {}, std::nullopt, places.default_root) == "disc");
}

TEST_CASE("With both prepared folders found there is nothing to do") {
  Places places;
  Places::Finished(places.default_root / "gamedata");
  Places::Finished(places.default_root / "updatedata");
  fs::create_directories(places.other_root);

  CHECK_FALSE(SetupNeededReason({}, {}, std::nullopt, places.default_root));
  // The recorded place is the one used, so an empty one is not set up even beside a full default.
  CHECK(SetupNeededReason({}, {}, places.other_root, places.default_root) == "disc-and-title-update");
}

TEST_CASE("Folders given on the command line mean the developer's run goes ahead as before") {
  Places places;

  CHECK_FALSE(SetupNeededReason(places.command_line, places.root / "dev" / "updatedata", std::nullopt,
                                places.default_root));
}

TEST_CASE("One folder on the command line leaves the other one to be found") {
  Places places;

  CHECK(SetupNeededReason(places.command_line, {}, std::nullopt, places.default_root) == "title-update");
  CHECK(SetupNeededReason({}, places.command_line, std::nullopt, places.default_root) == "disc");
}

TEST_CASE("The shader cache follows the recorded place, beside the prepared data") {
  using topspin4::user_paths::CacheRoot;
  const fs::path default_cache = LR"(C:\Users\player\AppData\Local\TopSpin4Recomp\cache)";

  CHECK(CacheRoot(std::nullopt, default_cache) == default_cache);
  CHECK(CacheRoot(fs::path(LR"(D:\Games\TopSpin4)"), default_cache) ==
        fs::path(LR"(D:\Games\TopSpin4\cache)"));
  // The Launcher's move carries the folder of this name (Preparation.ShaderCacheFolderName).
  CHECK(topspin4::user_paths::kCacheFolder == "cache");
}
