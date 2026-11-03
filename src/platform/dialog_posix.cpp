// POSIX / Android implementation of platform/dialog.h: log only.
//
// Android: REXLOG and stderr both reach logcat through SDL's stderr redirect. A real AlertDialog
// would need the SDL activity's JNI env; that is a UI task, recorded in docs/app-port.md.

#include "platform/dialog.h"

#include <cstdio>
#include <cstdlib>
#include <string>

#if defined(__ANDROID__)
#include <android/log.h>
#endif

namespace topspin4::platform {
namespace {

void Log(DialogKind kind, std::string_view title, std::string_view message) {
  const char* label = kind == DialogKind::kError     ? "error"
                      : kind == DialogKind::kWarning ? "warning"
                                                     : "info";
#if defined(__ANDROID__)
  __android_log_print(kind == DialogKind::kError ? ANDROID_LOG_ERROR : ANDROID_LOG_INFO,
                      "topspin4", "%s: %.*s", label, int(title.size()), title.data());
  __android_log_print(kind == DialogKind::kError ? ANDROID_LOG_ERROR : ANDROID_LOG_INFO,
                      "topspin4", "%.*s", int(message.size()), message.data());
#else
  std::fprintf(stderr, "topspin4 [%s] %.*s: %.*s\n", label, int(title.size()), title.data(),
               int(message.size()), message.data());
  std::fflush(stderr);
#endif
}

}  // namespace

bool ShowDialog(DialogKind kind, std::string_view title, std::string_view message) {
  Log(kind, title, message);
  return false;
}

void ShowErrorAndExit(std::string_view title, std::string_view message) {
  Log(DialogKind::kError, title, message);
  std::_Exit(2);
}

bool ShowInFileManager(const std::filesystem::path& folder) {
  Log(DialogKind::kInfo, "open folder", folder.string());
  return false;
}

}  // namespace topspin4::platform
