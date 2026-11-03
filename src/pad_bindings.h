// Controller remapping (ticket 24): which physical input drives each of the 16 pad inputs the
// game sees. The game reads a remapped pad (src/remapped_input.h wraps the controller drivers),
// so every action it binds to a button follows. The default is the pad as it is; the bindings
// are player settings (ts4_pad_<input>), and rebinding swaps instead of doubling up.
// Pure: the pad here is host-endian, and the keystroke codes are the Xbox 360's VK_PAD_*.

#pragma once

#include <algorithm>
#include <array>
#include <cstdint>
#include <functional>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace topspin4::pad {

inline constexpr uint16_t kButtonDpadUp = 0x0001;
inline constexpr uint16_t kButtonDpadDown = 0x0002;
inline constexpr uint16_t kButtonDpadLeft = 0x0004;
inline constexpr uint16_t kButtonDpadRight = 0x0008;
inline constexpr uint16_t kButtonStart = 0x0010;
inline constexpr uint16_t kButtonBack = 0x0020;
inline constexpr uint16_t kButtonLeftThumb = 0x0040;
inline constexpr uint16_t kButtonRightThumb = 0x0080;
inline constexpr uint16_t kButtonLeftShoulder = 0x0100;
inline constexpr uint16_t kButtonRightShoulder = 0x0200;
inline constexpr uint16_t kButtonA = 0x1000;
inline constexpr uint16_t kButtonB = 0x2000;
inline constexpr uint16_t kButtonX = 0x4000;
inline constexpr uint16_t kButtonY = 0x8000;

inline constexpr uint16_t kVkPadA = 0x5800;
inline constexpr uint16_t kVkPadB = 0x5801;
inline constexpr uint16_t kVkPadStart = 0x5814;

// A trigger counts as a press above this, as XINPUT_GAMEPAD_TRIGGER_THRESHOLD.
inline constexpr uint8_t kTriggerThreshold = 30;

struct Pad {
  uint16_t buttons = 0;
  uint8_t left_trigger = 0;
  uint8_t right_trigger = 0;
  int16_t thumb_lx = 0, thumb_ly = 0, thumb_rx = 0, thumb_ry = 0;
  bool operator==(const Pad&) const = default;
};

enum class Input : uint8_t {
  kA, kB, kX, kY, kLB, kRB, kLT, kRT, kLS, kRS, kBack, kStart, kUp, kDown, kLeft, kRight,
  kCount
};
inline constexpr size_t kInputCount = size_t(Input::kCount);

struct InputInfo {
  std::string_view name;  // in settings and the settings file
  uint16_t button;        // 0 for a trigger
  uint16_t virtual_key;   // VK_PAD_*
};

inline constexpr std::array<InputInfo, kInputCount> kInputs = {{
    {"a", kButtonA, kVkPadA},          {"b", kButtonB, kVkPadB},
    {"x", kButtonX, 0x5802},           {"y", kButtonY, 0x5803},
    {"lb", kButtonLeftShoulder, 0x5805}, {"rb", kButtonRightShoulder, 0x5804},
    {"lt", 0, 0x5806},                 {"rt", 0, 0x5807},
    {"ls", kButtonLeftThumb, 0x5816},  {"rs", kButtonRightThumb, 0x5817},
    {"back", kButtonBack, 0x5815},     {"start", kButtonStart, kVkPadStart},
    {"up", kButtonDpadUp, 0x5810},     {"down", kButtonDpadDown, 0x5811},
    {"left", kButtonDpadLeft, 0x5812}, {"right", kButtonDpadRight, 0x5813},
}};

inline std::string_view InputName(Input input) { return kInputs[size_t(input)].name; }
inline std::string SettingKey(Input input) { return "ts4_pad_" + std::string(InputName(input)); }

inline std::optional<Input> InputNamed(std::string_view name) {
  for (size_t i = 0; i < kInputCount; ++i) {
    if (kInputs[i].name == name) return Input(i);
  }
  return std::nullopt;
}

// For each input the game sees, the physical input that drives it.
class Bindings {
 public:
  static Bindings Default() {
    Bindings bindings;
    for (size_t i = 0; i < kInputCount; ++i) bindings.source_[i] = Input(i);
    return bindings;
  }
  Input Source(Input action) const { return source_[size_t(action)]; }
  void Set(Input action, Input physical) { source_[size_t(action)] = physical; }

 private:
  std::array<Input, kInputCount> source_{};
};

// Binds `action` to `physical`; the action that used `physical` takes `action`'s old input.
inline void Rebind(Bindings& bindings, Input action, Input physical) {
  const Input old = bindings.Source(action);
  for (size_t i = 0; i < kInputCount; ++i) {
    if (Input(i) != action && bindings.Source(Input(i)) == physical) {
      bindings.Set(Input(i), old);
    }
  }
  bindings.Set(action, physical);
}

struct Conflict {
  Input physical;
  std::vector<Input> actions;  // two or more
};

// Physical inputs that drive more than one action.
inline std::vector<Conflict> Conflicts(const Bindings& bindings) {
  std::vector<Conflict> conflicts;
  for (size_t p = 0; p < kInputCount; ++p) {
    Conflict conflict{Input(p), {}};
    for (size_t a = 0; a < kInputCount; ++a) {
      if (bindings.Source(Input(a)) == Input(p)) conflict.actions.push_back(Input(a));
    }
    if (conflict.actions.size() > 1) conflicts.push_back(std::move(conflict));
  }
  return conflicts;
}

// The physical value of an input: 0-255 for a trigger, 0 or 255 for a button.
inline uint8_t Value(const Pad& pad, Input input) {
  if (input == Input::kLT) return pad.left_trigger;
  if (input == Input::kRT) return pad.right_trigger;
  return (pad.buttons & kInputs[size_t(input)].button) ? 255 : 0;
}

// The pad the game sees. Sticks pass through.
inline Pad Remap(const Pad& physical, const Bindings& bindings) {
  Pad out = physical;
  out.buttons = 0;
  for (size_t a = 0; a < kInputCount; ++a) {
    const Input action = Input(a);
    const Input source = bindings.Source(action);
    const uint8_t value = Value(physical, source);
    const bool source_is_trigger = source == Input::kLT || source == Input::kRT;
    if (action == Input::kLT) {
      out.left_trigger = value;
    } else if (action == Input::kRT) {
      out.right_trigger = value;
    } else if (source_is_trigger ? value > kTriggerThreshold : value != 0) {
      out.buttons |= kInputs[a].button;
    }
  }
  // The guide button and anything else outside the 16 inputs stays as it is.
  uint16_t bound_buttons = 0;
  for (const InputInfo& info : kInputs) bound_buttons |= info.button;
  out.buttons |= physical.buttons & ~bound_buttons;
  return out;
}

// The physical pad behind what the game sees: Remap undone. The Settings page reads the pad this
// way, so a remap never moves the page's own navigation (ticket 06). Each action's value goes
// back to the input that drives it; sticks and buttons outside the 16 pass through.
inline Pad Unmap(const Pad& game, const Bindings& bindings) {
  Pad out = game;
  out.buttons = 0;
  out.left_trigger = 0;
  out.right_trigger = 0;
  for (size_t a = 0; a < kInputCount; ++a) {
    const Input action = Input(a);
    const Input source = bindings.Source(action);
    const uint8_t value = Value(game, action);
    const bool action_is_trigger = action == Input::kLT || action == Input::kRT;
    const bool pressed = action_is_trigger ? value > kTriggerThreshold : value != 0;
    if (source == Input::kLT) {
      out.left_trigger = std::max<uint8_t>(out.left_trigger, action_is_trigger ? value : (pressed ? 255 : 0));
    } else if (source == Input::kRT) {
      out.right_trigger = std::max<uint8_t>(out.right_trigger, action_is_trigger ? value : (pressed ? 255 : 0));
    } else if (pressed) {
      out.buttons |= kInputs[size_t(source)].button;
    }
  }
  uint16_t bound_buttons = 0;
  for (const InputInfo& info : kInputs) bound_buttons |= info.button;
  out.buttons |= game.buttons & ~bound_buttons;
  return out;
}

// A keystroke from a physical input comes out as the first action that input drives, and not
// at all from an input that drives none (a hand-edited double binding left it unbound). Codes
// outside the 16 inputs (stick directions) pass through.
inline std::optional<uint16_t> RemapKeystroke(uint16_t virtual_key, const Bindings& bindings) {
  for (size_t p = 0; p < kInputCount; ++p) {
    if (kInputs[p].virtual_key != virtual_key) continue;
    for (size_t a = 0; a < kInputCount; ++a) {
      if (bindings.Source(Input(a)) == Input(p)) return kInputs[a].virtual_key;
    }
    return std::nullopt;
  }
  return virtual_key;
}

// The bindings in the settings: ts4_pad_<input> names the physical input; a missing or unknown
// name keeps the input's own.
inline Bindings BindingsFromSettings(
    const std::function<std::optional<std::string>(std::string_view key)>& setting) {
  Bindings bindings = Bindings::Default();
  for (size_t a = 0; a < kInputCount; ++a) {
    if (auto value = setting(SettingKey(Input(a)))) {
      if (auto physical = InputNamed(*value)) bindings.Set(Input(a), *physical);
    }
  }
  return bindings;
}

}  // namespace topspin4::pad
