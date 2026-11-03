// Windows implementation of platform/crash.h: MiniDump through the crash_report module, and
// CommandLineToArgvW for the arguments.

#include "platform/crash.h"

#include "crash_report.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <shellapi.h>

namespace topspin4::platform {

void InstallCrashHandler() { topspin4::crash::Install(); }

bool WriteCrashDump(const std::filesystem::path& path) {
  // crash::Install() writes and exits when --ts4-write-crash-dump is on the command line; there
  // is no separate on-demand writer in the module.
  (void)path;
  return false;
}

std::vector<std::string> CommandLineArguments() {
  std::vector<std::string> args;
  int count = 0;
  wchar_t** wide = CommandLineToArgvW(GetCommandLineW(), &count);
  for (int i = 0; wide && i < count; ++i) {
    std::string arg;
    for (const wchar_t* c = wide[i]; *c; ++c) arg += *c < 0x80 ? static_cast<char>(*c) : '?';
    args.push_back(std::move(arg));
  }
  LocalFree(wide);
  return args;
}

}  // namespace topspin4::platform
