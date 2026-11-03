// Crash handling and command-line arguments without Win32 in shared code.
//
// Windows: the crash_report module (SetUnhandledExceptionFilter + dbghelp MiniDump) and
// CommandLineToArgvW. Android: a signal handler would write a tombstone-like report; today the
// module is stubbed (docs/app-port.md), so InstallCrashHandler() does nothing and the runtime's
// own handler (src/core/exception_handler_posix.cpp) stays in charge.

#pragma once

#include <filesystem>
#include <string>
#include <vector>

namespace topspin4::platform {

// Installs the platform crash handler. Idempotent.
void InstallCrashHandler();

// Writes a crash dump (minidump on Windows; Android: false, the report module is stubbed).
bool WriteCrashDump(const std::filesystem::path& path);

// The process's arguments in UTF-8 (Windows: CommandLineToArgvW; POSIX: none available here).
std::vector<std::string> CommandLineArguments();

}  // namespace topspin4::platform
