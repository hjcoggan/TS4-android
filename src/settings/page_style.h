// How the Settings page looks (ticket 08): Top Spin 4's colours and font, and the sizing rules
// that keep text readable and rows unclipped at any window size. Pure, so it is unit-tested
// (tests/host/test_options_row.cpp); settings_page.cpp draws with it.
//
// The font is the game's own UI font, read from the player's game data when the page starts
// (ADR-0006: nothing of the game is committed). The palette and layout are the front end's, sampled
// from captures of its settings pages (grey and white row cells, an orange selected row, a grey
// help bar, button prompts, a blue background).
// It is not read from the data: the front end's colours live in compressed Lua scenes
// (docs/research/game-settings.md, Addendum 2), which the runtime cannot read.

#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <filesystem>
#include <string>

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
  Rgba backdrop_top, backdrop_mid, backdrop_bottom;  // the blue over the whole screen
  Rgba heading;                                      // the title and the button prompts
  Rgba ball, ball_edge, ball_seam;                   // the tennis ball beside the title
  Rgba tab_active, tab_idle;                         // section names above the rows
  Rgba label_top, label_bottom;                      // a row's label cell (light grey)
  Rgba value_top, value_bottom;                      // a row's value cell (white)
  Rgba cell_edge;
  Rgba bar_top, bar_bottom;                          // the selected row (orange)
  Rgba bar_edge;
  Rgba selected_text, selected_shadow;               // on the orange row
  Rgba idle_text, idle_value, disabled_text;         // on a grey or white cell
  Rgba launch_mark;                                  // a value pinned by a Launch option
  Rgba help_top, help_bottom, help_edge;             // the grey bar under the rows
  Rgba help_text, note, note_pending;                // on the help bar
  Rgba button_a, button_b, button_shoulder;          // the prompt glyphs at the bottom
  Rgba button_letter, button_edge;
};

// Sampled from the game's own settings pages (match settings: a grey label cell beside a white
// value cell, the selected row orange with white text and arrows, a grey help bar under the
// rows, button prompts at the bottom left, the title with a ball top left, all on the blue
// menu background). The help bar is darker than the game's so its longer text stays readable.
inline constexpr Palette kTopSpinPalette = {
    .backdrop_top = {26, 58, 84, 235},
    .backdrop_mid = {44, 92, 128, 235},
    .backdrop_bottom = {24, 54, 78, 235},
    .heading = {255, 255, 255, 255},
    .ball = {214, 222, 52, 255},
    .ball_edge = {120, 128, 20, 255},
    .ball_seam = {250, 250, 235, 255},
    .tab_active = {250, 175, 30, 255},
    .tab_idle = {200, 222, 238, 255},
    .label_top = {214, 214, 215, 255},
    .label_bottom = {204, 205, 205, 255},
    .value_top = {233, 233, 234, 255},
    .value_bottom = {251, 251, 251, 255},
    .cell_edge = {255, 255, 255, 170},
    .bar_top = {238, 184, 22, 255},
    .bar_bottom = {222, 112, 0, 255},
    .bar_edge = {255, 228, 170, 255},
    .selected_text = {255, 255, 255, 255},
    .selected_shadow = {110, 50, 0, 180},
    .idle_text = {44, 46, 48, 255},
    .idle_value = {44, 46, 48, 255},
    .disabled_text = {104, 108, 112, 255},
    .launch_mark = {160, 60, 0, 255},
    .help_top = {94, 96, 98, 240},
    .help_bottom = {78, 80, 82, 240},
    .help_edge = {190, 192, 194, 200},
    .help_text = {255, 255, 255, 255},
    .note = {215, 228, 236, 255},
    .note_pending = {255, 222, 120, 255},
    .button_a = {104, 176, 40, 255},
    .button_b = {206, 48, 40, 255},
    .button_shoulder = {200, 202, 204, 255},
    .button_letter = {30, 32, 34, 255},
    .button_edge = {255, 255, 255, 140},
};

// A row label as the game shows it: upper case (ASCII; the labels are English).
inline std::string UpperLabel(std::string text) {
  for (char& c : text) {
    if (c >= 'a' && c <= 'z') c = char(c - 'a' + 'A');
  }
  return text;
}

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
