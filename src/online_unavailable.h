// Online menus with no Xbox LIVE (ticket 15). Xbox LIVE on the main menu, the way to Player Match
// and World Tour, asks through a system message box: "This option is only available with an
// Xbox LIVE-enabled gamer profile or as a guest. Do you want to sign-in ?" [YES | NO]. YES opens
// the sign-in UI (Swing 0x888C6928), which can never sign in to a LIVE that is gone. This box
// says so instead, with one OK, and the game gets NO: it returns to the main menu and never opens
// the sign-in UI.
// The prompt is recognised by its English text (the game's language is English unless
// user_language says otherwise); in another language the original prompt shows, and YES still
// returns to the menu (signin_ui_closes_unchanged).

#pragma once

#include <cstdint>
#include <optional>
#include <string_view>

#include <rex/kernel/xam/message_box.h>

namespace topspin4::online {

inline constexpr std::string_view kLivePromptStart =
    "This option is only available with an Xbox LIVE-enabled gamer profile";
inline constexpr std::string_view kUnavailableText =
    "Online features are not available yet. They are planned for future versions.";
inline constexpr uint32_t kAnswerNo = 1;  // the prompt's buttons are YES, NO

// The game's Xbox LIVE prompt becomes the unavailable message, answered NO; any other box
// is left alone.
inline std::optional<uint32_t> RewriteLivePrompt(rex::kernel::xam::TitleMessageBox& box) {
  if (!box.text.starts_with(kLivePromptStart) || box.buttons.size() != 2) {
    return std::nullopt;
  }
  box.text = std::string(kUnavailableText);
  box.buttons = {"OK"};
  box.active_button = 0;
  return kAnswerNo;
}

}  // namespace topspin4::online
