// Saving one setting into the flat TOML settings file without touching the rest of the file,
// unlike the SDK's SaveConfig, which rewrites every non-default cvar (debug and command-line
// values included). Adapted from WWE All Stars' settings/config_file.h (ADR-0004), without its
// migrations.

#pragma once

#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>
#include <string_view>
#include <system_error>

namespace topspin4::settings {

// The player's settings file, in the per-user folder (src/user_paths.h), so that replacing the
// game folder keeps it.
inline std::filesystem::path SettingsPath(const std::filesystem::path& user_data_root) {
  return user_data_root / "topspin4.settings.toml";
}

// The text with key set to the TOML literal value: its line replaced (keeping the line end), or
// appended if the text has none.
inline std::string SetTomlKey(std::string_view text, std::string_view key, std::string_view value) {
  const std::string line = std::string(key) + " = " + std::string(value);
  std::string out;
  bool found = false;
  size_t start = 0;
  while (start < text.size()) {
    size_t end = text.find('\n', start);
    end = end == std::string_view::npos ? text.size() : end + 1;
    std::string_view current = text.substr(start, end - start);
    std::string_view ending;
    if (current.ends_with("\r\n")) {
      ending = "\r\n";
    } else if (current.ends_with('\n')) {
      ending = "\n";
    }
    std::string_view body = current.substr(0, current.size() - ending.size());
    const size_t first = body.find_first_not_of(" \t");
    std::string_view rest = first == std::string_view::npos ? "" : body.substr(first);
    bool match = false;
    if (!found && rest.starts_with(key)) {
      std::string_view after = rest.substr(key.size());
      const size_t equals = after.find_first_not_of(" \t");
      match = equals != std::string_view::npos && after[equals] == '=';
    }
    if (match) {
      out += line;
      out += ending;
      found = true;
    } else {
      out += current;
    }
    start = end;
  }
  if (!found) {
    if (!out.empty() && out.back() != '\n') {
      out += '\n';
    }
    out += line;
    out += '\n';
  }
  return out;
}

// Reads text, or empty if the file cannot be read.
inline std::string ReadText(const std::filesystem::path& path) {
  std::ifstream in{path, std::ios::binary};
  if (!in) {
    return {};
  }
  std::ostringstream buffer;
  buffer << in.rdbuf();
  return buffer.str();
}

// Writes the text through a temporary file and a rename, so a crash never leaves the file half
// written.
inline bool WriteTextAtomically(const std::filesystem::path& path, const std::string& text) {
  std::error_code error;
  std::filesystem::create_directories(path.parent_path(), error);
  const std::filesystem::path temporary = path.string() + ".tmp";
  {
    std::ofstream out{temporary, std::ios::binary | std::ios::trunc};
    if (!out) {
      return false;
    }
    out << text;
    if (!out) {
      return false;
    }
  }
  std::filesystem::rename(temporary, path, error);
  if (error) {
    std::filesystem::remove(temporary, error);
    return false;
  }
  return true;
}

// Writes key = value (a TOML literal) into the settings file.
inline bool SaveSetting(const std::filesystem::path& path, std::string_view key,
                        std::string_view value) {
  return WriteTextAtomically(path, SetTomlKey(ReadText(path), key, value));
}

}  // namespace topspin4::settings
