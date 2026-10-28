#include "settings/settings_page.h"

#include <algorithm>
#include <atomic>
#include <cfloat>
#include <chrono>
#include <mutex>
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
#include "settings/open_combo.h"
#include "settings/options_row_sink.h"
#include "settings/page_style.h"
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
  pad::Bindings bindings = pad::Bindings::Default();  // what the game's input system was built with
} g;

// The page's colours are Top Spin 4's (settings/page_style.h).
constexpr const style::Palette& kPalette = style::kTopSpinPalette;
constexpr ImU32 Col(style::Rgba color) { return color.Packed(); }

// Maps the game's 1280x720 frame into the window the way Native shows the game: stretched to
// fill it. x follows the width, y and text sizes follow the height (so a 1280x800 window scales
// the text by 800/720 and the rows still fit), and text keeps its shape.
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
  const ImU32 line = Col(kPalette.rule), clear = Col(kPalette.rule.WithAlpha(0));
  const ImVec2 a = f.At(110, y), m = f.At(435, y + 2), b = f.At(760, y);
  draw->AddRectFilledMultiColor(a, {m.x, m.y}, clear, line, line, clear);
  draw->AddRectFilledMultiColor({m.x, a.y}, {b.x, m.y}, line, clear, clear, line);
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

// Text no wider than max_width (frame units): shrunk to fit, with an optional shadow, which the
// game draws under the white text of its selected row.
void FitText(ImDrawList* draw, ImFont* font, float size, const Frame& f, float x, float y,
             float max_width, ImU32 color, const std::string& text, const style::Rgba* shadow = nullptr) {
  const float fit = style::FitSize(size, TextWidth(font, size, f, text), max_width);
  if (shadow) Text(draw, font, fit, f, x + 1.2f, y + 1.5f, Col(*shadow), text);
  Text(draw, font, fit, f, x, y, color, text);
}

// The width the text takes once fitted to max_width.
float FittedWidth(ImFont* font, float size, const Frame& f, float max_width, const std::string& text) {
  const float width = TextWidth(font, size, f, text);
  return width * style::FitSize(size, width, max_width) / size;
}

void Arrow(ImDrawList* draw, const Frame& f, float x, float y, bool left, ImU32 color) {
  const float w = 12, h = 13;
  const ImVec2 tip = f.At(left ? x : x + w, y);
  const ImVec2 top = f.At(left ? x + w : x, y - h), bottom = f.At(left ? x + w : x, y + h);
  draw->AddTriangleFilled(tip, top, bottom, color);
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
    // The held state is what the game would see, after the player's remap; the page works on the
    // physical pad, so a remap cannot move its own navigation (ticket 06).
    topspin4::pad::Pad game;
    game.buttons = state.gamepad.buttons;
    game.left_trigger = state.gamepad.left_trigger;
    game.right_trigger = state.gamepad.right_trigger;
    game.thumb_lx = state.gamepad.thumb_lx;
    game.thumb_ly = state.gamepad.thumb_ly;
    const topspin4::pad::Pad physical = topspin4::pad::Unmap(game, g.bindings);
    pad.buttons = physical.buttons;
    pad.thumb_lx = physical.thumb_lx;
    pad.thumb_ly = physical.thumb_ly;
  }
  if (ImGui::IsKeyDown(ImGuiKey_UpArrow)) pad.buttons |= PageModel::kUp;
  if (ImGui::IsKeyDown(ImGuiKey_DownArrow)) pad.buttons |= PageModel::kDown;
  if (ImGui::IsKeyDown(ImGuiKey_LeftArrow)) pad.buttons |= PageModel::kLeft;
  if (ImGui::IsKeyDown(ImGuiKey_RightArrow)) pad.buttons |= PageModel::kRight;
  if (ImGui::IsKeyDown(ImGuiKey_Enter)) pad.buttons |= PageModel::kA;
  if (ImGui::IsKeyDown(ImGuiKey_Escape)) pad.buttons |= PageModel::kB;
  return pad;
}

// The SDK's name for a key that went down this frame, with the modifiers held ("Shift+W"), or
// empty. Only keys the SDK can name and read back are offered; Escape cancels and F4 is the page's
// own key, so neither can be bound.
std::string PressedKeyBinding() {
  struct Mapped {
    ImGuiKey key;
    rex::ui::VirtualKey vk;
  };
  static const std::vector<Mapped> keys = [] {
    std::vector<Mapped> list;
    auto add = [&list](ImGuiKey key, uint16_t vk) {
      list.push_back({key, static_cast<rex::ui::VirtualKey>(vk)});
    };
    for (int i = 0; i < 26; ++i) add(ImGuiKey(ImGuiKey_A + i), uint16_t('A' + i));
    for (int i = 0; i < 10; ++i) add(ImGuiKey(ImGuiKey_0 + i), uint16_t('0' + i));
    for (int i = 0; i < 12; ++i) {
      if (i != 3) add(ImGuiKey(ImGuiKey_F1 + i), uint16_t(0x70 + i));  // F4 opens the page
    }
    add(ImGuiKey_LeftArrow, 0x25);   add(ImGuiKey_UpArrow, 0x26);
    add(ImGuiKey_RightArrow, 0x27);  add(ImGuiKey_DownArrow, 0x28);
    add(ImGuiKey_Space, 0x20);       add(ImGuiKey_Enter, 0x0D);
    add(ImGuiKey_Tab, 0x09);         add(ImGuiKey_Backspace, 0x08);
    add(ImGuiKey_Insert, 0x2D);      add(ImGuiKey_Delete, 0x2E);
    add(ImGuiKey_Home, 0x24);        add(ImGuiKey_End, 0x23);
    add(ImGuiKey_PageUp, 0x21);      add(ImGuiKey_PageDown, 0x22);
    add(ImGuiKey_Semicolon, 0xBA);   add(ImGuiKey_Apostrophe, 0xDE);
    add(ImGuiKey_Comma, 0xBC);       add(ImGuiKey_Period, 0xBE);
    add(ImGuiKey_Minus, 0xBD);       add(ImGuiKey_Equal, 0xBB);
    add(ImGuiKey_Slash, 0xBF);       add(ImGuiKey_Backslash, 0xDC);
    add(ImGuiKey_LeftBracket, 0xDB); add(ImGuiKey_RightBracket, 0xDD);
    add(ImGuiKey_GraveAccent, 0xC0);
    return list;
  }();
  for (const Mapped& mapped : keys) {
    if (!ImGui::IsKeyPressed(mapped.key, false)) continue;
    std::string name = rex::ui::VirtualKeyToString(mapped.vk);
    if (name.empty() || rex::ui::ParseVirtualKey(name) != mapped.vk) continue;
    const ImGuiIO& io = ImGui::GetIO();
    return std::string(io.KeyCtrl ? "Ctrl+" : "") + (io.KeyAlt ? "Alt+" : "") +
           (io.KeyShift ? "Shift+" : "") + name;
  }
  return {};
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
  context.sections = {Section::kGraphics, Section::kControls};
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

  // A change from a script, through the model this page shows, so its rows follow.
  std::vector<SettingChange> Change(std::string_view key, std::string_view value) {
    return model_.Change(key, value);
  }

 protected:
  void OnDraw(ImGuiIO&) override {
    // A key binding row waiting for a key takes the first key pressed after A began the wait.
    const bool was_capturing = model_.capturing();
    for (const SettingChange& change : model_.Update(ReadPad(), NowMs())) Apply(change);
    if (was_capturing && model_.capturing()) {
      const std::string binding = PressedKeyBinding();
      if (!binding.empty()) {
        for (const SettingChange& change : model_.BindKey(binding)) Apply(change);
      }
    }
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
    const ImU32 panel_top = Col(kPalette.panel_top), panel_bottom = Col(kPalette.panel_bottom);
    draw->AddRectFilledMultiColor(f.At(90, 30), f.At(780, 714), panel_top, panel_top, panel_bottom,
                                  panel_bottom);
    draw->AddRect(f.At(90, 30), f.At(780, 714), Col(kPalette.panel_edge), 0, 0, 1.5f * f.scale);
    const style::Rgba shadow = kPalette.selected_shadow;
    FitText(draw, g.heading, 44, f, 118, 78, 630, Col(kPalette.heading), "PC SETTINGS", &shadow);
    Rule(draw, f, 140);

    const std::vector<Section>& sections = model_.model().sections();
    const ImU32 tab_idle = Col(kPalette.tab_idle), tab_active = Col(kPalette.tab_active);
    if (sections.size() > 1) {
      float x = 130;
      Text(draw, g.heading, 24, f, x, 148, tab_idle, "LB");
      x += 48;
      for (size_t s = 0; s < sections.size(); ++s) {
        const std::string name(SectionName(sections[s]));
        Text(draw, g.heading, 24, f, x, 148, s == model_.section_index() ? tab_active : tab_idle, name);
        x += TextWidth(g.heading, 24, f, name) + 28;
      }
      Text(draw, g.heading, 24, f, x, 148, tab_idle, "RB");
    } else {
      Text(draw, g.heading, 24, f, 130, 148, tab_active, std::string(SectionName(model_.section())));
    }

    // Rows fill y 188..564 above the rule: 56 apart, closer with seven; a longer list (Controls)
    // scrolls to keep the selected row in view.
    const std::vector<Row> rows = model_.rows();
    constexpr size_t kVisible = 7;
    const size_t shown = std::min(rows.size(), kVisible);
    const size_t selected_row = std::min(model_.selected_index(), rows.empty() ? 0 : rows.size() - 1);
    const size_t first =
        rows.size() <= kVisible
            ? 0
            : std::min(selected_row > kVisible / 2 ? selected_row - kVisible / 2 : 0,
                       rows.size() - kVisible);
    const float step = std::min(56.0f, 376.0f / float(std::max<size_t>(shown, 1)));
    const float bar = step - 8;
    // Labels take x 140..440 and values 470..710 between the arrows; either shrinks to fit.
    const float label_size = std::min(28.0f, bar - 8), value_size = std::min(28.0f, bar - 8);
    const float inset = (bar - label_size * 1.2f) / 2;
    constexpr float kLabelX = 140, kLabelWidth = 300, kValueCenter = 590, kValueWidth = 240;
    float y = 188;
    if (rows.size() > kVisible) {
      Text(draw, g.body, 21, f, 640, 152, tab_idle,
           std::to_string(selected_row + 1) + " / " + std::to_string(rows.size()));
    }
    for (size_t i = first; i < first + shown; ++i) {
      const Row& row = rows[i];
      const bool selected = i == selected_row;
      const bool waiting = model_.capturing() && model_.capture_key() == row.key;
      const ImVec2 a = f.At(120, y), b = f.At(750, y + bar);
      if (selected) {
        const ImU32 top = Col(kPalette.bar_top), bottom = Col(kPalette.bar_bottom);
        draw->AddRectFilledMultiColor(a, b, top, top, bottom, bottom);
        draw->AddRect(a, b, Col(kPalette.bar_edge), 0, 0, 1.5f * f.scale);
      } else {
        const ImU32 top = Col(kPalette.idle_top), bottom = Col(kPalette.idle_bottom);
        draw->AddRectFilledMultiColor(a, b, top, top, bottom, bottom);
      }
      // The game's selected row is white text with a shadow on orange; the others are dark text
      // on white.
      const style::Rgba* text_shadow = selected ? &shadow : nullptr;
      ImU32 color = Col(selected ? kPalette.selected_text : kPalette.idle_text);
      if (!row.available) color = Col(selected ? kPalette.selected_text.WithAlpha(190) : kPalette.disabled_text);
      std::string value = row.available ? row.value_label : "Unavailable";
      if (row.pending) value += " *";
      if (waiting) value = "Press a key...";
      ImU32 value_color = Col(selected ? kPalette.selected_text : kPalette.idle_value);
      if (!row.available) value_color = color;
      if (!selected && (row.mark == Mark::kLaunchOption || row.mark == Mark::kSafeStart)) {
        value_color = Col(kPalette.launch_mark);
      }
      // Clipped to the bar as a last resort, so no text ever spills over a neighbouring row.
      draw->PushClipRect(a, b, true);
      FitText(draw, g.heading, label_size, f, kLabelX, y + inset, kLabelWidth, color, row.label,
              text_shadow);
      const float width = FittedWidth(g.heading, value_size, f, kValueWidth, value);
      FitText(draw, g.heading, value_size, f, kValueCenter - width / 2, y + inset, kValueWidth,
              value_color, value, text_shadow);
      draw->PopClipRect();
      if (selected && row.editable && !row.capture && row.kind != RowKind::kAction) {
        const ImU32 arrow = Col(kPalette.selected_text);
        Arrow(draw, f, 452, y + bar / 2, true, arrow);
        Arrow(draw, f, 716, y + bar / 2, false, arrow);
      }
      y += step;
    }

    Rule(draw, f, 568);
    if (rows.empty()) return;
    const Row& row = rows[selected_row];
    std::string help = row.help;
    if (row.capture) help += " Press A, then the key to use (Esc or B cancels).";
    if (row.kind == RowKind::kAction) help += " Press A to reset.";
    // The help and the note under it must end above the panel's edge (y 714): the size drops a
    // point at a time until both fit, so a long description or note is never cut off.
    ImFont* body = g.body ? g.body : ImGui::GetFont();
    auto height = [&](float size, const std::string& text) {
      if (text.empty()) return 0.0f;
      return body->CalcTextSizeA(size * f.scale, FLT_MAX, 620 * f.scale_x, text.c_str()).y / f.scale;
    };
    constexpr float kTextTop = 578, kTextBottom = 706, kGap = 6;
    float size = 21;
    while (size > 14 && kTextTop + height(size, help) + (row.note.empty() ? 0 : kGap) +
                                height(size, row.note) > kTextBottom) {
      size -= 1;
    }
    Text(draw, g.body, size, f, 118, kTextTop, Col(kPalette.heading), help, 620);
    if (!row.note.empty()) {
      Text(draw, g.body, size, f, 118, kTextTop + height(size, help) + kGap,
           Col(row.pending ? kPalette.note_pending : kPalette.note), row.note, 620);
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

// Guest thread. Holds the game's input at once, so the press that asked never reaches the game
// (the dialog that follows also holds), and opens the page on the UI thread.
void RequestOpenFromGuest() {
  if (!g.drawer || !g.run_on_ui) return;
  if (g.hold.exchange(true)) return;  // already open, or opening
  g.run_on_ui([] {
    OpenPage();
    if (!g.dialog) g.hold = false;
  });
}

// Guest thread, on every pad poll: holds the game's input while the page is open. The pad combo is
// not decided here: the pad drivers filter it (open_combo.h) and call OnOpenCombo.
bool HoldGuestInput() { return g.hold; }

// Guest thread, from the pad driver's poll: Back and Start are both down.
void OnOpenCombo() {
  if (g.hold) return;
  REXLOG_INFO("settings page: opened with the pad combo (Back + Start)");
  RequestOpenFromGuest();
}

void RequestOptionsRowOpen() {
  REXLOG_INFO("settings page: opened from the OPTIONS row");
  RequestOpenFromGuest();
}

void LogOptionsRow(const char* message) { REXLOG_INFO("{}", message); }

}  // namespace

void BeginSession(SessionStart start) { g.session = std::move(start); }

void SetGamePadBindings(const pad::Bindings& bindings) { g.bindings = bindings; }

std::string CurrentPreset() {
  ValueMap in_effect;
  for (const Setting& setting : PlayerSettings()) {
    in_effect[std::string(setting.key)] = rex::cvar::GetFlagByName(setting.key);
  }
  return ActivePreset(in_effect,
                      topspin4::native::IsEnabled() ? kRendererNative : kRendererClassic);
}

std::string SessionReportJson() {
  return SessionSettingsJson(g.session.safe_start, CurrentPreset());
}

void AddFonts(ImFontAtlas* atlas, const std::filesystem::path& game_data_root) {
  // The game's own UI font from the player's data; if it is missing, Segoe UI Bold when Windows
  // has it, else ImGui's built-in font (the page draws with whatever it got). Sizes cover a 1440p
  // window without upscaling the glyphs much.
  std::error_code error;
  std::filesystem::path file = style::GameFontPath(game_data_root);
  if (game_data_root.empty() || !std::filesystem::is_regular_file(file, error)) {
    REXLOG_WARN("Settings page: the game's font {} is not there, using a fallback", Utf8(file));
    file = "C:/Windows/Fonts/segoeuib.ttf";
    if (!std::filesystem::is_regular_file(file, error)) {
      REXLOG_WARN("Settings page: no fallback font either, using ImGui's built-in one");
      return;
    }
  }
  const std::string path = Utf8(file);
  g.heading = atlas->AddFontFromFileTTF(path.c_str(), 64.0f);
  g.body = atlas->AddFontFromFileTTF(path.c_str(), 40.0f);
  REXLOG_INFO("Settings page: font {}", path);
}

void Configure(rex::ui::ImGuiDrawer* drawer, rex::input::InputSystem* input,
               const std::filesystem::path& settings_path,
               std::function<void(std::function<void()>)> run_on_ui) {
  g.drawer = drawer;
  g.input = input;
  g.settings_path = settings_path;
  g.run_on_ui = std::move(run_on_ui);
  if (input) input->SetGuestInputHold(HoldGuestInput);
  SetOpenComboHandler(OnOpenCombo);
  // The OPTIONS row's hooks (options_row_hooks.cpp, in the Swing Module's DLL) reach the page here.
  options_row::RowSink& row_sink = options_row::GetRowSink();
  row_sink.open_page = RequestOptionsRowOpen;
  row_sink.log = LogOptionsRow;
  rex::ui::RegisterBind("bind_ts4_settings_page", "F4", "Open the PC SETTINGS page", Toggle);
}

bool SetPlayerSettingFromScript(std::string_view key, std::string_view value) {
  const Setting* setting = FindSetting(key);
  if (!setting) return false;
  // The open page's model if there is one (its rows follow); else one built from this session.
  const std::vector<SettingChange> changes =
      g.dialog ? g.dialog->Change(key, value) : BuildModel().Change(key, value);
  for (const SettingChange& change : changes) Apply(change);
  if (changes.empty()) {
    if (setting->Allows(value)) {
      REXLOG_INFO("script input: set {} = {} changed nothing (already so, or fixed this session)",
                  key, value);
    } else {
      REXLOG_WARN("script input: set {} = {} was rejected, the setting does not allow it", key, value);
    }
  }
  return true;
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
