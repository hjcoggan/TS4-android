// The PC SETTINGS row in the game's OPTIONS menu (ticket 11): the pure part. The route is the
// ready-made PS3 Move calibration row of S4MainMenuSubOptions (docs/research/game-settings.md,
// Addendum 2): the game shows it when IsMoveController answers yes, labels it with the text ID
// PS3MoveCalibrate and runs ForceMoveRecalibrate when it is chosen. options_row_hooks.cpp, built
// into the Swing Module's DLL, answers yes only in OPTIONS, relabels the row and opens the page.

#pragma once

#include <string_view>

namespace topspin4::settings::options_row {

// The scene whose Move row is the PC SETTINGS row. Matched as a substring of the owning scene's
// name, which may carry a path.
inline constexpr std::string_view kScene = "S4MainMenuSubOptions";
// The text ID the Move row's label is looked up under.
inline constexpr std::string_view kMoveLabelId = "PS3MoveCalibrate";
// What the row says.
inline constexpr std::string_view kLabel = "PC SETTINGS";

inline bool IsOptionsScene(std::string_view scene_name) {
  return scene_name.find(kScene) != std::string_view::npos;
}

inline bool IsMoveLabelId(std::string_view text_id) { return text_id == kMoveLabelId; }

}  // namespace topspin4::settings::options_row
