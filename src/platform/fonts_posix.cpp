// POSIX / Android implementation of platform/fonts.h.

#include "platform/fonts.h"

#include <iterator>
#include <system_error>

namespace topspin4::platform {
namespace {

std::filesystem::path FirstExisting(const char* const* candidates, size_t count) {
  std::error_code error;
  for (size_t i = 0; i < count; ++i) {
    const std::filesystem::path path(candidates[i]);
    if (std::filesystem::is_regular_file(path, error)) return path;
  }
  return {};
}

}  // namespace

std::filesystem::path SystemFontPath(bool bold) {
#if defined(__ANDROID__)
  // Android 12+ ships Roboto under /system/fonts; the static names have been stable for years.
  static const char* const kRegular[] = {
      "/system/fonts/Roboto-Regular.ttf",
      "/system/fonts/RobotoStatic-Regular.ttf",
      "/system/fonts/DroidSans.ttf",
  };
  static const char* const kBold[] = {
      "/system/fonts/Roboto-Bold.ttf",
      "/system/fonts/RobotoStatic-Bold.ttf",
      "/system/fonts/DroidSans-Bold.ttf",
  };
  return bold ? FirstExisting(kBold, std::size(kBold)) : FirstExisting(kRegular, std::size(kRegular));
#else
  static const char* const kRegular[] = {
      "/System/Library/Fonts/Helvetica.ttc",
      "/System/Library/Fonts/SFNS.ttf",
  };
  static const char* const kBold[] = {
      "/System/Library/Fonts/Helvetica.ttc",
  };
  return bold ? FirstExisting(kBold, std::size(kBold)) : FirstExisting(kRegular, std::size(kRegular));
#endif
}

}  // namespace topspin4::platform
