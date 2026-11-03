// A system UI font, for the ImGui overlays when the game's own font is unavailable.
//
// Windows: Segoe UI (C:/Windows/Fonts). Android: the platform's Roboto (/system/fonts), which is
// the same family Segoe UI stands in for. macOS (host dev build): Helvetica. Empty when nothing
// is found, and the caller falls back to ImGui's built-in font.

#pragma once

#include <filesystem>

namespace topspin4::platform {

// A regular (bold == false) or bold (bold == true) system font, or an empty path.
std::filesystem::path SystemFontPath(bool bold);

}  // namespace topspin4::platform
