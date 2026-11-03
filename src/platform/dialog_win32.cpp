// Windows implementation of platform/dialog.h: MessageBoxW / ShellExecuteW.

#include "platform/dialog.h"

#include <cstdlib>
#include <string>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <shellapi.h>

namespace topspin4::platform {
namespace {

std::wstring Widen(std::string_view text) {
  if (text.empty()) return {};
  const int size = MultiByteToWideChar(CP_UTF8, 0, text.data(), int(text.size()), nullptr, 0);
  std::wstring wide(static_cast<size_t>(size), L'\0');
  MultiByteToWideChar(CP_UTF8, 0, text.data(), int(text.size()), wide.data(), size);
  return wide;
}

UINT Flags(DialogKind kind) {
  switch (kind) {
    case DialogKind::kWarning:
      return MB_OK | MB_ICONWARNING | MB_SETFOREGROUND | MB_TOPMOST;
    case DialogKind::kError:
      return MB_OK | MB_ICONERROR | MB_SETFOREGROUND | MB_TOPMOST;
    case DialogKind::kQuestion:
      return MB_YESNO | MB_ICONQUESTION | MB_SETFOREGROUND | MB_TOPMOST;
    case DialogKind::kInfo:
    default:
      return MB_OK | MB_ICONINFORMATION | MB_SETFOREGROUND | MB_TOPMOST;
  }
}

}  // namespace

bool ShowDialog(DialogKind kind, std::string_view title, std::string_view message) {
  const std::wstring wide_title = Widen(title);
  const std::wstring wide_message = Widen(message);
  const int result = MessageBoxW(nullptr, wide_message.c_str(), wide_title.c_str(), Flags(kind));
  return kind == DialogKind::kQuestion ? result == IDYES : true;
}

void ShowErrorAndExit(std::string_view title, std::string_view message) {
  ShowDialog(DialogKind::kError, title, message);
  std::_Exit(2);
}

bool ShowInFileManager(const std::filesystem::path& folder) {
  const std::wstring args = L"/select,\"" + folder.wstring() + L"\"";
  const auto result = ShellExecuteW(nullptr, L"open", L"explorer.exe", args.c_str(), nullptr,
                                    SW_SHOWNORMAL);
  return reinterpret_cast<intptr_t>(result) > 32;
}

}  // namespace topspin4::platform
