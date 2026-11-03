#include "not_set_up.h"

#include <cstdio>
#include <cstdlib>
#include <string>

#include <rex/cvar.h>
#include <rex/filesystem.h>

#include "command_line.h"
#include "platform/dialog.h"
#include "user_paths.h"

#if defined(_WIN32)
#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

namespace topspin4 {

#if defined(_WIN32)

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
  std::wstring command = command_line::QuoteArgument(launcher.wstring()) + L" --not-set-up " + Widen(reason);
  if (!user_data_root.empty()) {
    command += L" --user-data " + command_line::QuoteArgument(user_data_root.wstring());
  }
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
                reason == user_paths::kNotSetUpUnwritable
                    ? L"The game's folder can't be written to. Move the game folder somewhere you can "
                      L"write to, such as your Documents or a games drive, and start the Launcher again."
                    : L"Top Spin 4 needs to be set up first. Run topspin4-launcher.exe from the game's "
                      L"folder and follow the steps on its setup page, then start the game again.",
                L"Top Spin 4", MB_OK | MB_ICONINFORMATION | MB_SETFOREGROUND | MB_TOPMOST);
  }
  std::_Exit(0);
}

#else  // Android / POSIX: no Launcher to start and no process hand-over.

void ReportNotSetUp(std::string_view reason, const std::filesystem::path& user_data_root) {
  (void)user_data_root;
  const std::string message =
      reason == user_paths::kNotSetUpDisc
          ? "Top Spin 4 is not set up: no game data (gamedata/ with default.xex) in the app's files "
            "folder. Push the prepared data from the Mac with adb (see ts4-port/android/README.md)."
          : "Top Spin 4 is not set up: no prepared game data found in the app's files folder.";
  // Android: logcat, and the activity shows its own "data missing" screen before the library
  // loads. There is no second process to hand over to (relaunch.cpp logs and ends too).
  platform::ShowDialog(platform::DialogKind::kError, "Top Spin 4", message);
  std::_Exit(2);
}

#endif

}  // namespace topspin4
