#include "achievement_toast.h"

#include <algorithm>
#include <cmath>
#include <filesystem>
#include <string>

#include <fmt/format.h>
#include <imgui.h>
#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/ui/imgui_drawer.h>

#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <mmsystem.h>
#pragma comment(lib, "winmm.lib")
#undef PlaySound
#endif

REXCVAR_DECLARE(bool, audio_mute);

#include "achievement_toast_timeline.h"
#include "settings/config_file.h"

namespace topspin4::achievement_toast {
namespace {

ImFont* g_title_font = nullptr;  // "Achievement unlocked"
ImFont* g_name_font = nullptr;   // "15G - Rookie"

ImFont* LoadFont(ImFontAtlas* atlas, const char* path) {
  std::error_code error;
  if (!std::filesystem::is_regular_file(path, error)) return nullptr;
  return atlas->AddFontFromFileTTF(path, 40.0f);
}

ImU32 Rgba(int r, int g, int b, float a) {
  return IM_COL32(r, g, b, static_cast<int>(std::clamp(a, 0.0f, 1.0f) * 255.0f + 0.5f));
}

// The trophy on the badge, white, in units of the badge's radius around its centre.
void Trophy(ImDrawList* draw, ImVec2 c, float r, float alpha) {
  const ImU32 white = Rgba(255, 255, 255, alpha);
  const auto at = [&](float x, float y) { return ImVec2(c.x + x * r, c.y + y * r); };
  // The cup: a flat top over a round bottom.
  draw->AddRectFilled(at(-0.40f, -0.50f), at(0.40f, -0.12f), white);
  draw->AddCircleFilled(at(0, -0.14f), 0.40f * r, white, 32);
  // The handles.
  draw->AddCircle(at(-0.40f, -0.30f), 0.17f * r, white, 20, 0.09f * r);
  draw->AddCircle(at(0.40f, -0.30f), 0.17f * r, white, 20, 0.09f * r);
  // Stem and foot.
  draw->AddRectFilled(at(-0.08f, 0.20f), at(0.08f, 0.46f), white);
  draw->AddRectFilled(at(-0.30f, 0.44f), at(0.30f, 0.60f), white, 0.05f * r);
}

// The green badge: a gradient-looking disc (darker rim, lighter body, a gloss across the top).
void Badge(ImDrawList* draw, ImVec2 c, float r, float alpha) {
  if (r <= 0.5f) return;
  draw->AddCircleFilled(c, r, Rgba(38, 104, 8, alpha), 48);
  draw->AddCircleFilled(c, r * 0.92f, Rgba(86, 170, 22, alpha), 48);
  draw->AddCircleFilled(ImVec2(c.x, c.y + r * 0.10f), r * 0.80f, Rgba(108, 196, 34, alpha), 48);
  draw->AddEllipseFilled(ImVec2(c.x, c.y - r * 0.45f), ImVec2(r * 0.70f, r * 0.38f),
                         Rgba(255, 255, 255, alpha * 0.28f), 0, 40);
  Trophy(draw, c, r * 0.82f, alpha);
}

// The player's own unlock sound, if they put one in; played without waiting, over the game's audio.
void PlaySound(const std::filesystem::path& sound) {
  if (sound.empty() || REXCVAR_GET(audio_mute)) return;
#ifdef _WIN32
  PlaySoundW(sound.c_str(), nullptr, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
#endif
}

}  // namespace

void AddFonts(ImFontAtlas* atlas) {
  g_title_font = LoadFont(atlas, "C:/Windows/Fonts/segoeui.ttf");
  g_name_font = LoadFont(atlas, "C:/Windows/Fonts/seguisb.ttf");
  if (!g_name_font) g_name_font = g_title_font;
  if (!g_title_font) REXLOG_INFO("Achievement toast: no Segoe UI, using the Settings page's font");
}

Toast::Toast(rex::ui::ImGuiDrawer* drawer, ImFont* fallback, std::filesystem::path sound)
    : AchievementNotificationDialog(drawer), fallback_(fallback), queue_(kDuration) {
  std::error_code error;
  if (!sound.empty() && std::filesystem::is_regular_file(sound, error)) {
    sound_ = std::move(sound);
    REXLOG_INFO("Achievement toast: sound {}", settings::Utf8(sound_));
  }
  // Registered only while a toast is queued (ReXApp adds it on each unlock): any registered
  // dialog makes the presenter paint on the UI thread instead of the guest output thread.
  drawer->RemoveDialog(this);
}

void Toast::Push(const rex::system::AchievementEvent& event) { queue_.Push(event); }

void Toast::OnDraw(ImGuiIO& io) {
  const auto showing = queue_.At(std::chrono::steady_clock::now());
  if (!showing) {
    imgui_drawer()->RemoveDialog(this);
    return;
  }
  if (showing->started) PlaySound(sound_);
  const rex::system::AchievementEvent& event = showing->event;
  const float age = showing->age;

  const Frame f = At(age);
  if (f.alpha <= 0) return;

  // Laid out on a 720-line screen and scaled to the window's height.
  const float s = io.DisplaySize.y / 720.0f;
  const float height = 60 * s, radius = height / 2;
  const float badge_r = (radius - 4 * s) * f.badge_scale;
  const float title_size = 17 * s, name_size = 22 * s;
  ImFont* title_font = g_title_font ? g_title_font : (fallback_ ? fallback_ : ImGui::GetFont());
  ImFont* name_font = g_name_font ? g_name_font : title_font;

  const std::string title = "Achievement unlocked";
  const std::string name =
      fmt::format("{}G - {}", event.achievement.gamerscore, event.achievement.label);
  const float text_w = std::max(title_font->CalcTextSizeA(title_size, FLT_MAX, 0, title.c_str()).x,
                                name_font->CalcTextSizeA(name_size, FLT_MAX, 0, name.c_str()).x);
  // A narrow window caps the width below the minimum; the cap wins (std::clamp needs lo <= hi).
  const float full_w = std::min(std::max(height + 18 * s + text_w + 30 * s, 320 * s),
                                std::max(io.DisplaySize.x - 80 * s, height));
  const float w = height + (full_w - height) * f.open;

  const float cx = io.DisplaySize.x / 2;
  const float bottom = io.DisplaySize.y * 0.88f;
  const ImVec2 a(cx - w / 2, bottom - height), b(cx + w / 2, bottom);

  ImDrawList* draw = ImGui::GetForegroundDrawList();
  // The capsule: a soft shadow, the dark body, a gloss over its upper half and a thin rim.
  if (f.open > 0) {
    draw->AddRectFilled(ImVec2(a.x + 2 * s, a.y + 4 * s), ImVec2(b.x + 2 * s, b.y + 4 * s),
                        Rgba(0, 0, 0, f.alpha * 0.35f), radius);
    draw->AddRectFilled(a, b, Rgba(22, 22, 24, f.alpha * 0.94f), radius);
    draw->AddRectFilled(ImVec2(a.x + 3 * s, a.y + 2 * s), ImVec2(b.x - 3 * s, a.y + height * 0.5f),
                        Rgba(255, 255, 255, f.alpha * 0.07f), radius - 2 * s,
                        ImDrawFlags_RoundCornersTop);
    draw->AddRect(a, b, Rgba(120, 120, 124, f.alpha * 0.55f), radius, 0, 1.5f * s);
  }

  const ImVec2 badge_c(a.x + radius, a.y + radius);
  if (f.text_alpha > 0) {
    draw->PushClipRect(a, b, true);
    const float x = badge_c.x + radius + 14 * s;
    const float top = a.y + (height - (title_size + name_size + 2 * s)) / 2 - 1 * s;
    draw->AddText(title_font, title_size, ImVec2(x, top), Rgba(214, 214, 214, f.alpha * f.text_alpha),
                  title.c_str());
    draw->AddText(name_font, name_size, ImVec2(x, top + title_size + 2 * s),
                  Rgba(255, 255, 255, f.alpha * f.text_alpha), name.c_str());
    draw->PopClipRect();
  }
  Badge(draw, badge_c, badge_r, f.alpha);
}

}  // namespace topspin4::achievement_toast
