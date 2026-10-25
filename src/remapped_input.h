// Controller remapping in the game (ticket 24): the SDK's default input system, with each
// controller driver (SDL or XInput) wrapped so the game reads the pad through the player's
// bindings (pad_bindings.h). The keyboard and mouse driver keeps its own key bindings
// (keybind_*), and scripted runs (script_input.h) replace the whole input system as before.

#pragma once

#include <memory>

#include <rex/input/input_driver.h>
#include <rex/runtime.h>

#include "pad_bindings.h"

namespace topspin4 {

// The bindings in the ts4_pad_* cvars.
pad::Bindings BindingsFromCvars();

// Makes the runtime build its input system with the controller drivers remapped, unless the
// bindings are the default (then the SDK's own input system is left alone).
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
  std::unique_ptr<rex::input::InputDriver> inner_;
  pad::Bindings bindings_;
};

}  // namespace topspin4
