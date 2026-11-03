#include "fault_log.h"

#include <atomic>
#include <cstdint>
#include <mutex>

#include <rex/logging.h>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <dbghelp.h>
#endif

namespace topspin4::fault_log {

#if defined(_WIN32)
namespace {

constexpr int kMaxReports = 3;
constexpr int kMaxFrames = 32;
std::atomic<int> reports{0};
std::mutex dbghelp_mutex;  // dbghelp is single-threaded

void LogFrame(HANDLE process, int index, DWORD64 address) {
  alignas(SYMBOL_INFO) char buffer[sizeof(SYMBOL_INFO) + 256] = {};
  auto* symbol = reinterpret_cast<SYMBOL_INFO*>(buffer);
  symbol->SizeOfStruct = sizeof(SYMBOL_INFO);
  symbol->MaxNameLen = 255;
  DWORD64 displacement = 0;
  IMAGEHLP_LINE64 line{sizeof(IMAGEHLP_LINE64)};
  DWORD line_displacement = 0;
  const bool has_symbol = SymFromAddr(process, address, &displacement, symbol);
  const bool has_line = SymGetLineFromAddr64(process, address, &line_displacement, &line);
  IMAGEHLP_MODULE64 module{sizeof(IMAGEHLP_MODULE64)};
  const char* module_name = SymGetModuleInfo64(process, address, &module) ? module.ModuleName : "?";
  REXLOG_ERROR("  #{:02} {:016X} {}!{}+0x{:X} {}:{}", index, address, module_name,
               has_symbol ? symbol->Name : "?", displacement, has_line ? line.FileName : "",
               has_line ? line.LineNumber : 0);
}

LONG CALLBACK OnException(EXCEPTION_POINTERS* info) {
  const auto* record = info->ExceptionRecord;
  if (record->ExceptionCode != EXCEPTION_ACCESS_VIOLATION || reports.fetch_add(1) >= kMaxReports) {
    return EXCEPTION_CONTINUE_SEARCH;
  }
  std::lock_guard lock(dbghelp_mutex);
  HANDLE process = GetCurrentProcess();
  static const bool symbols = [process] {
    SymSetOptions(SymGetOptions() | SYMOPT_LOAD_LINES | SYMOPT_UNDNAME | SYMOPT_DEFERRED_LOADS);
    return SymInitialize(process, nullptr, TRUE) != FALSE;
  }();
  REXLOG_ERROR("Unhandled host access violation: {} of {:016X} at {:016X} (thread {})",
               record->ExceptionInformation[0] ? "write" : "read", record->ExceptionInformation[1],
               reinterpret_cast<uintptr_t>(record->ExceptionAddress), GetCurrentThreadId());
  if (!symbols) {
    rex::FlushLogging();
    return EXCEPTION_CONTINUE_SEARCH;
  }
  CONTEXT context = *info->ContextRecord;
  STACKFRAME64 frame{};
  frame.AddrPC = {context.Rip, 0, AddrModeFlat};
  frame.AddrStack = {context.Rsp, 0, AddrModeFlat};
  frame.AddrFrame = {context.Rbp, 0, AddrModeFlat};
  for (int i = 0; i < kMaxFrames; ++i) {
    if (!StackWalk64(IMAGE_FILE_MACHINE_AMD64, process, GetCurrentThread(), &frame, &context,
                     nullptr, SymFunctionTableAccess64, SymGetModuleBase64, nullptr) ||
        !frame.AddrPC.Offset) {
      break;
    }
    LogFrame(process, i, frame.AddrPC.Offset);
  }
  rex::FlushLogging();
  return EXCEPTION_CONTINUE_SEARCH;
}

}  // namespace

void Install() { AddVectoredExceptionHandler(0, OnException); }
#else
void Install() {}
#endif

}  // namespace topspin4::fault_log
