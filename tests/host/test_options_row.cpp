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

TEST_CASE("The pad combo is Back and Start together") {
  using topspin4::pad::kButtonA;
  using topspin4::pad::kButtonBack;
  using topspin4::pad::kButtonStart;
  CHECK(kOpenComboButtons == (kButtonBack | kButtonStart));
  CHECK_FALSE(OpenComboDown(kButtonBack));
  CHECK_FALSE(OpenComboDown(kButtonStart));
  CHECK(OpenComboDown(kButtonBack | kButtonStart));
  CHECK(OpenComboDown(kButtonBack | kButtonStart | kButtonA));
}

TEST_CASE("The chord opens the page once, and the game sees neither button") {
  using topspin4::pad::kButtonBack;
  using topspin4::pad::kButtonStart;
  OpenComboFilter filter;

  auto r = filter.Update(kButtonStart, 1000);
  CHECK(r.game_buttons == 0);
  CHECK_FALSE(r.open);
  r = filter.Update(kButtonStart, 1050);
  CHECK(r.game_buttons == 0);
  r = filter.Update(kButtonStart | kButtonBack, 1080);
  CHECK(r.game_buttons == 0);
  CHECK(r.open);
  r = filter.Update(kButtonStart | kButtonBack, 1100);
  CHECK_FALSE(r.open);  // once per press
  r = filter.Update(kButtonBack, 1200);
  CHECK(r.game_buttons == 0);  // masked until both are up
  CHECK_FALSE(r.open);
  r = filter.Update(0, 1300);
  CHECK(r.game_buttons == 0);

  // Pressed again after release: opens again.
  filter.Update(kButtonBack, 2000);
  CHECK(filter.Update(kButtonBack | kButtonStart, 2010).open);
  filter.Update(0, 2100);

  // Both in the same poll.
  r = filter.Update(kButtonBack | kButtonStart, 2500);
  CHECK(r.open);
  CHECK(r.game_buttons == 0);
}

TEST_CASE("A button held past the window passes through, and a late partner opens nothing") {
  using topspin4::pad::kButtonBack;
  using topspin4::pad::kButtonStart;
  OpenComboFilter filter;

  CHECK(filter.Update(kButtonBack, 3000).game_buttons == 0);
  CHECK(filter.Update(kButtonBack, 3099).game_buttons == 0);
  auto r = filter.Update(kButtonBack, 3100);
  CHECK(r.game_buttons == kButtonBack);
  CHECK_FALSE(r.open);
  r = filter.Update(kButtonBack | kButtonStart, 3300);
  CHECK(r.game_buttons == (kButtonBack | kButtonStart));
  CHECK_FALSE(r.open);
  r = filter.Update(0, 3400);  // long past the replay: released for good
  CHECK(r.game_buttons == 0);
  CHECK_FALSE(r.open);
}

TEST_CASE("A tap shorter than the window is replayed to the game, late but not lost") {
  using topspin4::pad::kButtonBack;
  using topspin4::pad::kButtonStart;
  OpenComboFilter filter;

  CHECK(filter.Update(kButtonStart, 4000).game_buttons == 0);
  // Released after 30 ms: the game sees Start pressed for kReplayMs from the release.
  auto r = filter.Update(0, 4030);
  CHECK(r.game_buttons == kButtonStart);
  CHECK_FALSE(r.open);
  CHECK(filter.Update(0, 4030 + kReplayMs - 1).game_buttons == kButtonStart);
  CHECK(filter.Update(0, 4030 + kReplayMs).game_buttons == 0);

  // Back is replayed as Back.
  filter.Update(kButtonBack, 5000);
  CHECK(filter.Update(0, 5010).game_buttons == kButtonBack);
  filter.Update(0, 5100);

  // A press made during the replay waits its own window, and can still be a chord.
  filter.Update(kButtonStart, 6000);
  filter.Update(0, 6010);
  CHECK(filter.Update(kButtonStart, 6030).game_buttons == kButtonStart);  // still replaying
  CHECK(filter.Update(kButtonStart, 6070).game_buttons == 0);             // judged afresh
  CHECK(filter.Update(kButtonStart | kButtonBack, 6080).open);

  // Released just after passing through: still shown for kReplayMs in all.
  filter.Update(0, 7000);
  filter.Update(kButtonStart, 7100);
  CHECK(filter.Update(kButtonStart, 7200).game_buttons == kButtonStart);
  CHECK(filter.Update(0, 7210).game_buttons == kButtonStart);
  CHECK(filter.Update(0, 7250).game_buttons == 0);
}

TEST_CASE("The gate masks only Back and Start and lets everything else through") {
  using topspin4::pad::kButtonA;
  using topspin4::pad::kButtonBack;
  using topspin4::pad::kButtonStart;
  OpenComboGate gate;
  int opened = 0;
  SetOpenComboHandler([&opened] { ++opened; });

  CHECK(gate.Apply(kButtonStart | kButtonA, 100) == kButtonA);
  CHECK(gate.Apply(kButtonStart | kButtonA | 0x0100, 120) == (kButtonA | 0x0100));
  CHECK(opened == 0);
  CHECK(gate.Apply(kButtonStart | kButtonBack | kButtonA, 140) == kButtonA);
  CHECK(opened == 1);
  CHECK(gate.Apply(kButtonStart | kButtonBack | kButtonA, 160) == kButtonA);
  CHECK(opened == 1);
  CHECK(gate.Apply(kButtonA, 180) == kButtonA);
  // A tap: Start comes back after the release.
  gate.Apply(kButtonStart, 1000);
  CHECK(gate.Apply(kButtonA, 1020) == (kButtonA | kButtonStart));
  SetOpenComboHandler({});
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
