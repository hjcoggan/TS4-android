// src/pad_bindings.h: controller remapping (ticket 24). Which physical input drives each input
// the game sees, the pad state and keystrokes that come out, and rebinding without conflicts.

#include <catch_amalgamated.hpp>

#include <map>
#include <optional>
#include <string>

#include "pad_bindings.h"

using namespace topspin4::pad;

namespace {

Pad Pressed(uint16_t buttons, uint8_t lt = 0, uint8_t rt = 0) {
  Pad pad;
  pad.buttons = buttons;
  pad.left_trigger = lt;
  pad.right_trigger = rt;
  pad.thumb_lx = 1000;
  pad.thumb_ry = -2000;
  return pad;
}

}  // namespace

TEST_CASE("The default bindings hand the game the pad as it is") {
  const Pad pad = Pressed(kButtonA | kButtonDpadUp | kButtonStart, 40, 255);
  CHECK(Remap(pad, Bindings::Default()) == pad);
  CHECK(Conflicts(Bindings::Default()).empty());
}

TEST_CASE("A swapped pair of buttons swaps what the game sees, sticks untouched") {
  Bindings bindings = Bindings::Default();
  bindings.Set(Input::kA, Input::kB);
  bindings.Set(Input::kB, Input::kA);

  const Pad out = Remap(Pressed(kButtonA), bindings);
  CHECK(out.buttons == kButtonB);
  CHECK(out.thumb_lx == 1000);
  CHECK(out.thumb_ry == -2000);
  CHECK(Remap(Pressed(kButtonA | kButtonB), bindings).buttons == (kButtonA | kButtonB));
}

TEST_CASE("Buttons and triggers can drive each other") {
  Bindings bindings = Bindings::Default();
  bindings.Set(Input::kRT, Input::kRB);  // the right trigger comes from the right shoulder
  bindings.Set(Input::kRB, Input::kRT);
  CHECK(Remap(Pressed(kButtonRightShoulder), bindings).right_trigger == 255);
  CHECK(Remap(Pressed(0), bindings).right_trigger == 0);
  // A trigger counts as pressed past XInput's threshold (30).
  CHECK(Remap(Pressed(0, 0, 30), bindings).buttons == 0);
  CHECK(Remap(Pressed(0, 0, 31), bindings).buttons == kButtonRightShoulder);
}

TEST_CASE("Rebinding to an input another action uses swaps the two, so nothing conflicts") {
  Bindings bindings = Bindings::Default();
  Rebind(bindings, Input::kA, Input::kX);
  CHECK(bindings.Source(Input::kA) == Input::kX);
  CHECK(bindings.Source(Input::kX) == Input::kA);
  CHECK(Conflicts(bindings).empty());
}

TEST_CASE("A hand-edited file binding one input to two actions is reported") {
  Bindings bindings = Bindings::Default();
  bindings.Set(Input::kB, Input::kA);  // A now drives A and B; nothing drives B
  const auto conflicts = Conflicts(bindings);
  REQUIRE(conflicts.size() == 1);
  CHECK(conflicts[0].physical == Input::kA);
  CHECK(conflicts[0].actions == std::vector<Input>{Input::kA, Input::kB});
}

TEST_CASE("Bindings come from the settings, one ts4_pad_<input> per input") {
  std::map<std::string, std::string> settings = {{"ts4_pad_a", "x"}, {"ts4_pad_x", "a"},
                                                  {"ts4_pad_start", "nonsense"}};
  const Bindings bindings = BindingsFromSettings([&](std::string_view key) -> std::optional<std::string> {
    auto found = settings.find(std::string(key));
    if (found == settings.end()) return std::nullopt;
    return found->second;
  });
  CHECK(bindings.Source(Input::kA) == Input::kX);
  CHECK(bindings.Source(Input::kX) == Input::kA);
  CHECK(bindings.Source(Input::kStart) == Input::kStart);
  CHECK(SettingKey(Input::kLT) == "ts4_pad_lt");
  CHECK(InputName(Input::kDown) == "down");
}

TEST_CASE("Menus see a remapped button's keystroke as the action it drives") {
  Bindings bindings = Bindings::Default();
  Rebind(bindings, Input::kA, Input::kB);
  CHECK(RemapKeystroke(kVkPadA, bindings) == kVkPadB);
  CHECK(RemapKeystroke(kVkPadB, bindings) == kVkPadA);
  CHECK(RemapKeystroke(kVkPadStart, bindings) == kVkPadStart);
  // Stick directions are not bindable and pass through.
  CHECK(RemapKeystroke(0x5820, bindings) == 0x5820);
}
