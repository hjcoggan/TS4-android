// The portable install (ADR-0009): every default folder is resolved from the folder that holds
// topspin4.exe, as the Launcher resolves its own from its folder
// (launcher/src/Launcher.Core/UserDirectories.cs). Nothing defaults to %APPDATA%, %LOCALAPPDATA% or
// the XDG data folder, on Windows or under Proton. The command line still wins
// (--user_data_root, --cache_root, --game_data_root, --update_data_root; the developer scripts pass
// them).

#pragma once

#include <filesystem>
#include <fstream>
#include <optional>
#include <string>
#include <string_view>

#include "native/host_paths.h"

namespace topspin4::user_paths {

// Saves, the Settings file, achievements, the disc art and the game files location file. The
// Launcher's UserDirectories.UserDataFolderName; keep the names equal.
inline constexpr std::string_view kUserDataFolder = "userdata";

inline std::filesystem::path UserDataRootIn(const std::filesystem::path& exe_folder) {
  return exe_folder / std::string(kUserDataFolder);
}

// Where the Launcher prepares the disc and TU1: the folders below sit directly in the exe folder.
inline std::filesystem::path PreparedDataRootIn(const std::filesystem::path& exe_folder) {
  return exe_folder;
}

// The folders for this process, from the folder that holds topspin4.exe.
std::filesystem::path ExecutableFolder();
std::filesystem::path DefaultUserDataRoot();
std::filesystem::path DefaultPreparedDataRoot();

// The folder can be written to: it is made if missing, and a file can be created in it and
// removed again. The portable install writes beside the exe, so a folder the game cannot write to
// (for example under Program Files) is refused, never replaced by a per-user one.
inline bool IsWritableFolder(const std::filesystem::path& folder) {
  std::error_code error;
  std::filesystem::create_directories(folder, error);
  if (!std::filesystem::is_directory(folder, error)) return false;
  const auto probe = folder / ".topspin4-write-test";
  {
    std::ofstream output(probe, std::ios::binary);
    if (!output.write("x", 1).flush()) return false;
  }
  std::filesystem::remove(probe, error);
  return true;
}

// The folders inside it, named as the Launcher names them (Preparation.GameDataFolderName and
// UpdateDataFolderName), mounted as game: and update:.
inline constexpr std::string_view kGameDataFolder = "gamedata";
inline constexpr std::string_view kUpdateDataFolder = "updatedata";

// The location file the Launcher writes in the user data folder (--user_data_root, else
// DefaultUserDataRoot) when the player moves the prepared files off the default folder
// (launcher/src/Launcher.Core/GameFilesLocation.cs): one absolute path, the folder that holds
// the two folders above.
inline constexpr std::string_view kLocationFileName = "game-files-location.txt";

// The recorded place in that file, or nothing when the file is missing, empty or does not start
// with an absolute path. Only the first non-empty line counts, with its surrounding blanks cut.
inline std::optional<std::filesystem::path> ReadLocationFile(const std::filesystem::path& file) {
  std::ifstream input(file, std::ios::binary);
  std::string line;
  while (std::getline(input, line)) {
    const auto first = line.find_first_not_of(" \t\r\n");
    if (first == std::string::npos) continue;
    line = line.substr(first, line.find_last_not_of(" \t\r\n") - first + 1);
    // The Launcher writes the file in UTF-8, so it is decoded as UTF-8, not the ANSI code page.
    const auto place = native::TextPath(line);
    if (place.is_absolute()) return place;
    return std::nullopt;
  }
  return std::nullopt;
}

// The file the Launcher writes into a prepared folder as the last step, just before it renames the
// partial folder into place (PreparedFolder.MarkerFileName); its one line is "manifest_version=<v>".
// A folder without it was not finished. The game checks only that, not the manifest's files and
// sizes the Launcher also compares.
inline constexpr std::string_view kPreparedMarkerFile = ".topspin4-prepared";
inline constexpr std::string_view kPreparedMarkerKey = "manifest_version=";

inline bool HasCompletionMarker(const std::filesystem::path& folder) {
  std::ifstream input(folder / std::string(kPreparedMarkerFile), std::ios::binary);
  std::string text(kPreparedMarkerKey.size(), '\0');
  input.read(text.data(), static_cast<std::streamsize>(text.size()));
  return input.gcount() == static_cast<std::streamsize>(text.size()) && text == kPreparedMarkerKey;
}

// Where the game finds one prepared folder ("gamedata" or "updatedata"): the command line's folder
// when it gave one, else the place the location file records, else the default prepared-data
// folder. A recorded place is the one the Launcher uses, so a folder missing there is not looked
// for in the default place. Empty when the folder is not there or the Launcher did not finish it
// (no completion marker): the game is not set up.
inline std::filesystem::path PreparedFolder(const std::filesystem::path& command_line,
                                               const std::optional<std::filesystem::path>& recorded_root,
                                               const std::filesystem::path& default_root,
                                               std::string_view folder) {
  if (!command_line.empty()) return command_line;
  const auto prepared = (recorded_root ? *recorded_root : default_root) / std::string(folder);
  std::error_code error;
  return std::filesystem::is_directory(prepared, error) && HasCompletionMarker(prepared)
             ? prepared
             : std::filesystem::path();
}

// Why the game is not set up, as the Launcher reads it from its --not-set-up argument
// (launcher/src/Launcher.Core/NotSetUp.cs; keep the tokens equal).
inline constexpr std::string_view kNotSetUpDisc = "disc";
inline constexpr std::string_view kNotSetUpTitleUpdate = "title-update";
inline constexpr std::string_view kNotSetUpBoth = "disc-and-title-update";
// The exe folder cannot be written to, so the portable install has nowhere to keep anything. The
// Launcher shows its own refusal for that folder; this token only opens it.
inline constexpr std::string_view kNotSetUpUnwritable = "folder-not-writable";

// The not-set-up decision: nothing when both prepared folders are found (a command line folder
// counts as found, so the developer scripts are never held up), else the reason to open the
// Launcher with. The game folder is the disc, the update folder is Title Update 1.
inline std::optional<std::string_view> SetupNeededReason(
    const std::filesystem::path& command_line_game, const std::filesystem::path& command_line_update,
    const std::optional<std::filesystem::path>& recorded_root, const std::filesystem::path& default_root) {
  const bool disc = !PreparedFolder(command_line_game, recorded_root, default_root, kGameDataFolder).empty();
  const bool update = !PreparedFolder(command_line_update, recorded_root, default_root, kUpdateDataFolder).empty();
  if (disc && update) return std::nullopt;
  return !disc && !update ? kNotSetUpBoth : !disc ? kNotSetUpDisc : kNotSetUpTitleUpdate;
}

// The shader and pipeline cache: a folder of its own in the place that holds the prepared folders
// (Preparation.ShaderCacheFolderName in the Launcher, whose move carries it along).
inline constexpr std::string_view kCacheFolder = "cache";

inline std::filesystem::path CacheRootIn(const std::filesystem::path& exe_folder) {
  return PreparedDataRootIn(exe_folder) / std::string(kCacheFolder);
}

std::filesystem::path DefaultCacheRoot();

// The cache root when the player moved the game files: it stays beside the prepared data, in the
// recorded place. Without a recorded place it is the default cache root.
inline std::filesystem::path CacheRoot(const std::optional<std::filesystem::path>& recorded_root,
                                          const std::filesystem::path& default_cache_root) {
  return recorded_root ? *recorded_root / kCacheFolder : default_cache_root;
}

}  // namespace topspin4::user_paths
