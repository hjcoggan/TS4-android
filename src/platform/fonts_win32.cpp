// Windows implementation of platform/fonts.h: Segoe UI.

#include "platform/fonts.h"

#include <system_error>

namespace topspin4::platform {

std::filesystem::path SystemFontPath(bool bold) {
  const std::filesystem::path path(bold ? "C:/Windows/Fonts/seguisb.ttf"
                                        : "C:/Windows/Fonts/segoeui.ttf");
  std::error_code error;
  return std::filesystem::is_regular_file(path, error) ? path : std::filesystem::path();
}

}  // namespace topspin4::platform
