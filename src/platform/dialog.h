// User-facing messages without Win32 in shared code (docs/windows-api-audit.md).
//
// Windows shows a MessageBoxW. Android has no native message box reachable without JNI, so the
// POSIX/Android implementation writes the message to the log (logcat) and returns false: the
// activity owns the UI. Callers treat false as "nobody saw it" and log as well.

#pragma once

#include <filesystem>
#include <string_view>

namespace topspin4::platform {

enum class DialogKind { kInfo, kWarning, kError, kQuestion };

// Shows a message. Returns true when the user answered yes (kQuestion), or dismissed it (others);
// false when no dialog could be shown.
bool ShowDialog(DialogKind kind, std::string_view title, std::string_view message);

// Shows the message where possible and ends the process (exit code 2). Does not return.
[[noreturn]] void ShowErrorAndExit(std::string_view title, std::string_view message);

// Opens a folder in the platform's file browser. Windows: explorer.exe; Android: no-op (false).
bool ShowInFileManager(const std::filesystem::path& folder);

}  // namespace topspin4::platform
