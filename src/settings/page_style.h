// How the Settings page looks (ticket 08): Top Spin 4's colours and font, and the sizing rules
// that keep text readable and rows unclipped at any window size. Pure, so it is unit-tested
// (tests/host/test_page_style.cpp); settings_page.cpp draws with it.
//
// The font is the game's own UI font, read from the player's game data when the page starts
// (ADR-0006: nothing of the game is committed). The palette is the front end's, sampled from a
// capture of its menus (white rows with dark text, an orange selected row, a blue background).
// It is not read from the data: the front end's colours live in compressed Lua scenes
// (docs/research/game-settings.md, Addendum 2), which the runtime cannot read.

#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <filesystem>

namespace topspin4::settings::style {

struct Rgba {
  uint8_t r, g, b, a;
  // Dear ImGui's packed colour (IM_COL32).
  constexpr uint32_t Packed() const {
    return uint32_t(r) | uint32_t(g) << 8 | uint32_t(b) << 16 | uint32_t(a) << 24;
  }
  constexpr Rgba WithAlpha(uint8_t alpha) const { return {r, g, b, alpha}; }
};

struct Palette {
  Rgba panel_top, panel_bottom, panel_edge;  // the blue behind the page
  Rgba heading;                              // the title and help text on the panel
  Rgba tab_active, tab_idle;                 // section names on the panel
  Rgba rule;                                 // the lines above and below the rows
  Rgba bar_top, bar_bottom;                  // the selected row (orange)
  Rgba bar_edge;
  Rgba idle_top, idle_bottom;                // the other rows (white)
  Rgba selected_text, selected_shadow;       // on the orange row
  Rgba idle_text, idle_value, disabled_text; // on a white row
  Rgba launch_mark;                          // a value pinned by a Launch option, on a white row
  Rgba note, note_pending;                   // the line under the help text
};

// Sampled from the game's main menu: the selected row is orange with white text, the others are
// white with dark grey text, on a blue background.
inline constexpr Palette kTopSpinPalette = {
    .panel_top = {46, 98, 140, 240},
    .panel_bottom = {22, 52, 80, 240},
    .panel_edge = {150, 195, 225, 170},
    .heading = {255, 255, 255, 255},
    .tab_active = {255, 190, 40, 255},
    .tab_idle = {200, 222, 238, 255},
    .rule = {200, 225, 245, 150},
    .bar_top = {240, 150, 10, 255},
    .bar_bottom = {208, 98, 0, 255},
    .bar_edge = {255, 214, 120, 255},
    .idle_top = {250, 250, 250, 245},
    .idle_bottom = {222, 224, 226, 245},
    .selected_text = {255, 255, 255, 255},
    .selected_shadow = {96, 40, 0, 190},
    .idle_text = {52, 56, 60, 255},
    .idle_value = {52, 56, 60, 255},
    .disabled_text = {120, 128, 136, 255},
    .launch_mark = {160, 60, 0, 255},
    .note = {196, 216, 230, 255},
    .note_pending = {255, 214, 90, 255},
};

// WCAG relative luminance and contrast ratio (opaque colours).
inline double Luminance(Rgba c) {
  auto channel = [](uint8_t v) {
    const double s = v / 255.0;
    return s <= 0.03928 ? s / 12.92 : std::pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * channel(c.r) + 0.7152 * channel(c.g) + 0.0722 * channel(c.b);
}

inline double Contrast(Rgba a, Rgba b) {
  const double la = Luminance(a), lb = Luminance(b);
  return (std::max(la, lb) + 0.05) / (std::min(la, lb) + 0.05);
}

// The game's UI font, in the player's game data (the Media\XUI folder of the disc).
inline std::filesystem::path GameFontPath(const std::filesystem::path& game_data_root) {
  return game_data_root / "Media" / "XUI" / "DroidSans-Bold.ttf";
}

// The size at which text of natural `width` at `size` fits in `max_width`: shrunk, never grown,
// and never below min_ratio of size (past that the text is cut to fit by the caller).
inline float FitSize(float size, float width, float max_width, float min_ratio = 0.6f) {
  if (width <= max_width || width <= 0 || max_width <= 0) return size;
  return std::max(size * (max_width / width), size * min_ratio);
}

}  // namespace topspin4::settings::style
