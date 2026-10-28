// `topspin4.exe --print-settings-schema`: writes the player settings list as JSON to standard
// output and exits before any window opens or the runtime starts (ticket 02). Adapted from WWE
// All Stars' settings/schema_export.cpp (ADR-0004).

#include "settings/schema.h"

#include <cstdio>
#include <cstdlib>
#include <string>
#include <vector>

#include <rex/platform.h>

#if REX_PLATFORM_WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

#include <fcntl.h>
#include <io.h>
#include <shellapi.h>
#endif

namespace topspin4::settings {

namespace {

std::vector<std::string> ProcessArguments() {
  std::vector<std::string> args;
#if REX_PLATFORM_WIN32
  // The switch is plain ASCII.
  int count = 0;
  wchar_t** wide = CommandLineToArgvW(GetCommandLineW(), &count);
  for (int i = 0; wide && i < count; ++i) {
    std::string arg;
    for (const wchar_t* c = wide[i]; *c; ++c) arg += *c < 0x80 ? static_cast<char>(*c) : '?';
    args.push_back(std::move(arg));
  }
  LocalFree(wide);
#endif
  return args;
}

void UseStandardOutput() {
#if REX_PLATFORM_WIN32
  // A GUI exe has no console: a pipe or file the caller gave already works, otherwise print to
  // the terminal that started it.
  if (_fileno(stdout) < 0 && AttachConsole(ATTACH_PARENT_PROCESS)) {
    std::freopen("CONOUT$", "w", stdout);
  }
  // Plain \n line ends, so the output is the same bytes everywhere.
  _setmode(_fileno(stdout), _O_BINARY);
#endif
}

}  // namespace

void PrintSchemaAndExitIfAsked() {
  if (!SchemaRequested(ProcessArguments())) return;
  const std::string text = SchemaJson();
  UseStandardOutput();
  std::fwrite(text.data(), 1, text.size(), stdout);
  std::fflush(stdout);
  std::exit(std::ferror(stdout) ? EXIT_FAILURE : EXIT_SUCCESS);
}

}  // namespace topspin4::settings
