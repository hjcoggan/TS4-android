// The in-game Settings page (ticket 03/04): F4 opens it over the game, so does the pad combo Back +
// Start (open_combo.h), the OPTIONS menu's PC SETTINGS row (options_row.h) and a script's
// `set settings_page open`; the controller drives it while the game reads an idle pad (the input
// hold of patch 0014). The rows come from the pure model (page_model.h). Adapted from WWE All
// Stars' settings_page (ADR-0004, ADR-0007).

#pragma once

#include <filesystem>
#include <functional>
#include <string_view>

#include "pad_bindings.h"
#include "settings/page_model.h"

namespace rex::input {
class InputSystem;
}
namespace rex::ui {
class ImGuiDrawer;
}

struct ImFontAtlas;

namespace topspin4::settings {

// What the Settings file and the Launch options gave at start, taken when the file is loaded
// (topspin4_app.h): the model's pinned set and file values.
struct SessionStart {
  ValueMap file_values;                           // what the Settings file holds
  std::set<std::string, std::less<>> pinned;      // settings given as Launch options
  bool safe_start = false;
};

// Records how this session started. Call from OnPostInitLogging, once the file is loaded.
void BeginSession(SessionStart start);

// The controller bindings the game's input system was built with (none remapped: the defaults).
// The page reads the pad with them undone, so a remap never moves the page's own navigation.
// Leave it unset when a scripted run replaces the input system.
void SetGamePadBindings(const pad::Bindings& bindings);

// The session report's "settings" object: whether this was a Safe start and the Graphics Preset
// in effect (original, enhanced or custom). For frame_dumps' session end.
std::string SessionReportJson();

// The Graphics Preset in effect now, from the cvars; for the log.
std::string CurrentPreset();

// Adds the page's font: the game's own (Media\XUI\DroidSans-Bold.ttf in the player's game data),
// else Segoe UI Bold when Windows has it, else ImGui's built-in font. Call from OnConfigureFonts.
void AddFonts(ImFontAtlas* atlas, const std::filesystem::path& game_data_root);

// Registers the F4 bind and the guest input hold. Changes are saved to settings_path. run_on_ui
// runs a function on the UI thread. Call once the ImGui drawer and the input system exist.
void Configure(rex::ui::ImGuiDrawer* drawer, rex::input::InputSystem* input,
               const std::filesystem::path& settings_path,
               std::function<void(std::function<void()>)> run_on_ui);

// The script input key whose value drives the page: `set settings_page open|close|toggle` opens
// or closes it; the page then reads the script's pad (press UP/DOWN/LEFT/RIGHT/A/B).
inline constexpr std::string_view kScriptKey = "settings_page";

// Runs the command if name is kScriptKey. Call on the UI thread. True if it was one.
bool RunScriptCommand(std::string_view name, std::string_view value);

// A script's `set key value` for a player setting in the registry: the same change path as the
// page (apply now or at the next start, save rules, Launch options and Safe start, the open
// page's rows). Call on the UI thread. False if key is no player setting; any other cvar is the
// caller's to set directly.
bool SetPlayerSettingFromScript(std::string_view key, std::string_view value);

}  // namespace topspin4::settings
