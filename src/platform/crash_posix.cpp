// POSIX / Android implementation of platform/crash.h.
//
// The game's crash report (src/crash_report.cpp) is Windows-only; on Android this shim keeps the
// API but writes nothing. A later task can install sigaction(SIGSEGV/SIGBUS/SIGABRT) with
// _Unwind_Backtrace + dladdr and zip the log with the existing crash_zip.h.

#include "platform/crash.h"

#include <cstdlib>

#include <rex/logging.h>

namespace topspin4::platform {

void InstallCrashHandler() {
  // The SDK's exception handler (SEH by signal) owns SIGSEGV/SIGBUS; nothing to add here yet.
}

bool WriteCrashDump(const std::filesystem::path& path) {
  REXLOG_WARN("crash: dump writing is not implemented on this platform ({})", path.string());
  return false;
}

std::vector<std::string> CommandLineArguments() { return {}; }

}  // namespace topspin4::platform
