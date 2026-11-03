// POSIX / Android implementation of platform/paths.h.
//
// Android has no "folder beside the executable" that can be written to: /proc/self/exe is
// /system/bin/app_process64. The activity (ts4-port/android) sets REX_APP_FOLDER and
// REX_EXECUTABLE_FOLDER to the app's files directory before the native library loads, so that is
// the base. The same layout as the Windows portable install is kept: gamedata/, updatedata/,
// userdata/, cache/ directly below it.

#include "platform/paths.h"

#include <cstdlib>
#include <cstring>
#include <mutex>
#include <string>

namespace topspin4::platform {
namespace {

std::mutex g_mutex;
std::filesystem::path g_base;
bool g_base_set = false;

const char* Env(const char* name) {
  const char* value = std::getenv(name);
  return value && *value ? value : nullptr;
}

// The folder the game data lives in, in the order the app shell can provide it.
std::filesystem::path ResolveBase() {
  for (const char* name : {"REX_APP_FOLDER", "REX_EXECUTABLE_FOLDER", "XDG_DATA_HOME", "HOME"}) {
    if (const char* value = Env(name)) return std::filesystem::path(value);
  }
  // Last resort: the working directory. On Android this is "/" and the game will report that it
  // is not set up, which is better than guessing a private path.
  std::error_code error;
  const auto current = std::filesystem::current_path(error);
  return error ? std::filesystem::path(".") : current;
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
    g_base = ResolveBase();
    g_base_set = true;
  }
  return g_base;
}

std::filesystem::path UserDataFolder() { return BaseFolder() / "userdata"; }
std::filesystem::path PreparedDataRoot() { return BaseFolder(); }
std::filesystem::path CacheFolder() { return BaseFolder() / "cache"; }
std::filesystem::path LogsFolder() { return BaseFolder() / "logs"; }

std::filesystem::path TempFolder() {
  if (const char* value = Env("TMPDIR")) return std::filesystem::path(value);
  if (const char* value = Env("TMP")) return std::filesystem::path(value);
  return CacheFolder() / "tmp";
}

std::filesystem::path PathFromUtf8(std::string_view text) {
  return std::filesystem::path(std::string(text));
}

std::string PathToUtf8(const std::filesystem::path& path) { return path.string(); }

FILE* OpenFile(const std::filesystem::path& path, const char* mode) {
  return std::fopen(path.c_str(), mode);
}

}  // namespace topspin4::platform
