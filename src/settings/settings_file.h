// Loading the player settings file at start (ticket 10). Values the registry does not allow are
// skipped, and keys it does not list are ignored, so an old or hand-edited file still loads. A
// file that is not TOML at all is set aside as <file>.corrupt and the defaults are used: the
// player's next change starts a clean file, and the old one stays for a bug report.

#pragma once

#include <filesystem>
#include <functional>
#include <string>
#include <string_view>
#include <vector>

namespace topspin4::settings {

struct LoadReport {
  enum class File { kMissing, kLoaded, kCorrupt };
  File file = File::kMissing;
  std::vector<std::string> warnings;
  std::filesystem::path set_aside;  // kCorrupt: where the file went
};

// Sets one setting (in the game, the cvar of that name); false if it was refused.
using ApplySetting = std::function<bool(std::string_view key, std::string_view value)>;

// Reads the settings file and applies each allowed value of a listed setting.
LoadReport LoadSettingsFile(const std::filesystem::path& path, const ApplySetting& apply);

}  // namespace topspin4::settings
