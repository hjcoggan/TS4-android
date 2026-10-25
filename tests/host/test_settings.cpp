// src/settings: the player settings file in the per-user folder (ticket 10), adapted from
// WWE All Stars' settings (ADR-0004).

#include <catch_amalgamated.hpp>

#include <filesystem>
#include <fstream>
#include <random>
#include <map>
#include <string>

#include "settings/config_file.h"
#include "settings/registry.h"
#include "settings/renderer_choice.h"
#include "settings/settings_file.h"

namespace fs = std::filesystem;
using namespace topspin4::settings;

namespace {

// A fresh, empty folder under the temp folder, removed at the end of the test.
struct TempFolder {
  TempFolder() {
    std::random_device random;
    path = fs::temp_directory_path() / ("ts4-settings-" + std::to_string(random()));
    fs::create_directories(path);
  }
  ~TempFolder() {
    std::error_code error;
    fs::remove_all(path, error);
  }
  fs::path path;
};

void WriteFile(const fs::path& path, const std::string& text) {
  std::ofstream(path, std::ios::binary) << text;
}

// Loads the file, collecting what it would set.
struct Loaded {
  explicit Loaded(const fs::path& path)
      : report(LoadSettingsFile(path, [this](std::string_view key, std::string_view value) {
          values[std::string(key)] = std::string(value);
          return true;
        })) {}
  std::map<std::string, std::string> values;
  LoadReport report;
};

}  // namespace

TEST_CASE("Saving a setting replaces its line and keeps every other line") {
  CHECK(SetTomlKey("# mine\nts4_renderer = \"classic\"\nother = 1\n", "ts4_renderer",
                   "\"native\"") == "# mine\nts4_renderer = \"native\"\nother = 1\n");
}

TEST_CASE("Saving a setting the file does not have appends it") {
  CHECK(SetTomlKey("other = 1", "ts4_renderer", "\"native\"") ==
        "other = 1\nts4_renderer = \"native\"\n");
  CHECK(SetTomlKey("", "ts4_renderer", "\"native\"") == "ts4_renderer = \"native\"\n");
}

TEST_CASE("A key that only starts with the setting's name is left alone") {
  CHECK(SetTomlKey("ts4_renderer_old = 1\r\n", "ts4_renderer", "\"native\"") ==
        "ts4_renderer_old = 1\r\nts4_renderer = \"native\"\n");
}

TEST_CASE("A saved setting is written whole, through a temporary file") {
  TempFolder folder;
  const fs::path path = SettingsPath(folder.path);
  CHECK(path == folder.path / "topspin4.settings.toml");
  REQUIRE(SaveSetting(path, "ts4_renderer", "\"native\""));
  REQUIRE(SaveSetting(path, "ts4_unlock_agassi_outfit", "true"));
  CHECK(ReadText(path) == "ts4_renderer = \"native\"\nts4_unlock_agassi_outfit = true\n");
  CHECK_FALSE(fs::exists(path.string() + ".tmp"));
}

TEST_CASE("Every setting defaults to the original game's behaviour") {
  // Classic is the reference renderer (ADR-0005); the disc keeps the Agassi outfit locked.
  REQUIRE(FindSetting("ts4_renderer"));
  CHECK(FindSetting("ts4_renderer")->default_value == "classic");
  REQUIRE(FindSetting("ts4_unlock_agassi_outfit"));
  CHECK(FindSetting("ts4_unlock_agassi_outfit")->default_value == "false");
  for (const Setting& setting : PlayerSettings()) {
    INFO(setting.key);
    CHECK(setting.Allows(setting.default_value));
  }
}

TEST_CASE("Without a settings file nothing is set and the defaults stay") {
  TempFolder folder;
  Loaded loaded(SettingsPath(folder.path));
  CHECK(loaded.report.file == LoadReport::File::kMissing);
  CHECK(loaded.values.empty());
}

TEST_CASE("A saved setting is set again at the next start") {
  TempFolder folder;
  const fs::path path = SettingsPath(folder.path);
  REQUIRE(SaveSetting(path, "ts4_renderer", TomlLiteral(*FindSetting("ts4_renderer"), "native")));
  REQUIRE(SaveSetting(path, "ts4_unlock_agassi_outfit",
                      TomlLiteral(*FindSetting("ts4_unlock_agassi_outfit"), "true")));
  Loaded loaded(path);
  CHECK(loaded.report.file == LoadReport::File::kLoaded);
  CHECK(loaded.values == std::map<std::string, std::string>{
                             {"ts4_renderer", "native"}, {"ts4_unlock_agassi_outfit", "true"}});
  CHECK(loaded.report.warnings.empty());
}

TEST_CASE("A value a setting does not allow is skipped with a warning; unknown keys are ignored") {
  TempFolder folder;
  const fs::path path = SettingsPath(folder.path);
  WriteFile(path,
            "ts4_renderer = \"vulkan\"\n"
            "ts4_unlock_agassi_outfit = 1\n"
            "some_removed_setting = true\n");
  Loaded loaded(path);
  CHECK(loaded.report.file == LoadReport::File::kLoaded);
  CHECK(loaded.values.empty());
  CHECK(loaded.report.warnings.size() == 2);
}

TEST_CASE("A corrupt settings file is set aside and the defaults are used") {
  TempFolder folder;
  const fs::path path = SettingsPath(folder.path);
  const std::string corrupt = "ts4_renderer = \"native\"\nthis is not toml\n";
  WriteFile(path, corrupt);
  Loaded loaded(path);
  CHECK(loaded.report.file == LoadReport::File::kCorrupt);
  CHECK(loaded.values.empty());
  CHECK_FALSE(fs::exists(path));
  CHECK(loaded.report.set_aside == fs::path(path.string() + ".corrupt"));
  CHECK(ReadText(loaded.report.set_aside) == corrupt);
  REQUIRE(loaded.report.warnings.size() == 1);
  // The next save starts a clean file.
  REQUIRE(SaveSetting(path, "ts4_renderer", "\"native\""));
  CHECK(Loaded(path).values == std::map<std::string, std::string>{{"ts4_renderer", "native"}});
}

TEST_CASE("Classic draws when Native is asked for but not available") {
  CHECK(ChooseRenderer("classic", false).renderer == Renderer::kClassic);
  CHECK(ChooseRenderer("classic", false).note.empty());
  const RendererChoice fallback = ChooseRenderer("native", false);
  CHECK(fallback.renderer == Renderer::kClassic);
  CHECK_FALSE(fallback.note.empty());
  CHECK(ChooseRenderer("native", true).renderer == Renderer::kNative);
  CHECK(ChooseRenderer("native", true).note.empty());
}

TEST_CASE("Controller and keyboard bindings are player settings that default to the pad as it is") {
  REQUIRE(FindSetting("ts4_pad_a"));
  CHECK(FindSetting("ts4_pad_a")->default_value == "a");
  CHECK(FindSetting("ts4_pad_a")->Allows("rt"));
  CHECK_FALSE(FindSetting("ts4_pad_a")->Allows("z"));
  // Keyboard and mouse stay off by default; the SDK's own key bindings apply when on.
  REQUIRE(FindSetting("mnk_mode"));
  CHECK(FindSetting("mnk_mode")->default_value == "false");
  REQUIRE(FindSetting("keybind_a"));
  CHECK(FindSetting("keybind_a")->default_value == "Semicolon,Space");
  CHECK(FindSetting("keybind_a")->Allows("Shift+J"));
  CHECK_FALSE(FindSetting("keybind_a")->Allows("bad\"quote"));
}

TEST_CASE("A saved key binding is set again at the next start") {
  TempFolder folder;
  const fs::path path = SettingsPath(folder.path);
  REQUIRE(SaveSetting(path, "keybind_a", TomlLiteral(*FindSetting("keybind_a"), "J,Space")));
  REQUIRE(SaveSetting(path, "ts4_pad_a", TomlLiteral(*FindSetting("ts4_pad_a"), "b")));
  CHECK(Loaded(path).values ==
        std::map<std::string, std::string>{{"keybind_a", "J,Space"}, {"ts4_pad_a", "b"}});
}

TEST_CASE("Key bindings may name any key, and may be cleared") {
  CHECK(FindSetting("keybind_dpad_right")->Allows("Shift+Right"));
  CHECK(FindSetting("keybind_a")->Allows(""));
  CHECK_FALSE(FindSetting("keybind_a")->Allows("a\\b"));
}
