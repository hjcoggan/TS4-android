// Paths in Native's host code: text in UTF-8 and wide opens on Windows, so a per-user path
// whose name is outside the ANSI code page still works (scripts/repo_guard.py).

#pragma once

#include <cstdio>
#include <cstring>
#include <filesystem>
#include <string>

#include "platform/paths.h"

namespace topspin4::native {

inline std::string PathText(const std::filesystem::path& path) {
  const std::u8string text = path.u8string();
  return std::string(text.begin(), text.end());
}

// A path from UTF-8 text (a cvar's value).
inline std::filesystem::path TextPath(const std::string& text) {
  return std::filesystem::path(std::u8string(text.begin(), text.end()));
}

// The one UTF-8 file open for the whole port (platform/paths.h owns the Windows widening).
inline FILE* OpenFile(const std::filesystem::path& path, const char* mode) {
  return platform::OpenFile(path, mode);
}

}  // namespace topspin4::native
