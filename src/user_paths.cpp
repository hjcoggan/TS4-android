#include "user_paths.h"

#include <cstdlib>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shlobj.h>
#else
#include <rex/filesystem.h>
#endif

namespace topspin4::user_paths {

namespace {

std::optional<std::string> GetEnvironment(std::string_view name) {
  if (const char* value = std::getenv(std::string(name).c_str())) {
    return std::string(value);
  }
  return std::nullopt;
}

}  // namespace

#if defined(_WIN32)
namespace {

// Wine's ntdll exports wine_get_version; Windows' does not.
bool UnderWine() {
  HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
  return ntdll && GetProcAddress(ntdll, "wine_get_version");
}

std::filesystem::path KnownFolder(const KNOWNFOLDERID& id) {
  std::filesystem::path result;
  PWSTR path = nullptr;
  if (SUCCEEDED(SHGetKnownFolderPath(id, KF_FLAG_DEFAULT, nullptr, &path))) {
    result = path;
  }
  CoTaskMemFree(path);
  return result;
}

}  // namespace
#endif

std::filesystem::path DefaultUserDataRoot() {
#if defined(_WIN32)
  return UserDataRoot(GetEnvironment, UnderWine(), KnownFolder(FOLDERID_RoamingAppData));
#else
  // A native Linux build is after 1.0 (docs/goals.md); keep the SDK's user folder until then.
  return rex::filesystem::GetUserFolder() / kFolderName;
#endif
}

std::filesystem::path DefaultCacheRoot() {
#if defined(_WIN32)
  return UserDataRoot(GetEnvironment, UnderWine(), KnownFolder(FOLDERID_LocalAppData)) / "cache";
#else
  return DefaultUserDataRoot() / "cache";
#endif
}

}  // namespace topspin4::user_paths
