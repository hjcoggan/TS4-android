// src/user_paths.h: the portable install. Every default folder is resolved from the folder that
// holds topspin4.exe, as the Launcher resolves its own from its folder
// (launcher/src/Launcher.Core/UserDirectories.cs). ADR-0009.

#include <catch_amalgamated.hpp>

#include <fstream>
#include <optional>
#include <string>

#include "user_paths.h"

namespace {

namespace fs = std::filesystem;
namespace user_paths = topspin4::user_paths;

}  // namespace

TEST_CASE("Saves and settings, the prepared data and the shader cache default to the exe folder") {
  const fs::path exe_folder = LR"(D:\Games\TopSpin4)";

  CHECK(user_paths::UserDataRootIn(exe_folder) == fs::path(LR"(D:\Games\TopSpin4\userdata)"));
  CHECK(user_paths::PreparedDataRootIn(exe_folder) == exe_folder);
  CHECK(user_paths::CacheRootIn(exe_folder) == fs::path(LR"(D:\Games\TopSpin4\cache)"));
  CHECK(user_paths::kUserDataFolder == "userdata");
}

TEST_CASE("Under Wine the roots stay beside the exe, on the Linux user's drive, not in the prefix") {
  // Wine reports the exe folder as a Z: path; nothing is looked up in the prefix or under XDG.
  const fs::path exe_folder = LR"(Z:\home\deck\games\TopSpin4)";

  CHECK(user_paths::UserDataRootIn(exe_folder) == fs::path(LR"(Z:\home\deck\games\TopSpin4\userdata)"));
  CHECK(user_paths::PreparedDataRootIn(exe_folder) == exe_folder);
  CHECK(user_paths::CacheRootIn(exe_folder) == fs::path(LR"(Z:\home\deck\games\TopSpin4\cache)"));
}

TEST_CASE("A folder that can be written to is accepted and one that cannot is refused") {
  const fs::path root = fs::temp_directory_path() / "ts4-writable-test";
  fs::remove_all(root);
  fs::create_directories(root);
  CHECK(user_paths::IsWritableFolder(root));
  // Made on the way, as the game makes userdata on first run.
  CHECK(user_paths::IsWritableFolder(root / "not-yet-made"));
  CHECK(fs::is_directory(root / "not-yet-made"));
  // A folder under a file cannot exist, so cannot be written to.
  std::ofstream(root / "a-file") << "x";
  CHECK_FALSE(user_paths::IsWritableFolder(root / "a-file" / "below"));
  CHECK(user_paths::kNotSetUpUnwritable == "folder-not-writable");
  std::error_code error;
  fs::remove_all(root, error);
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
  const fs::path default_cache = LR"(E:\Install\cache)";

  CHECK(CacheRoot(std::nullopt, default_cache) == default_cache);
  CHECK(CacheRoot(fs::path(LR"(D:\Games\TopSpin4)"), default_cache) ==
        fs::path(LR"(D:\Games\TopSpin4\cache)"));
  // The Launcher's move carries the folder of this name (Preparation.ShaderCacheFolderName).
  CHECK(topspin4::user_paths::kCacheFolder == "cache");
}

TEST_CASE("A location file in userdata moves the prepared data and the cache off the exe folder") {
  Places places;
  const fs::path exe_folder = places.root / "install";
  const fs::path moved = places.root / "moved";
  Places::Finished(exe_folder / "gamedata");
  Places::Finished(moved / "gamedata");
  const auto user_data = user_paths::UserDataRootIn(exe_folder);
  const auto location_file = user_data / std::string(user_paths::kLocationFileName);

  // No location file: the exe folder holds both.
  auto recorded = ReadLocationFile(location_file);
  CHECK_FALSE(recorded);
  CHECK(PreparedFolder({}, recorded, user_paths::PreparedDataRootIn(exe_folder), "gamedata") ==
        exe_folder / "gamedata");
  CHECK(user_paths::CacheRoot(recorded, user_paths::CacheRootIn(exe_folder)) == exe_folder / "cache");

  // The Launcher records the place in userdata; the game then reads both from there.
  fs::create_directories(user_data);
  std::ofstream(location_file, std::ios::binary) << moved.string() << "\n";
  recorded = ReadLocationFile(location_file);
  REQUIRE(recorded);
  CHECK(PreparedFolder({}, recorded, user_paths::PreparedDataRootIn(exe_folder), "gamedata") ==
        moved / "gamedata");
  CHECK(user_paths::CacheRoot(recorded, user_paths::CacheRootIn(exe_folder)) == moved / "cache");
}

TEST_CASE("Folders given on the command line win over the exe folder and the location file") {
  Places places;
  const fs::path exe_folder = places.root / "install";
  Places::Finished(exe_folder / "gamedata");
  Places::Finished(exe_folder / "updatedata");

  CHECK(PreparedFolder(places.command_line, std::nullopt, user_paths::PreparedDataRootIn(exe_folder),
                       "gamedata") == places.command_line);
  CHECK_FALSE(SetupNeededReason({}, {}, std::nullopt, user_paths::PreparedDataRootIn(exe_folder)));
  CHECK_FALSE(SetupNeededReason(places.command_line, places.root / "dev" / "updatedata", std::nullopt,
                                places.root / "elsewhere"));
}
