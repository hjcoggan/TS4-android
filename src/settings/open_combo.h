// The pad combo that opens the Settings page (ticket 07): hold Back and Start together. Pure.
//
// The game reads every button (docs: .scratch/game-settings/issues/07-pad-combo.md, Comments),
// so no single button is free. The combo works because the guest input hold (patch 0014) turns on
// in the very poll that sees both buttons down, so the game never sees the chord complete.
// Clicking both sticks, WWE's choice, is ruled out: Top Spin 4 reads them.

#pragma once

#include <cstdint>

namespace topspin4::settings {

// XINPUT_GAMEPAD_BACK | XINPUT_GAMEPAD_START, as pad::kButtonBack and pad::kButtonStart.
inline constexpr uint16_t kOpenComboButtons = 0x0020 | 0x0010;

inline bool OpenComboDown(uint16_t buttons) {
  return (buttons & kOpenComboButtons) == kOpenComboButtons;
}

// Fires once per press: on the poll where the combo goes down, not while it stays down, so the
// page that closes with the buttons still held does not open again.
class OpenComboLatch {
 public:
  bool Update(uint16_t buttons) {
    const bool down = OpenComboDown(buttons);
    const bool fire = down && !was_down_;
    was_down_ = down;
    return fire;
  }

 private:
  bool was_down_ = false;
};

}  // namespace topspin4::settings
