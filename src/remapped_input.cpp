#include "remapped_input.h"

#include <chrono>
#include <string>
#include <utility>

#include <rex/cvar.h>
#include <rex/input/device_assignment.h>
#include <rex/input/input_system.h>
#include <rex/input/mnk/mnk_input_driver.h>
#include <rex/input/nop/nop_input_driver.h>
#include <rex/input/sdl/sdl_input_driver.h>
#include <rex/logging.h>
#include <rex/ui/virtual_key.h>
#if REX_PLATFORM_WIN32
#include <rex/input/xinput/xinput_input_driver.h>
#endif

REXCVAR_DECLARE(std::string, input_backend);

namespace topspin4 {

using namespace rex::input;
using rex::X_RESULT;
using rex::X_STATUS;

namespace {

uint64_t NowMs() {
  return uint64_t(std::chrono::duration_cast<std::chrono::milliseconds>(
                      std::chrono::steady_clock::now().time_since_epoch())
                      .count());
}

pad::Pad ToPad(const X_INPUT_GAMEPAD& gamepad) {
  pad::Pad out;
  out.buttons = gamepad.buttons;
  out.left_trigger = gamepad.left_trigger;
  out.right_trigger = gamepad.right_trigger;
  out.thumb_lx = gamepad.thumb_lx;
  out.thumb_ly = gamepad.thumb_ly;
  out.thumb_rx = gamepad.thumb_rx;
  out.thumb_ry = gamepad.thumb_ry;
  return out;
}

void FromPad(const pad::Pad& in, X_INPUT_GAMEPAD& gamepad) {
  gamepad.buttons = in.buttons;
  gamepad.left_trigger = in.left_trigger;
  gamepad.right_trigger = in.right_trigger;
  gamepad.thumb_lx = in.thumb_lx;
  gamepad.thumb_ly = in.thumb_ly;
  gamepad.thumb_rx = in.thumb_rx;
  gamepad.thumb_ry = in.thumb_ry;
}

// The SDK's CreateDefaultInputSystem (input/input_system.cpp), with the controller driver
// wrapped in a RemappedDriver.
std::unique_ptr<InputSystem> CreateRemappedInputSystem(bool tool_mode,
                                                      const pad::Bindings& bindings) {
  auto input = std::make_unique<InputSystem>(nullptr);
  if (!tool_mode) {
    std::unique_ptr<InputDriver> controllers;
#if REX_PLATFORM_WIN32
    if (REXCVAR_GET(input_backend) == "xinput") {
      controllers = std::make_unique<xinput::XinputInputDriver>(nullptr, 0);
    }
#endif
    if (REXCVAR_GET(input_backend) == "sdl") {
      controllers = std::make_unique<sdl::SDLInputDriver>(nullptr, 0);
    }
    if (controllers) {
      auto remapped = std::make_unique<RemappedDriver>(std::move(controllers), bindings);
      if (remapped->Setup() == X_STATUS_SUCCESS) {
        input->AddDriver(std::move(remapped));
      }
    }
    auto mnk_driver = std::make_unique<mnk::MnkInputDriver>(nullptr, 0);
    if (mnk_driver->Setup() == X_STATUS_SUCCESS) {
      input->AddDriver(std::move(mnk_driver));
    }
  }
  const uint8_t nop_index = tool_mode ? 0 : 1;
  input->AddDriver(std::make_unique<nop::NopInputDriver>(nullptr, nop_index));
  input->SetDeviceAssignment(std::make_unique<SlotAssignment>());
  return input;
}

}  // namespace

pad::Bindings BindingsFromCvars() {
  return pad::BindingsFromSettings([](std::string_view key) -> std::optional<std::string> {
    if (!rex::cvar::GetFlagInfo(key)) return std::nullopt;
    return rex::cvar::GetFlagByName(key);
  });
}

void InstallRemappedInput(rex::RuntimeConfig& config) {
  const pad::Bindings bindings = BindingsFromCvars();
  for (size_t i = 0; i < pad::kInputCount; ++i) {
    const auto action = pad::Input(i);
    if (bindings.Source(action) != action) {
      REXLOG_INFO("Controller: {} is driven by {}", pad::InputName(action),
                  pad::InputName(bindings.Source(action)));
    }
  }
  for (const pad::Conflict& conflict : pad::Conflicts(bindings)) {
    REXLOG_WARN("Controller: {} drives {} actions; rebind in the settings",
                pad::InputName(conflict.physical), conflict.actions.size());
  }
  // Installed even for the default bindings: the wrapper also filters the Back+Start combo.
  config.input_factory = [bindings](bool tool_mode) -> std::unique_ptr<rex::system::IInputSystem> {
    return CreateRemappedInputSystem(tool_mode, bindings);
  };
}

RemappedDriver::RemappedDriver(std::unique_ptr<InputDriver> inner, pad::Bindings bindings)
    : InputDriver(nullptr, 0), inner_(std::move(inner)), bindings_(bindings) {}

X_STATUS RemappedDriver::Setup() {
  // The input system asks this driver whether the game window is active; the controller
  // driver inside asks it too, for the keystrokes it holds back.
  inner_->set_is_active_callback([this] { return is_active(); });
  return inner_->Setup();
}

void RemappedDriver::EnumerateDevices(std::vector<DeviceInfo>& out) {
  inner_->EnumerateDevices(out);
}

RemappedDriver::ComboPad& RemappedDriver::ComboPadFor(DeviceId id) {
  std::lock_guard lock(pads_mutex_);
  return pads_[id];  // std::map nodes stay put
}

uint16_t RemappedDriver::FilterCombo(DeviceId id, uint16_t physical_buttons) {
  return ComboPadFor(id).gate.Apply(physical_buttons, NowMs());
}

X_RESULT RemappedDriver::GetDeviceState(DeviceId id, X_INPUT_STATE* out) {
  const X_RESULT result = inner_->GetDeviceState(id, out);
  if (result == X_ERROR_SUCCESS && out) {
    // The combo filter works on the physical pad, before the player's bindings move anything.
    out->gamepad.buttons = FilterCombo(id, out->gamepad.buttons);
    FromPad(pad::Remap(ToPad(out->gamepad), bindings_), out->gamepad);
  }
  return result;
}

X_RESULT RemappedDriver::ComboKeystroke(DeviceId id, X_INPUT_KEYSTROKE* out) {
  X_INPUT_STATE state = {};
  if (inner_->GetDeviceState(id, &state) != X_ERROR_SUCCESS) {
    return X_ERROR_EMPTY;
  }
  const uint16_t shown = FilterCombo(id, state.gamepad.buttons) & settings::kOpenComboButtons;
  ComboPad& entry = ComboPadFor(id);
  const uint16_t changed = shown ^ entry.keys_sent;
  // Ups before downs, Start before Back, as the controller drivers order them.
  for (const bool down : {false, true}) {
    for (const auto [mask, key] :
         {std::pair{pad::kButtonStart, rex::ui::VirtualKey::kXInputPadStart},
          std::pair{pad::kButtonBack, rex::ui::VirtualKey::kXInputPadBack}}) {
      if (!(changed & mask) || bool(shown & mask) != down) continue;
      entry.keys_sent ^= mask;
      out->virtual_key = uint16_t(key);
      out->unicode = 0;
      out->user_index = 0;  // InputSystem stamps the guest user this device is assigned to
      out->hid_code = 0;
      out->flags = down ? X_INPUT_KEYSTROKE_KEYDOWN : X_INPUT_KEYSTROKE_KEYUP;
      return X_ERROR_SUCCESS;
    }
  }
  return X_ERROR_EMPTY;
}

X_RESULT RemappedDriver::GetDeviceCapabilities(DeviceId id, uint32_t flags,
                                               X_INPUT_CAPABILITIES* out) {
  return inner_->GetDeviceCapabilities(id, flags, out);
}

X_RESULT RemappedDriver::SetDeviceVibration(DeviceId id, X_INPUT_VIBRATION* vibration) {
  return inner_->SetDeviceVibration(id, vibration);
}

X_RESULT RemappedDriver::GetDeviceKeystroke(DeviceId id, uint32_t flags,
                                            X_INPUT_KEYSTROKE* out) {
  X_RESULT result = inner_->GetDeviceKeystroke(id, flags, out);
  if (out) {
    // Back and Start events come from the combo filter (ComboKeystroke), not from the controller
    // driver: its own would reach the game at once, whatever the filter holds back.
    const auto is_combo_key = [out] {
      const uint16_t key = out->virtual_key;
      return key == uint16_t(rex::ui::VirtualKey::kXInputPadStart) ||
             key == uint16_t(rex::ui::VirtualKey::kXInputPadBack);
    };
    for (int i = 0; i < 4 && result == X_ERROR_SUCCESS && is_combo_key(); ++i) {
      result = inner_->GetDeviceKeystroke(id, flags, out);
    }
    if (result == X_ERROR_SUCCESS && is_combo_key()) {
      result = X_ERROR_EMPTY;
    }
    if (result == X_ERROR_EMPTY) {
      result = ComboKeystroke(id, out);
    }
  }
  if (result == X_ERROR_SUCCESS && out) {
    const std::optional<uint16_t> key = pad::RemapKeystroke(out->virtual_key, bindings_);
    if (!key) {
      // The input drives no action: the game sees no keystroke this poll.
      return X_ERROR_EMPTY;
    }
    out->virtual_key = *key;
  }
  return result;
}

void RemappedDriver::OnWindowAvailable(rex::ui::Window* window) {
  inner_->OnWindowAvailable(window);
}

}  // namespace topspin4
