// The main menu's PC SETTINGS and EXIT TO DESKTOP rows: the pure part. The route is two hidden
// rows of S4MainMenu (docs/research/game-settings.md, Addendum 3): the 3D settings row and the PS3
// Move tutorial row, which the menu script's EXPORT_PatchList appends (the Move row only when
// IsAnyMoveConnected answers yes); on Xbox nothing calls it. main_menu_rows_hooks.cpp, built into
// the Swing Module's DLL, calls it while the menu's list is built, relabels the two rows and turns
// the scene changes they ask for into our two actions.

#pragma once

#include <atomic>
#include <string>
#include <string_view>

namespace topspin4::settings::main_menu {

// The menu scene and the list inside it.
inline constexpr std::string_view kMenuScene = "S4MainMenu";
inline constexpr std::string_view kMenuList = "MainMenu";

// The text IDs of the two rows and what they say instead.
inline constexpr std::string_view kSettingsLabelId = "MainButtonText3DConfig";
inline constexpr std::string_view kExitLabelId = "MainButtonTextMoveTutorial";
inline constexpr std::string_view kSettingsLabel = "PC SETTINGS";
inline constexpr std::string_view kExitLabel = "EXIT TO DESKTOP";

// The scenes the two rows change to when chosen.
inline constexpr std::string_view kSettingsScene = "S4Xbox3DDisclaimer";
inline constexpr std::string_view kExitScene = "S4MoveTutorialMain";

// The line the menu's script prints (through Pam.PPrint, a no-op in retail) as it builds the list.
inline constexpr std::string_view kUpdateListPrint = "S4MainMenu: EXPORT_UpdateList";

// What the row says for this text ID; empty if the ID is not one of ours.
inline std::string_view LabelFor(std::string_view text_id) {
  if (text_id == kSettingsLabelId) return kSettingsLabel;
  if (text_id == kExitLabelId) return kExitLabel;
  return {};
}

enum class SceneAction { kNone, kOpenSettings, kExitToDesktop };

// What a change to scene means while current (the last scene changed to) is up. Only a change made
// from the menu is ours: the 3D disclaimer has other ways in.
inline SceneAction ActionForScene(std::string_view scene, std::string_view current) {
  if (current != kMenuScene) return SceneAction::kNone;
  if (scene == kSettingsScene) return SceneAction::kOpenSettings;
  if (scene == kExitScene) return SceneAction::kExitToDesktop;
  return SceneAction::kNone;
}

inline bool IsUpdateListPrint(std::string_view text) { return text == kUpdateListPrint; }

// The menu calls the hooks make: append the hidden rows, and turn the menu back on (what a return
// to the menu runs: rebuild the list, reselect the row, SceneOn, give the list the input).
inline constexpr std::string_view kPatchListCall = "EXPORT_PatchList()";
inline constexpr std::string_view kMenuOnCall = "On()";

// The script action that makes call on the scene node named scene_node.
inline std::string NodeCall(std::string_view scene_node, std::string_view call) {
  return "$" + std::string(scene_node) + "." + std::string(call);
}

// Sequences the hooks. The menu asks Is3DAccessible just before its UpdateList print when it is
// built (arms); that print issues our PatchList (pending), which asks Is3DAccessible and then
// IsAnyMoveConnected. Inside a running action chain PatchList is queued, so "ours" cannot be
// told by the call stack; it is the queries between issuing and the Move query. The sign-in
// event reruns UpdateList with no Is3DAccessible before it: unarmed, so the rows are not
// appended twice.
class RowSequence {
 public:
  // Is3DAccessible was asked. True: it is our PatchList asking (answer 0). False: the menu is
  // being built (call through); armed.
  bool On3DQuery() {
    if (pending_.load()) return true;
    armed_.store(true);
    return false;
  }
  // The menu's UpdateList print. True: issue our PatchList now, once per arming.
  bool OnUpdateList() {
    if (!armed_.exchange(false)) return false;
    pending_.store(true);
    return true;
  }
  // IsAnyMoveConnected was asked. True only for our PatchList, whose last query it is.
  bool OnMoveQuery() { return pending_.exchange(false); }
  // Our PatchList could not be issued: the menu's own queries are the game's again.
  void OnPatchListFailed() { pending_.store(false); }

 private:
  std::atomic<bool> armed_{false};
  std::atomic<bool> pending_{false};
};

}  // namespace topspin4::settings::main_menu
