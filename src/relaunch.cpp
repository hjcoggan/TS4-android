#include "relaunch.h"

#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <mutex>
#include <string>
#include <vector>

#include <rex/cvar.h>
#include <rex/kernel/xam/module.h>
#include <rex/logging.h>
#include <rex/system/kernel_state.h>

#include "command_line.h"
#include "relaunch_plan.h"
#include "settings/config_file.h"

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <shellapi.h>
#endif

REXCVAR_DEFINE_STRING(ts4_launch_data, "", "TopSpin4",
                      "Set by the game when it relaunches itself (back to the title screen): "
                      "the file holding the launch data the previous copy left");

namespace topspin4::relaunch {
namespace {

using rex::kernel::xam::XamModule;

std::mutex g_mutex;
std::vector<uint8_t> g_pending;  // the guest's launch data for the new copy
bool g_requested = false;

#ifdef _WIN32
// Runs once the process has shut down (window closed, GPU and files released): starts the new
// copy, waits for it, and exits with its exit code.
void StartNewCopy() {
  std::vector<uint8_t> data;
  {
    std::lock_guard lock(g_mutex);
    if (!g_requested) return;
    data = g_pending;
  }
  std::error_code error;
  const std::filesystem::path file = std::filesystem::temp_directory_path(error) /
                                     ("topspin4-launch-data-" + std::to_string(GetCurrentProcessId()) + ".bin");
  {
    std::ofstream out(file, std::ios::binary);
    out.write(reinterpret_cast<const char*>(data.data()), std::streamsize(data.size()));
    if (!out) return;
  }

  int count = 0;
  wchar_t** wide = CommandLineToArgvW(GetCommandLineW(), &count);
  std::vector<std::wstring> arguments;
  for (int i = 1; wide && i < count; ++i) arguments.emplace_back(wide[i]);
  LocalFree(wide);

  wchar_t exe[32768];
  const DWORD length = GetModuleFileNameW(nullptr, exe, DWORD(std::size(exe)));
  if (length == 0 || length >= std::size(exe)) return;
  std::wstring command = command_line::QuoteArgument(exe);
  for (const std::wstring& argument : ChildArguments(arguments, file.wstring())) {
    command += L" " + command_line::QuoteArgument(argument);
  }

  STARTUPINFOW startup = {sizeof(startup)};
  PROCESS_INFORMATION process = {};
  if (!CreateProcessW(exe, command.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup,
                      &process)) {
    std::filesystem::remove(file, error);
    return;
  }
  CloseHandle(process.hThread);
  WaitForSingleObject(process.hProcess, INFINITE);
  DWORD code = 0;
  GetExitCodeProcess(process.hProcess, &code);
  CloseHandle(process.hProcess);
  std::_Exit(int(code));
}
#endif

}  // namespace

void RestoreLaunchData(rex::system::KernelState& kernel_state) {
  const std::string flag = REXCVAR_GET(ts4_launch_data);
  if (flag.empty()) return;
  const std::filesystem::path file = std::filesystem::u8path(flag);
  std::ifstream in(file, std::ios::binary);
  std::vector<uint8_t> data((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
  in.close();
  std::error_code error;
  std::filesystem::remove(file, error);
  auto xam = kernel_state.GetKernelModule<XamModule>("xam.xex");
  if (!xam) return;
  auto& loader = xam->loader_data();
  loader.launch_data = std::move(data);
  loader.launch_data_present = !loader.launch_data.empty();
  REXLOG_INFO("relaunch: restored {} bytes of launch data from {}", loader.launch_data.size(),
              settings::Utf8(file));
}

void OnGuestExit(rex::system::KernelState& kernel_state) {
  auto xam = kernel_state.GetKernelModule<XamModule>("xam.xex");
  if (!xam) return;
  const auto& loader = xam->loader_data();
  if (!IsOwnTitle(loader.launch_path)) return;
#ifdef _WIN32
  {
    std::lock_guard lock(g_mutex);
    if (g_requested) return;
    g_pending = loader.launch_data_present ? loader.launch_data : std::vector<uint8_t>{};
    g_requested = true;
  }
  REXLOG_INFO("relaunch: the game launches {} with {} bytes of launch data; a new copy starts "
              "once this one has shut down",
              loader.launch_path, loader.launch_data.size());
  std::atexit(StartNewCopy);
#else
  REXLOG_WARN("relaunch: the game asked to launch {}; not supported here, the game ends",
              loader.launch_path);
#endif
}

}  // namespace topspin4::relaunch
