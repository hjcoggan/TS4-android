// src/script_input.h: the scripted pad's `set` command, which changes a setting at a point in a
// scene (a live change, as a player makes one from the settings page).

#include <catch_amalgamated.hpp>

#include <filesystem>
#include <fstream>
#include <string>
#include <utility>
#include <vector>

#include "script_input.h"

using topspin4::ScriptInputDriver;

namespace {

struct Run {
  std::filesystem::path file;
  uint64_t frame = 0;
  std::chrono::milliseconds now{0};
  std::vector<std::pair<std::string, std::string>> sets;
  ScriptInputDriver driver;

  explicit Run(const std::string& script)
      : file(std::filesystem::temp_directory_path() / "ts4-test-script-input.txt"),
        driver(Write(file, script), [this] { return now; }, [this] { return frame; },
               ScriptInputDriver::kDevice,
               [this](std::string name, std::string value) {
                 sets.emplace_back(std::move(name), std::move(value));
               }) {}
  ~Run() { std::filesystem::remove(file); }

  static std::filesystem::path Write(const std::filesystem::path& path, const std::string& text) {
    std::ofstream(path, std::ios::binary) << text;
    return path;
  }

  uint16_t Buttons(uint64_t at_frame) {
    frame = at_frame;
    now += std::chrono::milliseconds(16);
    rex::input::X_INPUT_STATE state{};
    driver.GetDeviceState(ScriptInputDriver::kDevice, &state);
    return state.gamepad.buttons;
  }
};

}  // namespace

TEST_CASE("A set command changes the setting when the script reaches its frame") {
  Run run("at 10\nset ts4_sharpening high\nat 20\nset ts4_msaa 4\n");
  run.Buttons(5);
  CHECK(run.sets.empty());
  run.Buttons(10);
  CHECK(run.sets == std::vector<std::pair<std::string, std::string>>{{"ts4_sharpening", "high"}});
  run.Buttons(15);
  CHECK(run.sets.size() == 1);
  run.Buttons(20);
  CHECK(run.sets.size() == 2);
  CHECK(run.sets.back() == std::pair<std::string, std::string>{"ts4_msaa", "4"});
}

TEST_CASE("A set command holds no pad state: the next press starts on the same poll") {
  Run run("at 10\nset ts4_anisotropic_filtering 16\npress A 5f\n");
  CHECK(run.Buttons(10) == rex::input::X_INPUT_GAMEPAD_A);
  CHECK(run.sets.size() == 1);
}

TEST_CASE("A set command without a value is skipped") {
  Run run("set ts4_sharpening\npress B 5f\n");
  CHECK(run.Buttons(1) == rex::input::X_INPUT_GAMEPAD_B);
  CHECK(run.sets.empty());
}
