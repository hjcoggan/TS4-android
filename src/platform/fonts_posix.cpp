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
#elif defined(__APPLE__)
  static const char* const kRegular[] = {
      "/System/Library/Fonts/Helvetica.ttc",
      "/System/Library/Fonts/SFNS.ttf",
  };
  static const char* const kBold[] = {
      "/System/Library/Fonts/Helvetica.ttc",
  };
  return bold ? FirstExisting(kBold, std::size(kBold)) : FirstExisting(kRegular, std::size(kRegular));
#else
  static const char* const kRegular[] = {
      "/run/current-system/sw/share/X11/fonts/DejaVuSans.ttf",
      "/run/current-system/sw/share/X11/fonts/LiberationSans-Regular.ttf",
      "/run/current-system/sw/share/X11/fonts/NotoSans-Regular.ttf",
      "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
      "/usr/share/fonts/TTF/DejaVuSans.ttf",
      "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans.ttf",
      "/usr/share/fonts/liberation-sans/LiberationSans-Regular.ttf",
      "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
      "/usr/share/fonts/noto/NotoSans-Regular.ttf",
      "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
  };
  static const char* const kBold[] = {
      "/run/current-system/sw/share/X11/fonts/DejaVuSans-Bold.ttf",
      "/run/current-system/sw/share/X11/fonts/LiberationSans-Bold.ttf",
      "/run/current-system/sw/share/X11/fonts/NotoSans-Bold.ttf",
      "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
      "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
      "/usr/share/fonts/dejavu-sans-fonts/DejaVuSans-Bold.ttf",
      "/usr/share/fonts/liberation-sans/LiberationSans-Bold.ttf",
      "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf",
      "/usr/share/fonts/noto/NotoSans-Bold.ttf",
      "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf",
  };
  return bold ? FirstExisting(kBold, std::size(kBold)) : FirstExisting(kRegular, std::size(kRegular));
#endif
}

}  // namespace topspin4::platform
