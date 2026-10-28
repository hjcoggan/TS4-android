// The player settings list as JSON, for the launcher and the settings page to read instead of
// keeping a copy (`topspin4.exe --print-settings-schema`, ticket 02). Adapted from WWE All Stars'
// settings/schema.h (ADR-0004).
//
// Format 2, one setting per line in registry order. settings_file_version is the version the
// game writes into the Settings file (CurrentVersion in config_file.h), for the launcher to use
// instead of keeping its own:
//   {"format": 2, "settings_file_version": 2, "sections": ["Graphics", "Controls"],
//    "preset_section": "Graphics", "preset_label": "Graphics preset", "preset_help": "...",
//    "presets": [{"value": "original", "label": "Original",
//                 "values": {"ts4_msaa": "original", ...}}, ...],
//    "settings": [
//     {"key": "ts4_msaa", "kind": "choice", "default": "original",
//      "choices": ["original", "4", "8"], "choice_labels": ["Original", "4x", "8x"],
//      "applies_when": "now", "renderers": "native", "section": "Graphics", "label": "MSAA",
//      "order": 30, "help": "...", "binding": null}, ...]}
// kind is choice, bool or text; choices is empty unless the kind is choice; choice_labels are the
// choices as the in-game page names them, one each; applies_when is now or next_start; renderers
// is both or native. binding is null, or {"device": "pad" | "keyboard", "action": "LS click"} for a
// controller or key binding (what it is for, without the device). presets are the Graphics
// Presets in page order, each with the value of every setting it covers (custom is derived, never
// listed); the Preset row sits at the top of preset_section.

#pragma once

#include <cstdio>
#include <functional>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#include <rex/cvar.h>

#include "settings/config_file.h"
#include "settings/presets.h"
#include "settings/registry.h"

namespace topspin4::settings {

// The export's format; a reader refuses any other.
inline constexpr int kSchemaFormat = 2;

// The command-line switch that prints the schema and exits before any window or the runtime.
inline constexpr std::string_view kPrintSchemaSwitch = "--print-settings-schema";

inline bool SchemaRequested(const std::vector<std::string>& args) {
  for (const std::string& arg : args) {
    if (arg == kPrintSchemaSwitch) return true;
  }
  return false;
}

// When a change to a setting takes effect.
enum class Applies { kNow, kNextStart };

inline std::string_view AppliesName(Applies applies) {
  return applies == Applies::kNow ? "now" : "next_start";
}

// From the setting's cvar lifecycle, never from hand-written text: only a hot-reload cvar
// changes while the game runs. Empty if the setting has no cvar.
inline std::optional<Applies> AppliesFromCvar(std::string_view key) {
  const rex::cvar::FlagEntry* flag = rex::cvar::GetFlagInfo(key);
  if (!flag) return std::nullopt;
  return flag->lifecycle == rex::cvar::Lifecycle::kHotReload ? Applies::kNow : Applies::kNextStart;
}

using AppliesLookup = std::function<std::optional<Applies>(std::string_view key)>;

inline std::string_view KindName(Setting::Kind kind) {
  switch (kind) {
    case Setting::Kind::kChoice: return "choice";
    case Setting::Kind::kBool: return "bool";
    case Setting::Kind::kText: return "text";
  }
  return "";
}

// The text as a JSON string literal.
inline std::string JsonString(std::string_view text) {
  std::string out = "\"";
  for (char c : text) {
    switch (c) {
      case '"': out += "\\\""; break;
      case '\\': out += "\\\\"; break;
      case '\n': out += "\\n"; break;
      case '\r': out += "\\r"; break;
      case '\t': out += "\\t"; break;
      default:
        if (static_cast<unsigned char>(c) < 0x20) {
          char escaped[8];
          std::snprintf(escaped, sizeof(escaped), "\\u%04x", static_cast<unsigned char>(c));
          out += escaped;
        } else {
          out += c;
        }
    }
  }
  return out + "\"";
}

// The registry as JSON. A setting the lookup knows no cvar for is exported as applying at the
// next start, the safe reading.
inline std::string SchemaJson(const AppliesLookup& applies = AppliesFromCvar) {
  std::string out = "{\"format\": " + std::to_string(kSchemaFormat) +
                    ", \"settings_file_version\": " + std::to_string(CurrentVersion()) +
                    ", \"sections\": [";
  for (size_t i = 0; i < std::size(kSections); ++i) {
    out += (i ? ", " : "") + JsonString(SectionName(kSections[i]));
  }
  out += "],\n \"preset_section\": " + JsonString(SectionName(Section::kGraphics));
  out += ", \"preset_label\": " + JsonString(kPresetLabel);
  out += ", \"preset_help\": " + JsonString(PresetHelp());
  out += ",\n \"presets\": [";
  const std::vector<Preset>& presets = GraphicsPresets();
  for (size_t p = 0; p < presets.size(); ++p) {
    out += (p ? ", " : "") + std::string("{\"value\": ") + JsonString(presets[p].value) +
           ", \"label\": " + JsonString(presets[p].label) + ", \"values\": {";
    for (size_t v = 0; v < presets[p].values.size(); ++v) {
      out += (v ? ", " : "") + JsonString(presets[p].values[v].first) + ": " +
             JsonString(presets[p].values[v].second);
    }
    out += "}}";
  }
  out += "],\n \"settings\": [\n";
  const std::vector<Setting>& settings = PlayerSettings();
  for (size_t i = 0; i < settings.size(); ++i) {
    const Setting& setting = settings[i];
    out += "  {\"key\": " + JsonString(setting.key);
    out += ", \"kind\": " + JsonString(KindName(setting.kind));
    out += ", \"default\": " + JsonString(setting.default_value);
    out += ", \"choices\": [";
    for (size_t c = 0; c < setting.choices.size(); ++c) {
      out += (c ? ", " : "") + JsonString(setting.choices[c]);
    }
    out += "], \"choice_labels\": [";
    for (size_t c = 0; c < setting.choices.size(); ++c) {
      out += (c ? ", " : "") + JsonString(ChoiceLabel(setting, setting.choices[c]));
    }
    out += "], \"applies_when\": " +
           JsonString(AppliesName(applies(setting.key).value_or(Applies::kNextStart)));
    out += std::string(", \"renderers\": ") +
           (setting.renderers == Setting::Renderers::kBoth ? "\"both\"" : "\"native\"");
    out += ", \"section\": " + JsonString(SectionName(setting.section));
    out += ", \"label\": " + JsonString(setting.label);
    out += ", \"order\": " + std::to_string(setting.order);
    out += ", \"help\": " + JsonString(setting.help);
    const BindingInfo binding = BindingOf(setting);
    out += ", \"binding\": ";
    out += binding.device.empty() ? std::string("null")
                                  : "{\"device\": " + JsonString(binding.device) +
                                        ", \"action\": " + JsonString(binding.action) + "}";
    out += "}";
    out += i + 1 < settings.size() ? ",\n" : "\n";
  }
  return out + " ]}\n";
}

// With --print-settings-schema on the command line: prints SchemaJson to standard output and
// exits, before any window opens or the runtime starts, so it needs no game data. Otherwise
// returns. Defined in schema_export.cpp (the game only).
void PrintSchemaAndExitIfAsked();

}  // namespace topspin4::settings
