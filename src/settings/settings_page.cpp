#include "settings/settings_page.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <chrono>
#include <string>
#include <vector>

#include <imgui.h>

#include <rex/cvar.h>
#include <rex/input/input_system.h>
#include <rex/logging.h>
#include <rex/ui/imgui_dialog.h>
#include <rex/ui/keybinds.h>

#include "native/native_renderer.h"
#include "settings/config_file.h"
#include "settings/registry.h"

namespace topspin4::settings {

namespace {

class PageDialog;

struct State {
  rex::ui::ImGuiDrawer* drawer = nullptr;
  rex::input::InputSystem* input = nullptr;
  std::filesystem::path settings_path;
  std::atomic<bool> hold = false;  // read on the guest thread
  PageDialog* dialog = nullptr;    // UI thread only
  std::function<void(std::function<void()>)> run_on_ui;
  SessionStart session;  // UI thread only; file_values follows what the page saves
  ImFont* heading = nullptr;
  ImFont* body = nullptr;
} g;

// Colours of the game's OPTIONS screens (as sampled for WWE All Stars; Top Spin 4's own are
// ticket 08).
constexpr ImU32 kWhite = IM_COL32(255, 255, 255, 255);
constexpr ImU32 kGold = IM_COL32(250, 200, 40, 255);
constexpr ImU32 kGrey = IM_COL32(140, 152, 175, 255);
constexpr ImU32 kDim = IM_COL32(85, 94, 112, 255);
constexpr ImU32 kLaunchOrange = IM_COL32(255, 150, 60, 255);
constexpr ImU32 kPanelTop = IM_COL32(6, 16, 48, 255), kPanelBottom = IM_COL32(2, 6, 22, 255);
constexpr ImU32 kBarTop = IM_COL32(40, 120, 230, 245), kBarBottom = IM_COL32(10, 55, 150, 245);
constexpr ImU32 kBarIdle = IM_COL32(14, 32, 78, 205);
constexpr ImU32 kRuleOrange = IM_COL32(255, 140, 20, 255);

// Maps the game's 1280x720 frame into the window the way Native shows the game: stretched to
// fill it. x follows the width, y and text sizes follow the height, so text keeps its shape.
struct Frame {
  float scale_x;
  float scale;
  ImVec2 At(float x, float y) const { return {x * scale_x, y * scale}; }
};

Frame GameFrame() {
  const ImVec2 size = ImGui::GetIO().DisplaySize;
  return {size.x / 1280.0f, size.y / 720.0f};
}

void Rule(ImDrawList* draw, const Frame& f, float y) {
  const ImU32 clear = IM_COL32(255, 140, 20, 0);
  const ImVec2 a = f.At(110, y), m = f.At(435, y + 3), b = f.At(760, y);
  draw->AddRectFilledMultiColor(a, {m.x, m.y}, clear, kRuleOrange, kRuleOrange, clear);
  draw->AddRectFilledMultiColor({m.x, a.y}, {b.x, m.y}, kRuleOrange, clear, clear, kRuleOrange);
}

void Text(ImDrawList* draw, ImFont* font, float size, const Frame& f, float x, float y, ImU32 color,
          const std::string& text, float wrap = 0) {
  draw->AddText(font ? font : ImGui::GetFont(), size * f.scale, f.At(x, y), color, text.c_str(),
                nullptr, wrap * f.scale_x);
}

float TextWidth(ImFont* font, float size, const Frame& f, const std::string& text) {
  font = font ? font : ImGui::GetFont();
  return font->CalcTextSizeA(size, FLT_MAX, 0, text.c_str()).x * f.scale / f.scale_x;
}

void Arrow(ImDrawList* draw, const Frame& f, float x, float y, bool left) {
  const float w = 12, h = 13;
  const ImVec2 tip = f.At(left ? x : x + w, y);
  const ImVec2 top = f.At(left ? x + w : x, y - h), bottom = f.At(left ? x + w : x, y + h);
  draw->AddTriangleFilled(tip, top, bottom, kGold);
}

uint64_t NowMs() {
  return uint64_t(std::chrono::duration_cast<std::chrono::milliseconds>(
                      std::chrono::steady_clock::now().time_since_epoch())
                      .count());
}

// The controller (user 0's real state under the hold; a scripted run's pad is one), plus the
// keyboard's arrows, Enter and Esc for players without one.
Pad ReadPad() {
  Pad pad;
  rex::input::X_INPUT_STATE state = {};
  if (g.input && g.input->HeldState(0, &state)) {
    pad.buttons = state.gamepad.buttons;
    pad.thumb_lx = state.gamepad.thumb_lx;
    pad.thumb_ly = state.gamepad.thumb_ly;
  }
  if (ImGui::IsKeyDown(ImGuiKey_UpArrow)) pad.buttons |= PageModel::kUp;
  if (ImGui::IsKeyDown(ImGuiKey_DownArrow)) pad.buttons |= PageModel::kDown;
  if (ImGui::IsKeyDown(ImGuiKey_LeftArrow)) pad.buttons |= PageModel::kLeft;
  if (ImGui::IsKeyDown(ImGuiKey_RightArrow)) pad.buttons |= PageModel::kRight;
  if (ImGui::IsKeyDown(ImGuiKey_Enter)) pad.buttons |= PageModel::kA;
  if (ImGui::IsKeyDown(ImGuiKey_Escape)) pad.buttons |= PageModel::kB;
  return pad;
}

// Carries out what the model says: a live change sets the cvar, a saved one goes to the
// Settings file (a next-start setting only there, so nothing half-applies).
void Apply(const SettingChange& change) {
  if (change.set_cvar && !rex::cvar::SetFlagByName(change.key, change.value)) {
    REXLOG_WARN("settings page: {} = {} was rejected", change.key, change.value);
  }
  if (change.save) {
    const Setting* setting = FindSetting(change.key);
    if (!setting || !SaveSetting(g.settings_path, change.key, TomlLiteral(*setting, change.value))) {
      REXLOG_WARN("settings page: could not save {} to {}", change.key, Utf8(g.settings_path));
    }
    g.session.file_values[change.key] = change.value;
  }
  REXLOG_INFO("settings page: {} = {} ({})", change.key, change.value,
              change.save ? (change.set_cvar ? "applied and saved" : "saved for the next start")
                          : (change.set_cvar ? "applied, not saved" : "not applied, not saved"));
}

SettingsModel BuildModel() {
  ModelContext context;
  context.renderer = std::string(topspin4::native::IsEnabled() ? kRendererNative : kRendererClassic);
  context.pinned = g.session.pinned;
  context.safe_start = g.session.safe_start;
  ValueMap in_effect;
  for (const Setting& setting : PlayerSettings()) {
    in_effect[std::string(setting.key)] = rex::cvar::GetFlagByName(setting.key);
  }
  return SettingsModel(std::move(context), g.session.file_values, std::move(in_effect));
}

// Registered with the ImGui drawer only while the page is open or waiting for the pad to be
// released: a registered dialog moves presents to the UI thread (docs/research/movement-judder.md).
class PageDialog final : public rex::ui::ImGuiDialog {
 public:
  explicit PageDialog(rex::ui::ImGuiDrawer* drawer) : ImGuiDialog(drawer), model_(BuildModel()) {
    model_.Open(NowMs());
    g.hold = true;
  }

  void CloseFromKey() { model_.Close(); }

 protected:
  void OnDraw(ImGuiIO&) override {
    for (const SettingChange& change : model_.Update(ReadPad(), NowMs())) Apply(change);
    g.hold = model_.holds_input();
    if (!model_.holds_input()) {
      Close();
      return;
    }
    if (model_.is_open()) DrawPage();
  }

  void OnClose() override {
    g.hold = false;
    g.dialog = nullptr;
  }

 private:
  void DrawPage() {
    ImDrawList* draw = ImGui::GetForegroundDrawList();
    const Frame f = GameFrame();
    draw->AddRectFilledMultiColor(f.At(90, 30), f.At(780, 714), kPanelTop, kPanelTop, kPanelBottom,
                                  kPanelBottom);
    draw->AddRect(f.At(90, 30), f.At(780, 714), IM_COL32(60, 110, 200, 160), 0, 0, 1.5f * f.scale);
    Text(draw, g.heading, 46, f, 118, 88, kWhite, "PC SETTINGS");
    Rule(draw, f, 140);

    const std::vector<Section>& sections = model_.model().sections();
    if (sections.size() > 1) {
      float x = 130;
      Text(draw, g.heading, 24, f, x, 150, kGrey, "LB");
      x += 44;
      for (size_t s = 0; s < sections.size(); ++s) {
        const std::string name(SectionName(sections[s]));
        Text(draw, g.heading, 24, f, x, 150, s == model_.section_index() ? kGold : kGrey, name);
        x += TextWidth(g.heading, 24, f, name) + 26;
      }
      Text(draw, g.heading, 24, f, x, 150, kGrey, "RB");
    } else {
      Text(draw, g.heading, 24, f, 130, 150, kGold, std::string(SectionName(model_.section())));
    }

    // Rows fill y 188..564 above the rule: 56 apart, closer with more than six.
    const std::vector<Row> rows = model_.rows();
    const float step = std::min(56.0f, 376.0f / float(std::max<size_t>(rows.size(), 1)));
    const float bar = step - 8;
    const float label_size = std::min(32.0f, bar - 6), value_size = std::min(30.0f, bar - 8);
    const float inset = (bar - label_size - 2) / 2;
    float y = 188;
    for (size_t i = 0; i < rows.size(); ++i) {
      const Row& row = rows[i];
      const bool selected = i == model_.selected_index();
      const ImVec2 a = f.At(120, y), b = f.At(750, y + bar);
      if (selected) {
        draw->AddRectFilledMultiColor(a, b, kBarTop, kBarTop, kBarBottom, kBarBottom);
        draw->AddLine(a, {b.x, a.y}, IM_COL32(150, 210, 255, 255), 2 * f.scale);
      } else {
        draw->AddRectFilled(a, b, kBarIdle);
      }
      ImU32 color = selected ? kGold : kGrey;
      if (!row.available) color = kDim;
      Text(draw, g.heading, label_size, f, 140, y + inset, color, row.label);
      std::string value = row.available ? row.value_label : "Unavailable";
      if (row.pending) value += " *";
      const float width = TextWidth(g.heading, value_size, f, value);
      ImU32 value_color = selected ? kWhite : kGrey;
      if (!row.available) value_color = kDim;
      if (row.mark == Mark::kLaunchOption || row.mark == Mark::kSafeStart) {
        value_color = kLaunchOrange;
      }
      Text(draw, g.heading, value_size, f, 600 - width / 2, y + inset + 1, value_color, value);
      if (selected && row.editable) {
        Arrow(draw, f, 492, y + bar / 2, true);
        Arrow(draw, f, 686, y + bar / 2, false);
      }
      y += step;
    }

    Rule(draw, f, 568);
    if (rows.empty()) return;
    const Row& row = rows[model_.selected_index()];
    Text(draw, g.body, 21, f, 118, 578, kWhite, row.help, 620);
    ImFont* body = g.body ? g.body : ImGui::GetFont();
    const float help_bottom =
        578 + body->CalcTextSizeA(21 * f.scale, FLT_MAX, 620 * f.scale_x, row.help.c_str()).y /
                  f.scale;
    const float note_y = std::max(640.0f, help_bottom + 6);
    if (!row.note.empty()) {
      Text(draw, g.body, 21, f, 118, note_y, row.pending ? kGold : kGrey, row.note, 620);
    }
  }

  PageModel model_;
};

// UI thread. The dialog deletes itself once input is released.
void OpenPage() {
  if (!g.dialog && g.drawer) g.dialog = new PageDialog(g.drawer);
}

void Toggle() {
  if (g.dialog) {
    g.dialog->CloseFromKey();
  } else {
    OpenPage();
  }
}

// Guest thread, on every pad poll: holds the game's input while the page is open.
bool HoldGuestInput() { return g.hold; }

}  // namespace

void BeginSession(SessionStart start) { g.session = std::move(start); }

void AddFonts(ImFontAtlas* atlas) {
  // Sizes cover a 1440p window without upscaling the glyphs much.
  const char* heading = "C:/Windows/Fonts/impact.ttf";
  const char* body = "C:/Windows/Fonts/segoeuib.ttf";
  if (std::filesystem::exists(heading)) g.heading = atlas->AddFontFromFileTTF(heading, 64.0f);
  if (std::filesystem::exists(body)) g.body = atlas->AddFontFromFileTTF(body, 40.0f);
}

void Configure(rex::ui::ImGuiDrawer* drawer, rex::input::InputSystem* input,
               const std::filesystem::path& settings_path,
               std::function<void(std::function<void()>)> run_on_ui) {
  g.drawer = drawer;
  g.input = input;
  g.settings_path = settings_path;
  g.run_on_ui = std::move(run_on_ui);
  if (input) input->SetGuestInputHold(HoldGuestInput);
  rex::ui::RegisterBind("bind_ts4_settings_page", "F4", "Open the PC SETTINGS page", Toggle);
}

bool RunScriptCommand(std::string_view name, std::string_view value) {
  if (name != kScriptKey) return false;
  if (value == "open") {
    OpenPage();
  } else if (value == "close") {
    if (g.dialog) g.dialog->CloseFromKey();
  } else if (value == "toggle") {
    Toggle();
  } else {
    REXLOG_WARN("script input: set {} wants open, close or toggle, not '{}'", name, value);
  }
  return true;
}

}  // namespace topspin4::settings
