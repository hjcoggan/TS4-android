// Loading the player settings file at start (ticket 10). Values the registry does not allow are
// skipped, and keys it does not list are ignored, so an old or hand-edited file still loads. A
// file that is not TOML at all is set aside as <file>.corrupt and the defaults are used: the
// player's next change starts a clean file, and the old one stays for a bug report.
//
// The file carries a version (config_file.h): the build's migrations run on the file's text
// before any value is applied, and a file brought up to date (or one with no version, which is
// version 1) is written back once. A file of a newer build is read as well as possible and
// never rewritten.

#pragma once

#include <filesystem>
#include <functional>
#include <string>
#include <string_view>
#include <vector>

#include "settings/config_file.h"

namespace topspin4::settings {

struct LoadReport {
  enum class File { kMissing, kLoaded, kCorrupt };
  File file = File::kMissing;
  std::vector<std::string> warnings;
  std::filesystem::path set_aside;  // kCorrupt: where the file went
  long long file_version = 1;       // kLoaded: the version the file had (1 if it had none)
  bool newer_than_build = false;    // kLoaded: written by a newer build; left as it is
  bool rewritten = false;           // kLoaded: migrated or versioned, and written back
};

// Sets one setting (in the game, the cvar of that name); false if it was refused.
using ApplySetting = std::function<bool(std::string_view key, std::string_view value)>;

// Reads the settings file and applies each allowed value of a listed setting.
LoadReport LoadSettingsFile(const std::filesystem::path& path, const ApplySetting& apply,
                            const std::vector<Migration>& migrations = Migrations());

}  // namespace topspin4::settings
