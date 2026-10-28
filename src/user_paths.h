// The per-user folder for saves and settings (ticket 14), resolved as the launcher resolves its
// own folders (launcher/src/Launcher.Core/UserDirectories.cs): %APPDATA%\TopSpin4Recomp on
// Windows. Under Proton the Windows folders sit inside the game's Wine prefix, so the Linux
// user's XDG data folder is used instead, reached through Wine's Z: drive, and saves survive a
// new prefix. --user_data_root on the command line still wins (the developer scripts pass it).

#pragma once

#include <filesystem>
#include <fstream>
#include <functional>
#include <optional>
#include <string>
#include <string_view>

namespace topspin4::user_paths {

inline constexpr std::string_view kFolderName = "TopSpin4Recomp";

using Environment = std::function<std::optional<std::string>(std::string_view)>;

// The game's folder under a Windows app data folder (roaming for saves and settings, local for
// the shader cache), or under the Linux user's XDG data folder when the game runs under Wine.
inline std::filesystem::path AppFolder(const Environment& environment, bool under_wine,
                                          const std::filesystem::path& app_data) {
  if (under_wine) {
    // XDG_DATA_HOME when it is absolute (the spec says to ignore a relative one), else
    // ~/.local/share.
    std::string unix;
    if (auto data = environment("XDG_DATA_HOME"); data && data->starts_with('/')) {
      unix = *data;
    } else if (auto home = environment("HOME"); home && home->starts_with('/')) {
      while (home->size() > 1 && home->ends_with('/')) home->pop_back();
      unix = *home + "/.local/share";
    }
    if (!unix.empty()) {
      while (unix.size() > 1 && unix.ends_with('/')) unix.pop_back();
      std::string windows = "Z:" + unix + "/" + std::string(kFolderName);
      for (char& c : windows) {
        if (c == '/') c = '\\';
      }
      return std::filesystem::path(windows);
    }
  }
  return app_data / kFolderName;
}

// The folder for this process: the real environment, Wine detection and the known folder.
std::filesystem::path DefaultUserDataRoot();

// Where the Launcher prepares the disc and TU1 (UserDirectories.PreparedDataRoot): under the
// local app data folder (the same XDG folder under Proton).
std::filesystem::path DefaultPreparedDataRoot();

// The folders inside it, named as the Launcher names them (Preparation.GameDataFolderName and
// UpdateDataFolderName), mounted as game: and update:.
inline constexpr std::string_view kGameDataFolder = "gamedata";
inline constexpr std::string_view kUpdateDataFolder = "updatedata";

// The location file the Launcher writes in the per-user folder (--user_data_root, else
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
    std::filesystem::path place(line);
    if (place.is_absolute()) return place;
    return std::nullopt;
  }
  return std::nullopt;
}

// Where the game finds one prepared folder ("gamedata" or "updatedata"): the command line's folder
// when it gave one, else the place the location file records, else the default prepared-data
// folder. A recorded place is the one the Launcher uses, so a folder missing there is not looked
// for in the default place. Empty when the folder is not there: the game is not set up.
inline std::filesystem::path PreparedFolder(const std::filesystem::path& command_line,
                                               const std::optional<std::filesystem::path>& recorded_root,
                                               const std::filesystem::path& default_root,
                                               std::string_view folder) {
  if (!command_line.empty()) return command_line;
  const auto prepared = (recorded_root ? *recorded_root : default_root) / folder;
  std::error_code error;
  return std::filesystem::is_directory(prepared, error) ? prepared : std::filesystem::path();
}

// The shader and pipeline cache: beside the launcher's prepared data under the local app data
// folder (the same XDG folder under Proton), as it is large and derivable again.
std::filesystem::path DefaultCacheRoot();

}  // namespace topspin4::user_paths
