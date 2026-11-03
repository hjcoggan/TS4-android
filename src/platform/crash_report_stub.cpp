// Android / POSIX stand-in for src/crash_report.cpp (Windows: SetUnhandledExceptionFilter +
// dbghelp MiniDumpWriteDump, which do not exist on Android).
//
// The public API (crash_report.h) is kept so the app layer compiles unchanged; the report itself
// is not written. The debug cvars stay registered, and the two forced-crash ones still crash, so
// "does the SDK's signal handling survive a host crash" can be tested on the device.
//
// What a real port would add later: sigaction(SIGSEGV/SIGBUS/SIGABRT) with _Unwind_Backtrace +
// dladdr, the session log and settings file zipped by the existing crash_zip.h, and an Android
// share Intent instead of ShellExecuteW. See docs/app-port.md.

#include "crash_report.h"

#include <csignal>
#include <cstdint>
#include <cstdlib>

#include <rex/cvar.h>
#include <rex/logging.h>

REXCVAR_DEFINE_BOOL(ts4_debug_force_crash, false, "TopSpin4",
                    "Debugging: crash once the game is set up, to check crash handling");
REXCVAR_DEFINE_BOOL(ts4_debug_force_abort, false, "TopSpin4",
                    "Debugging: call abort once the game is set up, to check crash handling");
REXCVAR_DEFINE_INT32(ts4_debug_force_crash_at_frame, -1, "TopSpin4",
                     "Debugging: crash once this guest frame is shown");

namespace topspin4::crash {
namespace {

// Kept so a later report writer can read them without touching the app layer.
std::filesystem::path g_settings_file;
std::string g_gpu_name;
std::string g_driver_version;
const uint8_t* g_guest_views[2] = {};
void* g_window = nullptr;

}  // namespace

void Install() {
  REXLOG_INFO("crash: report writing is not implemented on this platform");
}

void SetSettingsFile(const std::filesystem::path& path) { g_settings_file = path; }

void SetWindow(void* native_window) { g_window = native_window; }

void SetGpu(const std::string& name, const std::string& driver_version) {
  g_gpu_name = name;
  g_driver_version = driver_version;
}

void SetGuestMemory(const uint8_t* virtual_membase, const uint8_t* physical_membase) {
  g_guest_views[0] = virtual_membase;
  g_guest_views[1] = physical_membase;
}

void CrashIfAsked() {
  if (REXCVAR_GET(ts4_debug_force_crash)) {
    REXLOG_ERROR("crash: ts4_debug_force_crash: raising SIGSEGV");
    std::raise(SIGSEGV);
  }
  if (REXCVAR_GET(ts4_debug_force_abort)) {
    REXLOG_ERROR("crash: ts4_debug_force_abort: calling abort");
    std::abort();
  }
}

int64_t CrashFrameAsked() { return int64_t(REXCVAR_GET(ts4_debug_force_crash_at_frame)); }

void CrashIfAskedAtFrame(uint64_t frame) {
  const int64_t asked = CrashFrameAsked();
  if (asked >= 0 && int64_t(frame) >= asked) {
    REXLOG_ERROR("crash: ts4_debug_force_crash_at_frame: crashing at frame {}", frame);
    std::raise(SIGSEGV);
    std::abort();
  }
}

}  // namespace topspin4::crash
