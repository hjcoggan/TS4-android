// The player settings (ticket 10): one list, so the settings file, the cvars that carry the
// values (settings_cvars.cpp) and the tests agree. Every default is the original game's
// behaviour; Enhancements and extras are opt-in (docs/goals.md).

#pragma once

#include <algorithm>
#include <iterator>
#include <string>
#include <string_view>
#include <vector>

#include "pad_bindings.h"

namespace topspin4::settings {

inline constexpr std::string_view kRendererClassic = "classic";
inline constexpr std::string_view kRendererNative = "native";

// The settings page's sections, in the order they are shown.
enum class Section { kGraphics, kControls };
inline constexpr Section kSections[] = {Section::kGraphics, Section::kControls};

inline constexpr std::string_view SectionName(Section section) {
  switch (section) {
    case Section::kGraphics: return "Graphics";
    case Section::kControls: return "Controls";
  }
  return "";
}

struct Setting {
  enum class Kind { kChoice, kBool, kText };
  // The renderers a setting does anything on (ADR-0005).
  enum class Renderers { kBoth, kNativeOnly };

  std::string_view key;  // the cvar and the settings file key
  Kind kind;
  std::string_view default_value;
  std::vector<std::string_view> choices;  // kChoice only
  std::string_view help;
  // Where the settings page shows it: its section, label, and place among that section's rows
  // (lower first). When it applies (now or at the next start) is not here: it is the cvar's
  // lifecycle (schema.h), so it cannot disagree with what the code does.
  Section section;
  std::string_view label;
  int order;
  Renderers renderers = Renderers::kBoth;

  // Whether the setting does anything on this renderer (kRendererClassic or kRendererNative);
  // the settings page shows one that does not as unavailable.
  bool AvailableOn(std::string_view renderer) const {
    return renderers == Renderers::kBoth || renderer == kRendererNative;
  }

  bool Allows(std::string_view value) const {
    if (kind == Kind::kBool) {
      return value == "true" || value == "false";
    }
    if (kind == Kind::kText) {
      // A plain TOML basic string, as the SDK's key names are.
      return value.find_first_of("\"\\\r\n") == std::string_view::npos;
    }
    return std::find(choices.begin(), choices.end(), value) != choices.end();
  }
};

// The SDK's keyboard and mouse driver (mnk) and its key bindings, with the SDK's defaults: on,
// the keyboard and mouse are a controller of their own (ticket 24).
struct KeyBinding {
  std::string_view key, default_value;
};
inline constexpr KeyBinding kKeyBindings[] = {
    {"keybind_a", "Semicolon,Space"},       {"keybind_b", "Quote,Backspace"},
    {"keybind_x", "L"},                     {"keybind_y", "P"},
    {"keybind_left_trigger", "Q,I"},        {"keybind_right_trigger", "E,O"},
    {"keybind_left_shoulder", "1"},         {"keybind_right_shoulder", "3"},
    {"keybind_lstick_up", "W"},             {"keybind_lstick_down", "S"},
    {"keybind_lstick_left", "A"},           {"keybind_lstick_right", "D"},
    {"keybind_lstick_press", "F"},          {"keybind_rstick_up", "Up"},
    {"keybind_rstick_down", "Down"},        {"keybind_rstick_left", "Left"},
    {"keybind_rstick_right", "Right"},      {"keybind_rstick_press", "K"},
    {"keybind_dpad_up", "Shift+Up"},        {"keybind_dpad_down", "Shift+Down"},
    {"keybind_dpad_left", "Shift+Left"},    {"keybind_dpad_right", "Shift+Right"},
    {"keybind_back", "Z,Tab"},              {"keybind_start", "X,Return"},
};

namespace detail {

// "keybind_lstick_up" -> "Keys: left stick up".
inline std::string KeyBindingLabel(std::string_view key) {
  std::string label = "Keys:";
  key.remove_prefix(std::string_view("keybind_").size());
  while (!key.empty()) {
    const size_t end = key.find('_');
    std::string_view word = key.substr(0, end);
    key = end == std::string_view::npos ? std::string_view() : key.substr(end + 1);
    label += ' ';
    if (word == "lstick") {
      label += "left stick";
    } else if (word == "rstick") {
      label += "right stick";
    } else if (word == "dpad") {
      label += "D-pad";
    } else if (word.size() == 1) {
      label += static_cast<char>(word[0] - 'a' + 'A');  // the face buttons: A, B, X, Y
    } else {
      label += word;
    }
  }
  return label;
}

// The pad inputs as the player names them, in pad::kInputs order.
inline constexpr std::string_view kPadLabels[pad::kInputCount] = {
    "Pad: A",  "Pad: B",  "Pad: X",       "Pad: Y",        "Pad: LB",         "Pad: RB",
    "Pad: LT", "Pad: RT", "Pad: LS click", "Pad: RS click", "Pad: Back",       "Pad: Start",
    "Pad: D-pad up", "Pad: D-pad down", "Pad: D-pad left", "Pad: D-pad right"};

}  // namespace detail

inline const std::vector<Setting>& PlayerSettings() {
  static const std::vector<Setting> settings = [] {
    std::vector<Setting> list = {
      {"ts4_renderer", Setting::Kind::kChoice, kRendererClassic,
       {kRendererClassic, kRendererNative},
       "Renderer: classic (Xenos GPU emulation, the reference) or native (Vulkan; not built "
       "yet, so Classic draws until it is). Takes effect at the next start",
       Section::kGraphics, "Renderer", 10},
      // Image Enhancements (.scratch/image-quality/spec.md). Native only:
      {"ts4_resolution_scale", Setting::Kind::kChoice, "1", {"1", "auto", "2", "3"},
       "Resolution scale (Native renderer): draw the game at this multiple of its 1280x720, "
       "filtered down to the window when larger; auto picks the smallest that covers the "
       "16:9 area of the window, and follows it. The GPU's limit may clamp it",
       Section::kGraphics, "Resolution scale", 20, Setting::Renderers::kNativeOnly},
      {"ts4_msaa", Setting::Kind::kChoice, "original", {"original", "4", "8"},
       "MSAA (Native renderer): original (the game's own 2x on its match pass) or more "
       "samples where the game multisamples, for smoother edges; the GPU's limit may lower it",
       Section::kGraphics, "MSAA", 30, Setting::Renderers::kNativeOnly},
      // On both renderers:
      {"ts4_anisotropic_filtering", Setting::Kind::kChoice, "original",
       {"original", "4", "8", "16"},
       "Anisotropic filtering: original (the game's own texture filtering) or a level forced on "
       "textures seen at an angle (court lines, banners)",
       Section::kGraphics, "Anisotropic filtering", 40},
      {"ts4_sharpening", Setting::Kind::kChoice, "off", {"off", "low", "medium", "high"},
       "Sharpening: off, or CAS (contrast-adaptive sharpening) of the final image at low, "
       "medium or high strength",
       Section::kGraphics, "Sharpening", 50},
      {"mnk_mode", Setting::Kind::kBool, "false", {},
       "Play with keyboard and mouse, as a controller of their own",
       Section::kControls, "Keyboard and mouse", 10},
      {"mnk_mouse", Setting::Kind::kBool, "false", {},
       "Keyboard and mouse: the mouse moves the right stick",
       Section::kControls, "Mouse moves right stick", 20},
    };
    std::vector<std::string_view> inputs;
    for (const pad::InputInfo& info : pad::kInputs) inputs.push_back(info.name);
    // ts4_pad_<input>: the controller input that drives it (pad_bindings.h). The names are
    // static, so the keys can be views of them.
    static const std::vector<std::string> pad_keys = [] {
      std::vector<std::string> keys;
      for (size_t i = 0; i < pad::kInputCount; ++i) keys.push_back(pad::SettingKey(pad::Input(i)));
      return keys;
    }();
    for (size_t i = 0; i < pad::kInputCount; ++i) {
      list.push_back({pad_keys[i], Setting::Kind::kChoice, pad::kInputs[i].name, inputs,
                      "Controller: the input that drives this one. Takes effect at the next "
                      "start",
                      Section::kControls, detail::kPadLabels[i], 100 + int(i)});
    }
    // Built whole before any view is taken of it.
    static const std::vector<std::string> key_labels = [] {
      std::vector<std::string> labels;
      for (const KeyBinding& binding : kKeyBindings) {
        labels.push_back(detail::KeyBindingLabel(binding.key));
      }
      return labels;
    }();
    int key_order = 200;
    for (size_t i = 0; i < std::size(kKeyBindings); ++i) {
      list.push_back({kKeyBindings[i].key, Setting::Kind::kText, kKeyBindings[i].default_value, {},
                      "Keyboard: the keys for this controller input, comma-separated",
                      Section::kControls, key_labels[i], key_order++});
    }
    return list;
  }();
  return settings;
}

// The settings in the order the page shows them: by section, then by their order.
inline std::vector<const Setting*> SettingsInPageOrder() {
  std::vector<const Setting*> sorted;
  for (const Setting& setting : PlayerSettings()) sorted.push_back(&setting);
  std::stable_sort(sorted.begin(), sorted.end(), [](const Setting* a, const Setting* b) {
    if (a->section != b->section) return a->section < b->section;
    return a->order < b->order;
  });
  return sorted;
}

inline const Setting* FindSetting(std::string_view key) {
  for (const Setting& setting : PlayerSettings()) {
    if (setting.key == key) return &setting;
  }
  return nullptr;
}

// The value as the settings file's TOML literal: choices and text quoted, booleans bare.
inline std::string TomlLiteral(const Setting& setting, std::string_view value) {
  if (setting.kind == Setting::Kind::kBool) {
    return std::string(value);
  }
  return "\"" + std::string(value) + "\"";
}

}  // namespace topspin4::settings
