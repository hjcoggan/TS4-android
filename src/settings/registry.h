// The player settings (ticket 10): one list, so the settings file, the cvars that carry the
// values (settings_cvars.cpp) and the tests agree. Every default is the original game's
// behaviour; Enhancements and extras are opt-in (docs/goals.md).

#pragma once

#include <algorithm>
#include <string>
#include <string_view>
#include <vector>

#include "pad_bindings.h"

namespace topspin4::settings {

inline constexpr std::string_view kRendererClassic = "classic";
inline constexpr std::string_view kRendererNative = "native";

struct Setting {
  enum class Kind { kChoice, kBool, kText };

  std::string_view key;  // the cvar and the settings file key
  Kind kind;
  std::string_view default_value;
  std::vector<std::string_view> choices;  // kChoice only
  std::string_view help;

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

inline const std::vector<Setting>& PlayerSettings() {
  static const std::vector<Setting> settings = [] {
    std::vector<Setting> list = {
      {"ts4_renderer", Setting::Kind::kChoice, kRendererClassic,
       {kRendererClassic, kRendererNative},
       "Renderer: classic (Xenos GPU emulation, the reference) or native (Vulkan; not built "
       "yet, so Classic draws until it is). Takes effect at the next start"},
      {"ts4_unlock_agassi_outfit", Setting::Kind::kBool, "false", {},
       "Make the Agassi preorder outfit selectable, as the preorder code did on the Xbox 360. "
       "Takes effect at the next start"},
      {"mnk_mode", Setting::Kind::kBool, "false", {},
       "Play with keyboard and mouse, as a controller of their own"},
      {"mnk_mouse", Setting::Kind::kBool, "false", {},
       "Keyboard and mouse: the mouse moves the right stick"},
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
                      "start"});
    }
    for (const KeyBinding& binding : kKeyBindings) {
      list.push_back({binding.key, Setting::Kind::kText, binding.default_value, {},
                      "Keyboard: the keys for this controller input, comma-separated"});
    }
    return list;
  }();
  return settings;
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
