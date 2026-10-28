// The Settings page as a pure model (ticket 03/04, spec "Settings page model"): no ImGui, no
// GPU, no cvars. Adapted from WWE All Stars' settings/page_model.h (ADR-0004, ADR-0007), and
// from wwe-svr2011-pc's GRAPHICS page navigation (KaikoClanworth1, used with the author's
// permission).
//
// Two parts:
//  * SettingsModel: the registry, the values the Settings file holds, the values in effect, the
//    settings pinned by Launch options and whether this is a Safe start in; rows (value,
//    choices, availability, mark) and, for a change, what to carry out (set the cvar now, save
//    to the file).
//  * PageModel: a controller snapshot and the time in; navigation, changes and the game's input
//    hold out.
// The ImGui page only draws these and carries the changes out (settings_page.cpp).

#pragma once

#include <algorithm>
#include <cctype>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <map>
#include <set>
#include <string>
#include <string_view>
#include <vector>

#include "settings/registry.h"
#include "settings/schema.h"

namespace topspin4::settings {

using ValueMap = std::map<std::string, std::string, std::less<>>;

// When a row's change takes effect, or why it does not follow the Settings file.
enum class Mark {
  kNow,           // a live setting: applies at once and is saved
  kNextStart,     // saved, applies the next time the game starts
  kLaunchOption,  // given on the command line or as REX_*: a change lasts until the game closes
  kSafeStart,     // Safe start session: a change is never saved
};

struct ChoiceView {
  std::string value;  // as the cvar parses it
  std::string label;  // as the page shows it
};

struct Row {
  const Setting* setting = nullptr;
  std::string key, label, help;
  Section section = Section::kGraphics;
  Mark mark = Mark::kNow;
  bool available = true;      // does anything on this session's renderer
  bool editable = true;       // the page can step it (not text, not unavailable)
  bool pending = false;       // kNextStart: the shown value is not the one in effect
  std::string value;          // shown
  std::string value_label;
  std::vector<ChoiceView> choices;
  size_t choice = 0;          // the index in choices of value
  std::string note;           // when the change applies; the reason if unavailable
};

// One setting's change, for the caller to carry out.
struct SettingChange {
  std::string key, value;
  bool set_cvar = false;  // set the cvar now
  bool save = false;      // write it to the Settings file
  bool operator==(const SettingChange&) const = default;
};

struct ModelContext {
  std::string renderer{kRendererClassic};  // the renderer this session draws with
  // Settings given as Launch options: they win over the file and are never saved.
  std::set<std::string, std::less<>> pinned;
  bool safe_start = false;
  // The sections the page shows, in order. Controls join when their rows can be edited.
  std::vector<Section> sections{Section::kGraphics};
  AppliesLookup applies = AppliesFromCvar;
};

inline std::string ChoiceLabel(const Setting& setting, std::string_view value) {
  if (setting.kind == Setting::Kind::kBool) return value == "true" ? "On" : "Off";
  if (value.empty()) return "Not set";
  if (setting.kind == Setting::Kind::kText) return std::string(value);
  std::string label(value);
  if (std::all_of(label.begin(), label.end(), [](char c) { return std::isdigit((unsigned char)c); })) {
    return label + "x";  // resolution scale, MSAA, anisotropic filtering
  }
  label[0] = char(std::toupper((unsigned char)label[0]));
  return label;
}

class SettingsModel {
 public:
  // file_values: what the Settings file holds (key to value; ignored in a Safe start).
  // in_effect: each setting's value now (the cvars').
  SettingsModel(ModelContext context, ValueMap file_values, ValueMap in_effect)
      : context_(std::move(context)), in_effect_(std::move(in_effect)) {
    for (const Setting* setting : SettingsInPageOrder()) {
      if (!Shown(setting->section)) continue;
      settings_.push_back(setting);
      std::string value = ValueOf(in_effect_, setting->key, setting->default_value);
      // A next-start setting shows what is saved, which is not yet in effect.
      if (!context_.safe_start && !Pinned(*setting) && AppliesNext(*setting)) {
        const auto saved = file_values.find(setting->key);
        if (saved != file_values.end() && setting->Allows(saved->second)) value = saved->second;
      }
      shown_[std::string(setting->key)] = std::move(value);
    }
  }

  const ModelContext& context() const { return context_; }
  const std::vector<Section>& sections() const { return context_.sections; }

  // The rows of one section, in page order.
  std::vector<Row> Rows(Section section) const {
    std::vector<Row> rows;
    for (const Setting* setting : settings_) {
      if (setting->section == section) rows.push_back(MakeRow(*setting));
    }
    return rows;
  }

  // The row for a key, or none (setting null) if the page does not show it.
  Row RowFor(std::string_view key) const {
    for (const Setting* setting : settings_) {
      if (setting->key == key) return MakeRow(*setting);
    }
    return {};
  }

  // Changes a setting to a value the registry allows; returns what to carry out, empty if the
  // value is refused or already shown. Presets (several settings at once) return several.
  std::vector<SettingChange> Change(std::string_view key, std::string_view value) {
    const Setting* setting = FindSetting(key);
    const auto shown = shown_.find(key);
    if (!setting || shown == shown_.end() || !setting->Allows(value) || shown->second == value) {
      return {};
    }
    shown->second = std::string(value);
    SettingChange change{std::string(key), std::string(value)};
    const bool now = !AppliesNext(*setting);
    if (Pinned(*setting)) {
      change.set_cvar = now;  // lasts until the game closes; never saved
    } else if (context_.safe_start) {
      change.set_cvar = now;
    } else {
      change.set_cvar = now;
      change.save = true;  // a next-start change is saved but not applied
    }
    if (change.set_cvar) in_effect_[std::string(key)] = change.value;
    return {change};
  }

  // The next (direction 1) or previous (-1) choice of an editable row, wrapping.
  std::vector<SettingChange> Step(std::string_view key, int direction) {
    const Row row = RowFor(key);
    if (!row.setting || !row.editable || row.choices.empty()) return {};
    const size_t count = row.choices.size();
    const size_t next = (row.choice + (direction < 0 ? count - 1 : 1)) % count;
    return Change(key, row.choices[next].value);
  }

 private:
  bool Shown(Section section) const {
    return std::find(context_.sections.begin(), context_.sections.end(), section) !=
           context_.sections.end();
  }
  bool Pinned(const Setting& setting) const {
    return context_.pinned.find(setting.key) != context_.pinned.end();
  }
  bool AppliesNext(const Setting& setting) const {
    return context_.applies(setting.key).value_or(Applies::kNextStart) == Applies::kNextStart;
  }
  static std::string ValueOf(const ValueMap& values, std::string_view key, std::string_view fallback) {
    const auto found = values.find(key);
    return found != values.end() ? found->second : std::string(fallback);
  }

  Row MakeRow(const Setting& setting) const {
    Row row;
    row.setting = &setting;
    row.key = std::string(setting.key);
    row.label = std::string(setting.label);
    row.help = std::string(setting.help);
    row.section = setting.section;
    row.value = shown_.at(row.key);
    const bool next = AppliesNext(setting);
    row.available = setting.AvailableOn(context_.renderer);
    row.editable = row.available && setting.kind != Setting::Kind::kText;
    if (Pinned(setting)) {
      row.mark = Mark::kLaunchOption;
    } else if (context_.safe_start) {
      row.mark = Mark::kSafeStart;
    } else {
      row.mark = next ? Mark::kNextStart : Mark::kNow;
    }
    row.pending = row.mark == Mark::kNextStart &&
                  row.value != ValueOf(in_effect_, setting.key, setting.default_value);
    if (setting.kind == Setting::Kind::kBool) {
      row.choices = {{"false", "Off"}, {"true", "On"}};
    } else if (setting.kind == Setting::Kind::kChoice) {
      for (std::string_view choice : setting.choices) {
        row.choices.push_back({std::string(choice), ChoiceLabel(setting, choice)});
      }
    }
    row.choice = row.choices.size();
    for (size_t i = 0; i < row.choices.size(); ++i) {
      if (row.choices[i].value == row.value) row.choice = i;
    }
    if (row.choice == row.choices.size()) {
      // A value no choice names is shown as it is.
      row.choices.insert(row.choices.begin(), {row.value, ChoiceLabel(setting, row.value)});
      row.choice = 0;
    }
    row.value_label = row.choices[row.choice].label;
    row.note = NoteFor(row, next);
    return row;
  }

  std::string NoteFor(const Row& row, bool next) const {
    if (!row.available) {
      return "Only the Native renderer uses this setting; this session draws with Classic.";
    }
    switch (row.mark) {
      case Mark::kLaunchOption:
        return next ? "Set by launch options: a change here does not apply at the next start."
                    : "Set by launch options: a change here lasts until the game closes.";
      case Mark::kSafeStart:
        return next ? "Safe start: a change here is not kept."
                    : "Safe start: a change here lasts until the game closes.";
      case Mark::kNextStart: return "Applies the next time the game starts.";
      case Mark::kNow: break;
    }
    return {};
  }

  ModelContext context_;
  ValueMap in_effect_;
  ValueMap shown_;
  std::vector<const Setting*> settings_;  // the shown ones, in page order
};

// One controller snapshot, in XInput's layout.
struct Pad {
  uint16_t buttons = 0;
  int16_t thumb_lx = 0;
  int16_t thumb_ly = 0;
};

class PageModel {
 public:
  static constexpr uint16_t kUp = 0x0001, kDown = 0x0002, kLeft = 0x0004, kRight = 0x0008;
  static constexpr uint16_t kStart = 0x0010, kBack = 0x0020, kA = 0x1000, kB = 0x2000;
  static constexpr uint16_t kLB = 0x0100, kRB = 0x0200;
  static constexpr int kStickDeadZone = 16000;
  static constexpr uint64_t kOpenGuardMs = 150;
  static constexpr uint64_t kRepeatDelayMs = 400;
  static constexpr uint64_t kRepeatIntervalMs = 130;

  explicit PageModel(SettingsModel model) : model_(std::move(model)) {}

  // Opens on the first row of the first section. Input stays held from the game until the page
  // closes and every button is released (the pad that opened the page cannot reach the game).
  void Open(uint64_t now_ms) {
    open_ = true;
    holding_ = true;
    waiting_release_ = true;
    opened_ms_ = now_ms;
    section_ = 0;
    row_ = 0;
    previous_ = 0;
  }

  // Closes the page from outside (its key pressed again). Input stays held until every button is
  // released, as when B closes it.
  void Close() { open_ = false; }

  // One poll of the pad. Returns the setting changes it made.
  std::vector<SettingChange> Update(const Pad& pad, uint64_t now_ms) {
    const uint16_t held = Held(pad);
    std::vector<SettingChange> changes;
    if (!holding_) return changes;
    if (waiting_release_) {
      if (held == 0 && now_ms >= opened_ms_ + kOpenGuardMs) waiting_release_ = false;
      previous_ = held;
      return changes;
    }
    if (!open_) {
      if (held == 0) holding_ = false;  // released after closing: the game gets the pad back
      previous_ = held;
      return changes;
    }
    const uint16_t pressed = held & ~previous_;
    if (pressed & (kB | kStart | kBack)) {
      open_ = false;
      previous_ = held;
      return changes;
    }
    const uint16_t directions = held & (kUp | kDown | kLeft | kRight);
    uint16_t act = pressed & (kUp | kDown | kLeft | kRight | kA);
    if (directions != (previous_ & (kUp | kDown | kLeft | kRight))) {
      repeat_at_ms_ = now_ms + kRepeatDelayMs;  // a new direction starts its delay
    } else if (directions && now_ms >= repeat_at_ms_) {
      act |= directions;
      repeat_at_ms_ = now_ms + kRepeatIntervalMs;
    }
    previous_ = held;
    const std::vector<Section>& sections = model_.sections();
    if (sections.empty()) return changes;
    if (pressed & (kLB | kRB)) {
      if (pressed & kLB) section_ = (section_ + sections.size() - 1) % sections.size();
      if (pressed & kRB) section_ = (section_ + 1) % sections.size();
      row_ = 0;
      return changes;
    }
    const std::vector<Row> rows = this->rows();
    if (rows.empty()) return changes;
    if (act & kUp) row_ = (row_ + rows.size() - 1) % rows.size();
    if (act & kDown) row_ = (row_ + 1) % rows.size();
    const Row& selected = rows[row_];
    if (act & kLeft) changes = model_.Step(selected.key, -1);
    if (act & (kRight | kA)) changes = model_.Step(selected.key, 1);
    return changes;
  }

  bool is_open() const { return open_; }
  // Whether the game must see an idle controller.
  bool holds_input() const { return holding_; }
  const SettingsModel& model() const { return model_; }
  Section section() const { return model_.sections()[section_]; }
  size_t section_index() const { return section_; }
  std::vector<Row> rows() const { return model_.Rows(section()); }
  size_t selected_index() const { return row_; }

 private:
  // Buttons plus the left stick as D-pad directions.
  static uint16_t Held(const Pad& pad) {
    uint16_t held = pad.buttons;
    if (pad.thumb_ly > kStickDeadZone) held |= kUp;
    if (pad.thumb_ly < -kStickDeadZone) held |= kDown;
    if (pad.thumb_lx < -kStickDeadZone) held |= kLeft;
    if (pad.thumb_lx > kStickDeadZone) held |= kRight;
    return held;
  }

  SettingsModel model_;
  size_t section_ = 0;
  size_t row_ = 0;
  bool open_ = false;
  bool holding_ = false;
  bool waiting_release_ = false;
  uint64_t opened_ms_ = 0;
  uint64_t repeat_at_ms_ = 0;
  uint16_t previous_ = 0;
};

}  // namespace topspin4::settings
