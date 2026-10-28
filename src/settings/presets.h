// Graphics Presets (ticket 05, spec "Presets"): named sets of values for the settings the
// Graphics section covers. Original is every covered setting at its default, so the first start
// looks like the 360 game (ADR-0001); Enhanced is the best look the port offers. A Preset is
// derived, never saved: the row shows the Preset whose values all match, else Custom. Pure; the
// Settings page model (page_model.h) and the session report both read it. Adapted from WWE All
// Stars' Graphics preset (ADR-0004).

#pragma once

#include <functional>
#include <map>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#include "settings/registry.h"

namespace topspin4::settings {

using ValueMap = std::map<std::string, std::string, std::less<>>;

// The Preset row's key on the page. It names no setting and is never saved.
inline constexpr std::string_view kPresetKey = "graphics_preset";
inline constexpr std::string_view kCustomPreset = "custom";

struct Preset {
  std::string value;  // original, enhanced
  std::string label;
  std::vector<std::pair<std::string, std::string>> values;  // each covered setting's value
};

// The covered settings and each Preset's values. Original comes from the registry's defaults, so
// it cannot drift from them.
inline const std::vector<Preset>& GraphicsPresets() {
  static const std::vector<Preset> presets = [] {
    constexpr std::string_view kCovered[] = {"ts4_resolution_scale", "ts4_msaa",
                                             "ts4_anisotropic_filtering", "ts4_sharpening"};
    constexpr std::string_view kEnhanced[] = {"auto", "4", "16", "off"};
    Preset original{"original", "Original", {}};
    Preset enhanced{"enhanced", "Enhanced", {}};
    for (size_t i = 0; i < std::size(kCovered); ++i) {
      const Setting* setting = FindSetting(kCovered[i]);
      original.values.emplace_back(std::string(kCovered[i]),
                                   setting ? std::string(setting->default_value) : std::string());
      enhanced.values.emplace_back(std::string(kCovered[i]), std::string(kEnhanced[i]));
    }
    return std::vector<Preset>{original, enhanced};
  }();
  return presets;
}

// The Preset the values match, or custom. A setting that does nothing on this renderer (Native
// only on Classic) is left out of the match, so it cannot make the row read Custom. A key the
// values lack counts as its default.
inline std::string ActivePreset(const ValueMap& values, std::string_view renderer) {
  for (const Preset& preset : GraphicsPresets()) {
    bool all = true;
    for (const auto& [key, value] : preset.values) {
      const Setting* setting = FindSetting(key);
      if (!setting || !setting->AvailableOn(renderer)) continue;
      const auto found = values.find(key);
      all &= (found != values.end() ? found->second : std::string(setting->default_value)) == value;
    }
    if (all) return preset.value;
  }
  return std::string(kCustomPreset);
}

// The session report's settings object, written after "renderer" (tickets 05 and 09): whether
// this was a Safe start and the Preset in effect at the end (original, enhanced or custom).
inline std::string SessionSettingsJson(bool safe_start, std::string_view preset) {
  return std::string("\"settings\": {\"safe_start\": ") + (safe_start ? "true" : "false") +
         ", \"preset\": \"" + std::string(preset) + "\"}";
}

}  // namespace topspin4::settings
