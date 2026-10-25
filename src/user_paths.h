// The per-user folder for saves and settings (ticket 14), resolved as the launcher resolves its
// own folders (launcher/src/Launcher.Core/UserDirectories.cs): %APPDATA%\TopSpin4Recomp on
// Windows. Under Proton the Windows folders sit inside the game's Wine prefix, so the Linux
// user's XDG data folder is used instead, reached through Wine's Z: drive, and saves survive a
// new prefix. --user_data_root on the command line still wins (the developer scripts pass it).

#pragma once

#include <filesystem>
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

// The shader and pipeline cache: beside the launcher's prepared data under the local app data
// folder (the same XDG folder under Proton), as it is large and derivable again.
std::filesystem::path DefaultCacheRoot();

}  // namespace topspin4::user_paths
