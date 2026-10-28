// The OPTIONS row's pure parts (ticket 11), the pad combo (ticket 07) and the page's style
// (ticket 08). No GPU, no game.

#include <catch_amalgamated.hpp>

#include "pad_bindings.h"
#include "settings/open_combo.h"
#include "settings/options_row.h"
#include "settings/page_style.h"

using namespace topspin4::settings;

TEST_CASE("The row answers only for the OPTIONS scene and the Move calibration label") {
  CHECK(options_row::IsOptionsScene("S4MainMenuSubOptions"));
  CHECK(options_row::IsOptionsScene("Scenes\\S4MainMenuSubOptions.scene"));
  CHECK_FALSE(options_row::IsOptionsScene("S4PG_OptionsMenu"));
  CHECK_FALSE(options_row::IsOptionsScene("S4StartScreen"));
  CHECK_FALSE(options_row::IsOptionsScene(""));
  CHECK(options_row::IsMoveLabelId("PS3MoveCalibrate"));
  CHECK_FALSE(options_row::IsMoveLabelId("PS3MoveCalibrate2"));
  CHECK_FALSE(options_row::IsMoveLabelId("MainButtonTextTwoD"));
}

TEST_CASE("The pad combo is Back and Start together, and fires once per press") {
  using topspin4::pad::kButtonA;
  using topspin4::pad::kButtonBack;
  using topspin4::pad::kButtonStart;
  CHECK(kOpenComboButtons == (kButtonBack | kButtonStart));
  CHECK_FALSE(OpenComboDown(kButtonBack));
  CHECK_FALSE(OpenComboDown(kButtonStart));
  CHECK(OpenComboDown(kButtonBack | kButtonStart));
  CHECK(OpenComboDown(kButtonBack | kButtonStart | kButtonA));

  OpenComboLatch latch;
  CHECK_FALSE(latch.Update(kButtonStart));
  CHECK(latch.Update(kButtonStart | kButtonBack));
  CHECK_FALSE(latch.Update(kButtonStart | kButtonBack));  // still held
  CHECK_FALSE(latch.Update(kButtonStart));
  CHECK(latch.Update(kButtonStart | kButtonBack));        // pressed again
}

TEST_CASE("The page uses the game's font from the player's game data") {
  CHECK(style::GameFontPath("D:/game") ==
        std::filesystem::path("D:/game") / "Media" / "XUI" / "DroidSans-Bold.ttf");
}

TEST_CASE("Long text shrinks to fit and short text is left alone") {
  CHECK(style::FitSize(28, 100, 300) == 28);
  CHECK(style::FitSize(28, 300, 300) == 28);
  CHECK(style::FitSize(28, 400, 200) == Catch::Approx(16.8f));  // floor: 60%
  CHECK(style::FitSize(28, 350, 300) == Catch::Approx(24.0f));
  CHECK(style::FitSize(28, 50, 0) == 28);
}

TEST_CASE("The page's text is readable on its backgrounds") {
  const style::Palette& p = style::kTopSpinPalette;
  CHECK(style::Contrast(p.idle_text, p.idle_bottom) >= 4.5);
  CHECK(style::Contrast(p.idle_value, p.idle_bottom) >= 4.5);
  CHECK(style::Contrast(p.disabled_text, p.idle_top) >= 3.0);
  CHECK(style::Contrast(p.launch_mark, p.idle_bottom) >= 4.5);
  CHECK(style::Contrast(p.heading, p.panel_top) >= 4.5);
  CHECK(style::Contrast(p.note, p.panel_bottom) >= 4.5);
  CHECK(style::Contrast(p.note_pending, p.panel_top) >= 4.5);
  CHECK(style::Contrast(p.tab_active, p.panel_top) >= 3.0);
  // White on the orange row, with the game's shadow under it.
  CHECK(style::Contrast(p.selected_text, p.bar_bottom) >= 3.0);
}
