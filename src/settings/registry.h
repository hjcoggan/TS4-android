// The player settings (ticket 10): one list, so the settings file, the cvars that carry the
// values (settings_cvars.cpp) and the tests agree. Every default is the original game's
// behaviour; Enhancements and extras are opt-in (docs/goals.md).

#pragma once

#include <algorithm>
#include <string>
#include <string_view>
#include <vector>

namespace topspin4::settings {

inline constexpr std::string_view kRendererClassic = "classic";
inline constexpr std::string_view kRendererNative = "native";

struct Setting {
  enum class Kind { kChoice, kBool };

  std::string_view key;  // the cvar and the settings file key
  Kind kind;
  std::string_view default_value;
  std::vector<std::string_view> choices;  // kChoice only
  std::string_view help;

  bool Allows(std::string_view value) const {
    if (kind == Kind::kBool) {
      return value == "true" || value == "false";
    }
    return std::find(choices.begin(), choices.end(), value) != choices.end();
  }
};

inline const std::vector<Setting>& PlayerSettings() {
  static const std::vector<Setting> settings = {
      {"ts4_renderer", Setting::Kind::kChoice, kRendererClassic,
       {kRendererClassic, kRendererNative},
       "Renderer: classic (Xenos GPU emulation, the reference) or native (Vulkan; not built "
       "yet, so Classic draws until it is). Takes effect at the next start"},
      {"ts4_unlock_agassi_outfit", Setting::Kind::kBool, "false", {},
       "Make the Agassi preorder outfit selectable, as the preorder code did on the Xbox 360. "
       "Takes effect at the next start"},
  };
  return settings;
}

inline const Setting* FindSetting(std::string_view key) {
  for (const Setting& setting : PlayerSettings()) {
    if (setting.key == key) return &setting;
  }
  return nullptr;
}

// The value as the settings file's TOML literal: choices quoted, booleans bare.
inline std::string TomlLiteral(const Setting& setting, std::string_view value) {
  if (setting.kind == Setting::Kind::kBool) {
    return std::string(value);
  }
  return "\"" + std::string(value) + "\"";
}

}  // namespace topspin4::settings
