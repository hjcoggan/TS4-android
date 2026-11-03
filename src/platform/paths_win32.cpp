// Windows implementation of platform/paths.h: the portable install rooted at the exe folder.

#include "platform/paths.h"

#include <cstring>
#include <iterator>
#include <mutex>
#include <string>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

namespace topspin4::platform {
namespace {

std::mutex g_mutex;
std::filesystem::path g_base;
bool g_base_set = false;

std::filesystem::path ExeFolder() {
  wchar_t buffer[MAX_PATH * 4];
  const DWORD length = GetModuleFileNameW(nullptr, buffer, DWORD(std::size(buffer)));
  if (!length || length == DWORD(std::size(buffer))) return std::filesystem::path();
  return std::filesystem::path(std::wstring(buffer, length)).parent_path();
}

}  // namespace

void SetBaseFolder(const std::filesystem::path& folder) {
  std::lock_guard lock(g_mutex);
  g_base = folder;
  g_base_set = !folder.empty();
}

std::filesystem::path BaseFolder() {
  std::lock_guard lock(g_mutex);
  if (!g_base_set) {
    g_base = ExeFolder();
    g_base_set = true;
  }
  return g_base;
}

std::filesystem::path UserDataFolder() { return BaseFolder() / "userdata"; }
std::filesystem::path PreparedDataRoot() { return BaseFolder(); }
std::filesystem::path CacheFolder() { return BaseFolder() / "cache"; }
std::filesystem::path LogsFolder() { return BaseFolder() / "logs"; }

std::filesystem::path TempFolder() {
  wchar_t buffer[MAX_PATH * 4];
  const DWORD length = GetTempPathW(DWORD(std::size(buffer)), buffer);
  return length ? std::filesystem::path(std::wstring(buffer, length)) : CacheFolder() / "tmp";
}

std::filesystem::path PathFromUtf8(std::string_view text) {
  if (text.empty()) return std::filesystem::path();
  const int size = MultiByteToWideChar(CP_UTF8, 0, text.data(), int(text.size()), nullptr, 0);
  std::wstring wide(static_cast<size_t>(size), L'\0');
  MultiByteToWideChar(CP_UTF8, 0, text.data(), int(text.size()), wide.data(), size);
  return std::filesystem::path(wide);
}

std::string PathToUtf8(const std::filesystem::path& path) {
  const std::wstring wide = path.wstring();
  if (wide.empty()) return {};
  const int size = WideCharToMultiByte(CP_UTF8, 0, wide.data(), int(wide.size()), nullptr, 0,
                                       nullptr, nullptr);
  std::string utf8(static_cast<size_t>(size), '\0');
  WideCharToMultiByte(CP_UTF8, 0, wide.data(), int(wide.size()), utf8.data(), size, nullptr,
                      nullptr);
  return utf8;
}

FILE* OpenFile(const std::filesystem::path& path, const char* mode) {
  const std::wstring wide_mode(mode, mode + std::strlen(mode));
  return _wfopen(path.c_str(), wide_mode.c_str());
}

}  // namespace topspin4::platform
