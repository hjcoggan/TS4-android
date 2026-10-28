#include "settings/settings_file.h"

#include <algorithm>
#include <optional>
#include <system_error>

#include <toml++/toml.hpp>

#include "settings/config_file.h"
#include "settings/registry.h"

namespace topspin4::settings {

LoadReport LoadSettingsFile(const std::filesystem::path& path, const ApplySetting& apply,
                            const std::vector<Migration>& migrations) {
  LoadReport report;
  std::error_code error;
  if (!std::filesystem::exists(path, error)) {
    return report;
  }
  toml::table table;
  // Read through the wide path: the per-user folder holds the Windows user name, which
  // path::string() cannot always map to the code page (it throws).
  const std::string text = ReadText(path);
  try {
    table = toml::parse(text);
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
  report.file_version = std::max(1LL, FileVersion(text).value_or(1));
  if (report.file_version > CurrentVersion(migrations)) {
    report.newer_than_build = true;
    report.warnings.push_back("settings file " + Utf8(path) + " is version " +
                              std::to_string(report.file_version) + ", newer than this build's " +
                              std::to_string(CurrentVersion(migrations)) +
                              "; reading what it can and leaving the file as it is");
  } else if (const std::string migrated = MigrateSettingsText(text, migrations); migrated != text) {
    try {
      table = toml::parse(migrated);
      report.rewritten = WriteTextAtomically(path, migrated);
      if (!report.rewritten) {
        report.warnings.push_back("settings file " + Utf8(path) +
                                  " could not be updated to version " +
                                  std::to_string(CurrentVersion(migrations)));
      }
    } catch (const toml::parse_error& parse_error) {
      report.warnings.push_back("settings file " + Utf8(path) + " did not migrate cleanly (" +
                                std::string(parse_error.description()) +
                                "); reading it as it is");
    }
  }
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
    } else if (auto number = node->as_integer(); number && setting.kind == Setting::Kind::kChoice) {
      // A bare 16 for a choice of numbers (Allows still decides whether 16 is one).
      value = std::to_string(number->get());
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
