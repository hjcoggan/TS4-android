// The game's controller input (tickets 24 and 07): the SDK's default input system, with each
// controller driver (SDL or XInput) wrapped so the game reads the pad through the player's
// bindings (pad_bindings.h) and through the Settings page's Back+Start combo filter
// (settings/open_combo.h). The keyboard and mouse driver keeps its own key bindings (keybind_*),
// and scripted runs (script_input.h) replace the whole input system as before, with the same
// combo filter on player 1's scripted pad.

#pragma once

#include <map>
#include <memory>
#include <mutex>

#include <rex/input/input_driver.h>
#include <rex/runtime.h>

#include "pad_bindings.h"
#include "settings/open_combo.h"

namespace topspin4 {

// The bindings in the ts4_pad_* cvars.
pad::Bindings BindingsFromCvars();

// Makes the runtime build its input system with the controller drivers wrapped. Always: with the
// default bindings the wrapper only filters the Back+Start combo and changes nothing else.
void InstallRemappedInput(rex::RuntimeConfig& config);

class RemappedDriver final : public rex::input::InputDriver {
 public:
  RemappedDriver(std::unique_ptr<rex::input::InputDriver> inner, pad::Bindings bindings);

  rex::X_STATUS Setup() override;
  void EnumerateDevices(std::vector<rex::input::DeviceInfo>& out) override;
  rex::X_RESULT GetDeviceState(rex::input::DeviceId id, rex::input::X_INPUT_STATE* out) override;
  rex::X_RESULT GetDeviceCapabilities(rex::input::DeviceId id, uint32_t flags,
                                      rex::input::X_INPUT_CAPABILITIES* out) override;
  rex::X_RESULT SetDeviceVibration(rex::input::DeviceId id,
                                   rex::input::X_INPUT_VIBRATION* vibration) override;
  rex::X_RESULT GetDeviceKeystroke(rex::input::DeviceId id, uint32_t flags,
                                   rex::input::X_INPUT_KEYSTROKE* out) override;
  void OnWindowAvailable(rex::ui::Window* window) override;

 private:
  struct ComboPad {
    settings::OpenComboGate gate;
    uint16_t keys_sent = 0;  // the Back/Start bits the keystroke stream has reported as down
  };

  ComboPad& ComboPadFor(rex::input::DeviceId id);
  // The physical buttons with Back and Start filtered (settings/open_combo.h).
  uint16_t FilterCombo(rex::input::DeviceId id, uint16_t physical_buttons);
  // Back and Start keystrokes follow the filtered state, not the controller driver's own events,
  // so a masked press reaches the game late instead of at once.
  rex::X_RESULT ComboKeystroke(rex::input::DeviceId id, rex::input::X_INPUT_KEYSTROKE* out);

  std::unique_ptr<rex::input::InputDriver> inner_;
  pad::Bindings bindings_;
  std::mutex pads_mutex_;
  std::map<rex::input::DeviceId, ComboPad> pads_;
};

}  // namespace topspin4
