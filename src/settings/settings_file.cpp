#include "settings/settings_file.h"

#include <optional>
#include <system_error>

#include <toml++/toml.hpp>

#include "settings/config_file.h"
#include "settings/registry.h"

namespace topspin4::settings {

LoadReport LoadSettingsFile(const std::filesystem::path& path, const ApplySetting& apply) {
  LoadReport report;
  std::error_code error;
  if (!std::filesystem::exists(path, error)) {
    return report;
  }
  toml::table table;
  try {
    // Read through the wide path: the per-user folder holds the Windows user name, which
    // path::string() cannot always map to the code page (it throws).
    table = toml::parse(ReadText(path));
  } catch (const toml::parse_error& parse_error) {
    report.file = LoadReport::File::kCorrupt;
    report.set_aside = path;
    report.set_aside += ".corrupt";
    std::filesystem::rename(path, report.set_aside, error);
    report.warnings.push_back(
        "settings file " + Utf8(path) + " is not valid TOML (" +
        std::string(parse_error.description()) + ", line " +
        std::to_string(parse_error.source().begin.line) + "); using the defaults" +
        (error ? std::string(" (could not set it aside: ") + error.message() + ")"
               : "; the file was moved to " + Utf8(report.set_aside)));
    if (error) report.set_aside.clear();
    return report;
  }
  report.file = LoadReport::File::kLoaded;
  for (const Setting& setting : PlayerSettings()) {
    const toml::node* node = table.get(setting.key);
    if (!node) {
      continue;
    }
    std::optional<std::string> value;
    if (auto text = node->value<std::string>(); text && setting.kind != Setting::Kind::kBool) {
      value = *text;
    } else if (auto flag = node->as_boolean(); flag && setting.kind == Setting::Kind::kBool) {
      value = flag->get() ? "true" : "false";
    }
    if (!value || !setting.Allows(*value)) {
      report.warnings.push_back("settings file: " + std::string(setting.key) +
                                " has a value it does not allow; keeping the default");
      continue;
    }
    if (!apply(setting.key, *value)) {
      report.warnings.push_back("settings file: " + std::string(setting.key) + " = " + *value +
                                " was refused; keeping the default");
    }
  }
  return report;
}

}  // namespace topspin4::settings
