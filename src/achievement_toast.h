// The in-game "Achievement unlocked" toast, drawn in the manner of the Xbox 360 dashboard's: a
// dark capsule at the screen's lower middle with a green trophy badge, "Achievement unlocked"
// and "<points>G - <name>". It replaces the SDK's corner toast (ReXApp's
// CreateAchievementNotificationDialog). Drawn with ImGui shapes only: no Microsoft artwork. Each
// toast plays the sound ChooseSound picks (achievement_sound.h).

#pragma once

#include <filesystem>

#include <rex/ui/overlay/achievement_notification.h>

#include "achievement_sound.h"
#include "achievement_toast_queue.h"

struct ImFont;
struct ImFontAtlas;

namespace topspin4::achievement_toast {

// Adds the toast's fonts (Segoe UI: the console's own face is not ours to ship); where Windows
// has none (Proton), the toast draws with the font its constructor is given. Call from
// OnConfigureFonts.
void AddFonts(ImFontAtlas* atlas);

class Toast : public rex::ui::AchievementNotificationDialog {
 public:
  // `sound`: a WAV played as each toast shows (Windows; muted with --audio_mute), or a path that
  // does not exist for none.
  Toast(rex::ui::ImGuiDrawer* drawer, ImFont* fallback, std::filesystem::path sound);

  // Thread-safe: the runtime calls it from guest threads.
  void Push(const rex::system::AchievementEvent& event) override;

  // Timed by the clock, so a guest frame's paint is enough.
  bool PaintsWithGuestFrames() const override { return true; }

 protected:
  void OnDraw(ImGuiIO& io) override;

 private:
  ImFont* fallback_;
  std::filesystem::path sound_;
  Queue<rex::system::AchievementEvent> queue_;
};

}  // namespace topspin4::achievement_toast
