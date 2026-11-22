// The main menu rows' pure parts, the pad combo (ticket 07) and the page's style
// (ticket 08). No GPU, no game.

#include <catch_amalgamated.hpp>

#include "pad_bindings.h"
#include "settings/open_combo.h"
#include "settings/main_menu_rows.h"
#include "settings/page_style.h"

using namespace topspin4::settings;

TEST_CASE("The two rows are relabelled and nothing else is") {
  CHECK(main_menu::LabelFor("MainButtonText3DConfig") == "PC SETTINGS");
  CHECK(main_menu::LabelFor("MainButtonTextMoveTutorial") == "EXIT TO DESKTOP");
  CHECK(main_menu::LabelFor("MainButtonTextMoveTutorial2").empty());
  CHECK(main_menu::LabelFor("MainButtonTextOptions").empty());
  CHECK(main_menu::LabelFor("MainButtonTextMoveVideoTutorial").empty());
  CHECK(main_menu::LabelFor("PS3MoveCalibrate").empty());
  CHECK(main_menu::LabelFor("").empty());
}

TEST_CASE("The rows' scene changes from the menu become the two actions") {
  using main_menu::SceneAction;
  constexpr std::string_view menu = "S4MainMenu";
  CHECK(main_menu::ActionForScene("S4Xbox3DDisclaimer", menu) == SceneAction::kOpenSettings);
  CHECK(main_menu::ActionForScene("S4MoveTutorialMain", menu) == SceneAction::kExitToDesktop);
  CHECK(main_menu::ActionForScene("S4MainMenuSubOptions", menu) == SceneAction::kNone);
  CHECK(main_menu::ActionForScene("S4MoveTutorial", menu) == SceneAction::kNone);
  CHECK(main_menu::ActionForScene("", menu) == SceneAction::kNone);
  // The same scenes reached from anywhere else are the game's.
  CHECK(main_menu::ActionForScene("S4Xbox3DDisclaimer", "S4PG_InGame") == SceneAction::kNone);
  CHECK(main_menu::ActionForScene("S4MoveTutorialMain", "") == SceneAction::kNone);
}

TEST_CASE("Only the menu's UpdateList print triggers PatchList") {
  CHECK(main_menu::IsUpdateListPrint("S4MainMenu: EXPORT_UpdateList"));
  CHECK_FALSE(main_menu::IsUpdateListPrint("S4MainMenu: EXPORT_PatchList"));
  CHECK_FALSE(main_menu::IsUpdateListPrint("S4MainMenu: EXPORT_UpdateList "));
  CHECK_FALSE(main_menu::IsUpdateListPrint(""));
  CHECK(main_menu::NodeCall("S4MainMenu", main_menu::kPatchListCall) ==
        "$S4MainMenu.EXPORT_PatchList()");
  CHECK(main_menu::NodeCall("S4MainMenu", main_menu::kMenuOnCall) == "$S4MainMenu.On()");
}

TEST_CASE("Each menu build appends the rows once, a rerun of UpdateList none") {
  main_menu::RowSequence rows;
  CHECK_FALSE(rows.OnUpdateList());  // UpdateList before any build
  CHECK_FALSE(rows.OnMoveQuery());   // the Move query from elsewhere (an invitation) stays 0

  CHECK_FALSE(rows.On3DQuery());     // the menu is built: the game's own answer
  CHECK(rows.OnUpdateList());        // issue PatchList
  CHECK(rows.On3DQuery());           // PatchList asks: ours, 0
  CHECK(rows.OnMoveQuery());         // PatchList asks: ours, 1
  CHECK_FALSE(rows.OnMoveQuery());   // and only once

  CHECK_FALSE(rows.OnUpdateList());  // the sign-in rerun appends nothing

  // Turning the menu back on builds it again.
  CHECK_FALSE(rows.On3DQuery());
  CHECK(rows.OnUpdateList());
  CHECK(rows.On3DQuery());
  CHECK(rows.OnMoveQuery());
}

TEST_CASE("A PatchList that could not be issued leaves the queries to the game") {
  main_menu::RowSequence rows;
  CHECK_FALSE(rows.On3DQuery());
  CHECK(rows.OnUpdateList());
  rows.OnPatchListFailed();
  CHECK_FALSE(rows.On3DQuery());   // the game's own answer, and armed again
  CHECK_FALSE(rows.OnMoveQuery()); // an invitation's Move query stays 0
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

TEST_CASE("Row labels are upper case, as the game's") {
  CHECK(style::UpperLabel("Resolution scale (3D)") == "RESOLUTION SCALE (3D)");
  CHECK(style::UpperLabel("") == "");
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
  CHECK(style::Contrast(p.idle_text, p.label_top) >= 4.5);
  CHECK(style::Contrast(p.idle_value, p.value_top) >= 4.5);
  CHECK(style::Contrast(p.disabled_text, p.value_bottom) >= 3.0);
  CHECK(style::Contrast(p.disabled_text, p.label_bottom) >= 3.0);  // a disabled row's label
  CHECK(style::Contrast(p.launch_mark, p.value_top) >= 4.5);
  CHECK(style::Contrast(p.heading, p.backdrop_mid) >= 4.5);
  CHECK(style::Contrast(p.help_text, p.help_top) >= 4.5);
  CHECK(style::Contrast(p.note, p.help_top) >= 4.5);
  CHECK(style::Contrast(p.note_pending, p.help_top) >= 4.5);
  CHECK(style::Contrast(p.tab_active, p.backdrop_mid) >= 3.0);
  CHECK(style::Contrast(p.tab_idle, p.backdrop_mid) >= 4.5);
  // White on the orange row, with the game's shadow under it.
  CHECK(style::Contrast(p.selected_text, p.bar_bottom) >= 3.0);
}
