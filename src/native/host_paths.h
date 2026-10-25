// Paths in Native's host code: text in UTF-8 and wide opens on Windows, so a per-user path
// whose name is outside the ANSI code page still works (scripts/repo_guard.py).

#pragma once

#include <cstdio>
#include <cstring>
#include <filesystem>
#include <string>

namespace topspin4::native {

inline std::string PathText(const std::filesystem::path& path) {
  const std::u8string text = path.u8string();
  return std::string(text.begin(), text.end());
}

// A path from UTF-8 text (a cvar's value).
inline std::filesystem::path TextPath(const std::string& text) {
  return std::filesystem::path(std::u8string(text.begin(), text.end()));
}

inline FILE* OpenFile(const std::filesystem::path& path, const char* mode) {
#if defined(_WIN32)
  const std::wstring wide_mode(mode, mode + std::strlen(mode));
  return _wfopen(path.c_str(), wide_mode.c_str());
#else
  return std::fopen(path.c_str(), mode);  // narrow path ok
#endif
}

}  // namespace topspin4::native
