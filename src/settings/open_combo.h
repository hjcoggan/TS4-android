// The pad combo that opens the Settings page (ticket 07): hold Back and Start together.
//
// The game reads every button (docs: .scratch/game-settings/issues/07-pad-combo.md, Comments),
// so no single button is free. Back and Start rarely land in the same poll, so a lone one is
// filtered at the pad-state level (the controller driver's GetDeviceState, remapped_input.cpp, and
// the scripted pad's): only its bit is masked while it waits kComboWindowMs for the other, every
// other button and stick goes through untouched. Then:
//   - the other arrives in the window: the page opens and the game sees neither until both are up;
//   - the lone button is released in the window: it is replayed to the game, pressed for
//     kReplayMs after the release, so a quick tap (menu confirm, pause) is only late, not lost;
//   - it is still held when the window ends: it passes through from then on (a late partner opens
//     nothing).
// Clicking both sticks, WWE's choice, is ruled out: Top Spin 4 reads them.
//
// OpenComboFilter is pure. OpenComboGate adds the lock and the page-open notification.

#pragma once

#include <cstdint>
#include <functional>
#include <mutex>
#include <utility>

namespace topspin4::settings {

// XINPUT_GAMEPAD_BACK | XINPUT_GAMEPAD_START, as pad::kButtonBack and pad::kButtonStart.
inline constexpr uint16_t kOpenComboButtons = 0x0020 | 0x0010;

// How long a lone Back or Start is masked from the game, waiting for the other one.
inline constexpr uint64_t kComboWindowMs = 100;

// How long a button that was released inside the window, or just passed through, is shown to the
// game as pressed (about two polls at 60 Hz), so the game sees a tap.
inline constexpr uint64_t kReplayMs = 50;

inline bool OpenComboDown(uint16_t buttons) {
  return (buttons & kOpenComboButtons) == kOpenComboButtons;
}

// Decides, on each pad poll, which of Back and Start the game sees and whether the combo just
// completed.
class OpenComboFilter {
 public:
  struct Result {
    uint16_t game_buttons = 0;  // the Back/Start bits the game sees this poll
    bool open = false;          // the combo completed in this poll: open the page (once per press)
  };

  Result Update(uint16_t buttons, uint64_t now_ms) {
    const uint16_t combo = buttons & kOpenComboButtons;
    if (state_ == State::kReplay) {
      if (now_ms < until_ms_) return {held_, false};
      state_ = State::kIdle;  // a press made during the replay is judged from here
    }
    switch (state_) {
      case State::kIdle:
        if (combo == 0) return {};
        held_ = combo;
        if (OpenComboDown(combo)) {
          state_ = State::kOpened;
          return {0, true};
        }
        state_ = State::kWaiting;
        since_ms_ = now_ms;
        return {};
      case State::kWaiting:
        if (combo == 0) return Replay(now_ms + kReplayMs);  // released inside the window
        if (OpenComboDown(combo)) {
          state_ = State::kOpened;
          return {0, true};
        }
        if (combo != held_) {  // the lone button changed: judge the new one from now
          held_ = combo;
          since_ms_ = now_ms;
          return {};
        }
        if (now_ms - since_ms_ >= kComboWindowMs) {
          state_ = State::kPassed;
          since_ms_ = now_ms;
          return {combo, false};
        }
        return {};
      case State::kOpened:  // masked until both are up, so the page closing mid-hold is quiet
        if (combo == 0) state_ = State::kIdle;
        return {};
      case State::kPassed:  // the game has seen the button: no combo until all are up
        if (combo == 0) {
          // Released right after it passed: it still shows for kReplayMs in all.
          if (now_ms < since_ms_ + kReplayMs) return Replay(since_ms_ + kReplayMs);
          state_ = State::kIdle;
          return {};
        }
        held_ |= combo;
        return {combo, false};
      case State::kReplay:
        break;  // handled above
    }
    return {};
  }

 private:
  enum class State { kIdle, kWaiting, kOpened, kPassed, kReplay };

  Result Replay(uint64_t until_ms) {
    state_ = State::kReplay;
    until_ms_ = until_ms;
    return {held_, false};
  }

  State state_ = State::kIdle;
  uint16_t held_ = 0;  // the Back/Start bits being waited on, passed or replayed
  uint64_t since_ms_ = 0;
  uint64_t until_ms_ = 0;
};

// Who opens the page when the combo completes. Set by the Settings page; called on the thread that
// polls the pad.
inline std::mutex& OpenComboHandlerMutex() {
  static std::mutex mutex;
  return mutex;
}

inline std::function<void()>& OpenComboHandlerSlot() {
  static std::function<void()> handler;
  return handler;
}

inline void SetOpenComboHandler(std::function<void()> handler) {
  std::lock_guard lock(OpenComboHandlerMutex());
  OpenComboHandlerSlot() = std::move(handler);
}

inline void NotifyOpenCombo() {
  std::function<void()> handler;
  {
    std::lock_guard lock(OpenComboHandlerMutex());
    handler = OpenComboHandlerSlot();
  }
  if (handler) handler();
}

// One pad's filter, safe to poll from any thread.
class OpenComboGate {
 public:
  // The pad's buttons as the game sees them: Back and Start filtered, the rest as given. Calls
  // the handler when this poll completes the combo.
  uint16_t Apply(uint16_t buttons, uint64_t now_ms) {
    OpenComboFilter::Result result;
    {
      std::lock_guard lock(mutex_);
      result = filter_.Update(buttons, now_ms);
    }
    if (result.open) NotifyOpenCombo();
    return uint16_t((buttons & ~kOpenComboButtons) | result.game_buttons);
  }

 private:
  std::mutex mutex_;
  OpenComboFilter filter_;
};

}  // namespace topspin4::settings
