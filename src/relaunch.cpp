#include "relaunch.h"

#include <atomic>
#include <cstdint>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include <utility>
#include <vector>

#include <rex/cvar.h>
#include <rex/kernel/xam/module.h>
#include <rex/logging.h>
#include <rex/system/kernel_state.h>
#include <rex/ui/window.h>

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

REXCVAR_DECLARE(bool, fullscreen);

REXCVAR_DEFINE_STRING(ts4_launch_data, "", "TopSpin4",
                      "Set by the game when it relaunches itself (back to the title screen): "
                      "the file holding the launch data the previous copy left");
REXCVAR_DEFINE_STRING(ts4_relaunch_ready, "", "TopSpin4",
                      "Set by the game when it relaunches itself: the event to signal once this "
                      "copy shows its window (the previous copy then hides its own)");
REXCVAR_DEFINE_STRING(ts4_relaunch_rect, "", "TopSpin4",
                      "Set by the game when it relaunches itself in a window: the previous "
                      "window's outer rectangle, x,y,width,height");

namespace topspin4::relaunch {
namespace {

using rex::kernel::xam::XamModule;

std::function<void(std::function<void()>)> g_run_on_ui;
rex::ui::Window* g_window = nullptr;
std::atomic<bool> g_shown{false};

#ifdef _WIN32
struct OwnWindows {
  HWND largest = nullptr;
  LONG area = 0;
};

BOOL CALLBACK FindLargest(HWND window, LPARAM param) {
  DWORD process = 0;
  GetWindowThreadProcessId(window, &process);
  RECT rect;
  if (process != GetCurrentProcessId() || !IsWindowVisible(window) ||
      !GetWindowRect(window, &rect)) {
    return TRUE;
  }
  auto& own = *reinterpret_cast<OwnWindows*>(param);
  const LONG area = (rect.right - rect.left) * (rect.bottom - rect.top);
  if (area > own.area) own = {window, area};
  return TRUE;
}

// The largest visible window of process `pid`.
HWND LargestWindowOf(DWORD pid) {
  struct Search {
    DWORD pid;
    HWND window = nullptr;
    LONG area = 0;
  } search{pid};
  EnumWindows(
      [](HWND window, LPARAM param) -> BOOL {
        auto& search = *reinterpret_cast<Search*>(param);
        DWORD process = 0;
        GetWindowThreadProcessId(window, &process);
        RECT rect;
        if (process == search.pid && IsWindowVisible(window) && GetWindowRect(window, &rect)) {
          const LONG area = (rect.right - rect.left) * (rect.bottom - rect.top);
          if (area > search.area) search = {search.pid, window, area};
        }
        return TRUE;
      },
      reinterpret_cast<LPARAM>(&search));
  return search.window;
}

DWORD ForegroundProcess() {
  DWORD process = 0;
  GetWindowThreadProcessId(GetForegroundWindow(), &process);
  return process;
}

BOOL CALLBACK HideIfOurs(HWND window, LPARAM) {
  DWORD process = 0;
  GetWindowThreadProcessId(window, &process);
  if (process == GetCurrentProcessId() && IsWindowVisible(window)) ShowWindow(window, SW_HIDE);
  return TRUE;
}

// Hands over to a new copy with `data` (see relaunch.h) and exits with its exit code. Called on
// the thread that saw the guest end, before the SDK's shutdown (which hard-exits when the window
// closes, so nothing after it would run). Returns only if the new copy could not be started.
void HandOver(const std::vector<uint8_t>& data) {
  const DWORD pid = GetCurrentProcessId();
  std::error_code error;
  const std::filesystem::path file =
      std::filesystem::temp_directory_path(error) /
      ("topspin4-launch-data-" + std::to_string(pid) + ".bin");
  {
    std::ofstream out(file, std::ios::binary);
    out.write(reinterpret_cast<const char*>(data.data()), std::streamsize(data.size()));
    if (!out) return;
  }

  std::vector<std::wstring> settings = {std::wstring(kLaunchDataFlag) + L"=" + file.wstring()};
  // A visible window is handed over: the new one starts hidden, in the same place, and this one
  // stays up until the new one shows (a hidden run, such as a scene run, stays hidden).
  OwnWindows own;
  EnumWindows(FindLargest, reinterpret_cast<LPARAM>(&own));
  HANDLE ready = nullptr;
  if (own.largest) {
    const std::wstring event = L"Local\\topspin4-relaunch-" + std::to_wstring(pid);
    ready = CreateEventW(nullptr, TRUE, FALSE, event.c_str());
    const bool fullscreen = REXCVAR_GET(fullscreen);
    settings.push_back(L"--window_hidden=true");
    settings.push_back(std::wstring(L"--fullscreen=") + (fullscreen ? L"true" : L"false"));
    if (ready) settings.push_back(std::wstring(kReadyFlag) + L"=" + event);
    RECT rect;
    if (!fullscreen && GetWindowRect(own.largest, &rect)) {
      settings.push_back(std::wstring(kRectFlag) + L"=" + std::to_wstring(rect.left) + L"," +
                         std::to_wstring(rect.top) + L"," +
                         std::to_wstring(rect.right - rect.left) + L"," +
                         std::to_wstring(rect.bottom - rect.top));
    }
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
  for (const std::wstring& argument : ChildArguments(arguments, settings)) {
    command += L" " + command_line::QuoteArgument(argument);
  }

  // The new copy may take the foreground from this one.
  AllowSetForegroundWindow(ASFW_ANY);
  STARTUPINFOW startup = {sizeof(startup)};
  PROCESS_INFORMATION process = {};
  if (!CreateProcessW(exe, command.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup,
                      &process)) {
    std::filesystem::remove(file, error);
    if (ready) CloseHandle(ready);
    return;
  }
  CloseHandle(process.hThread);
  if (ready) {
    // Until the new window shows, or the new copy ends, or a minute passes.
    const HANDLE handles[2] = {ready, process.hProcess};
    const DWORD waited = WaitForMultipleObjects(2, handles, FALSE, 60000);
    REXLOG_INFO("relaunch: {}", waited == WAIT_OBJECT_0       ? "the new copy shows its window"
                                : waited == WAIT_OBJECT_0 + 1 ? "the new copy ended before it showed"
                                                              : "the new copy did not show in time");
    CloseHandle(ready);
    // The keyboard and the pad are read only by the focused window. This copy, the foreground
    // one while the player plays, may give the focus away; the new copy also asks for it.
    if (waited == WAIT_OBJECT_0) {
      const bool had_focus = ForegroundProcess() == pid;
      if (HWND window = LargestWindowOf(process.dwProcessId)) {
        if (had_focus) SetForegroundWindow(window);
      }
      REXLOG_INFO("relaunch: focus {} this copy; now with {}", had_focus ? "was with" : "was not with",
                  ForegroundProcess() == process.dwProcessId ? "the new copy" : "another program");
    }
  }
  EnumWindows(HideIfOurs, 0);
  rex::FlushLogging();
  WaitForSingleObject(process.hProcess, INFINITE);
  DWORD code = 0;
  GetExitCodeProcess(process.hProcess, &code);
  CloseHandle(process.hProcess);
  std::_Exit(int(code));
}

// On the UI thread: this copy's window takes the old one's place and the old copy is told. The
// window was opened hidden (--window_hidden), so it is shown here through its own handle; SDL
// may hold a fullscreen request back while a window is hidden, so fullscreen is applied again.
void ShowHandedOverWindow() {
  auto* hwnd = g_window ? static_cast<HWND>(g_window->GetNativeWindowHandle()) : nullptr;
  if (hwnd) {
    rex::cvar::SetFlagByName("window_hidden", "false");
    Rect rect;
    if (ParseRect(REXCVAR_GET(ts4_relaunch_rect), rect)) {
      SetWindowPos(hwnd, nullptr, rect.x, rect.y, rect.width, rect.height,
                   SWP_NOZORDER | SWP_NOACTIVATE);
    }
    ShowWindow(hwnd, SW_SHOW);
    const bool focused = SetForegroundWindow(hwnd);
    REXLOG_INFO("relaunch: this copy {} the focus", focused ? "took" : "could not take");
    if (g_window->IsFullscreen()) {
      g_window->SetFullscreen(false);
      g_window->SetFullscreen(true);
    }
  }
  const std::string name = REXCVAR_GET(ts4_relaunch_ready);
  const std::wstring wide(name.begin(), name.end());  // the event name is ASCII
  if (HANDLE event = OpenEventW(EVENT_MODIFY_STATE, FALSE, wide.c_str())) {
    SetEvent(event);
    CloseHandle(event);
  }
  REXLOG_INFO("relaunch: window {} in place of the previous copy's", hwnd ? "shown" : "not found");
}
#endif

}  // namespace

void Configure(rex::system::KernelState& kernel_state, rex::ui::Window* window,
               std::function<void(std::function<void()>)> run_on_ui) {
  g_window = window;
  g_run_on_ui = std::move(run_on_ui);
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

bool WaitingForFirstFrame() {
  return !REXCVAR_GET(ts4_relaunch_ready).empty() && !g_shown.load();
}

void OnFrameShown(uint64_t) {
#ifdef _WIN32
  if (!WaitingForFirstFrame() || g_shown.exchange(true) || !g_run_on_ui) return;
  g_run_on_ui(ShowHandedOverWindow);
#endif
}

void OnGuestExit(rex::system::KernelState& kernel_state) {
  auto xam = kernel_state.GetKernelModule<XamModule>("xam.xex");
  if (!xam) return;
  const auto& loader = xam->loader_data();
  if (!IsOwnTitle(loader.launch_path)) return;
#ifdef _WIN32
  REXLOG_INFO("relaunch: the game launches {} with {} bytes of launch data; handing over to a new "
              "copy",
              loader.launch_path, loader.launch_data.size());
  HandOver(loader.launch_data_present ? loader.launch_data : std::vector<uint8_t>{});
  REXLOG_ERROR("relaunch: the new copy could not be started; the game ends");
#else
  REXLOG_WARN("relaunch: the game asked to launch {}; not supported here, the game ends",
              loader.launch_path);
#endif
}

}  // namespace topspin4::relaunch
