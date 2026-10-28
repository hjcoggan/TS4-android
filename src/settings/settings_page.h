// The in-game Settings page (ticket 03/04): F4 opens it over the game, or a script's
// `set settings_page open`; the controller drives it while the game reads an idle pad (the input
// hold of patch 0014). The rows come from the pure model (page_model.h). Adapted from WWE All
// Stars' settings_page (ADR-0004, ADR-0007).

#pragma once

#include <filesystem>
#include <functional>
#include <string_view>

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

// Adds the page's fonts (Impact for headings, Segoe UI Bold for help text, when Windows has
// them). Call from OnConfigureFonts.
void AddFonts(ImFontAtlas* atlas);

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

}  // namespace topspin4::settings
