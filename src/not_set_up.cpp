#include "not_set_up.h"

#include <cstdio>
#include <cstdlib>
#include <string>

#include <rex/cvar.h>
#include <rex/filesystem.h>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

namespace topspin4 {

namespace {

constexpr const wchar_t* kLauncherName = L"topspin4-launcher.exe";

std::wstring Widen(std::string_view text) {
  return std::wstring(text.begin(), text.end());
}

// Starts the Launcher beside the exe on the setup page; false when there is none or it won't start.
bool StartLauncher(std::string_view reason, const std::filesystem::path& user_data_root) {
  const std::filesystem::path folder = rex::filesystem::GetExecutableFolder();
  const std::filesystem::path launcher = folder / kLauncherName;
  std::error_code error;
  if (!std::filesystem::exists(launcher, error)) return false;
  std::wstring command = L"\"" + launcher.wstring() + L"\" --not-set-up " + Widen(reason);
  if (!user_data_root.empty()) command += L" --user-data \"" + user_data_root.wstring() + L"\"";
  STARTUPINFOW startup{};
  startup.cb = sizeof(startup);
  PROCESS_INFORMATION process{};
  if (!CreateProcessW(launcher.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr,
                      folder.c_str(), &startup, &process)) {
    return false;
  }
  CloseHandle(process.hThread);
  CloseHandle(process.hProcess);
  return true;
}

}  // namespace

void ReportNotSetUp(std::string_view reason, const std::filesystem::path& user_data_root) {
  if (rex::cvar::Query<bool>("headless")) {
    std::fprintf(stderr, "Top Spin 4 is not set up (%.*s) and no game folders were given.\n",
                 int(reason.size()), reason.data());
    std::_Exit(2);
  }
  if (!StartLauncher(reason, user_data_root)) {
    MessageBoxW(nullptr,
                L"Top Spin 4 needs to be set up first. Run topspin4-launcher.exe from the game's "
                L"folder and follow the steps on its setup page, then start the game again.",
                L"Top Spin 4", MB_OK | MB_ICONINFORMATION | MB_SETFOREGROUND | MB_TOPMOST);
  }
  std::_Exit(0);
}

}  // namespace topspin4
