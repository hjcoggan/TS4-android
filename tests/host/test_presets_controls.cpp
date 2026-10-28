// src/settings/page_model.h and presets.h: Graphics Presets (ticket 05), Safe start (ticket 09)
// and the Controls section (ticket 06). No ImGui, no GPU.

#include <catch_amalgamated.hpp>

#include <set>

#include "pad_bindings.h"
#include "settings/page_model.h"

using namespace topspin4::settings;

namespace {

std::optional<Applies> Lifecycles(std::string_view key) {
  return key == "ts4_renderer" || key.starts_with("ts4_pad_") ? Applies::kNextStart : Applies::kNow;
}

ValueMap Defaults() {
  ValueMap values;
  for (const Setting& setting : PlayerSettings()) {
    values[std::string(setting.key)] = std::string(setting.default_value);
  }
  return values;
}

ValueMap WithValues(std::initializer_list<std::pair<const char*, const char*>> changes) {
  ValueMap values = Defaults();
  for (const auto& [key, value] : changes) values[key] = value;
  return values;
}

ModelContext Context(std::string_view renderer = kRendererNative) {
  ModelContext context;
  context.renderer = std::string(renderer);
  context.applies = Lifecycles;
  return context;
}

ModelContext ControlsContext() {
  ModelContext context = Context();
  context.sections = {Section::kGraphics, Section::kControls};
  return context;
}

const ValueMap kEnhancedValues = {{"ts4_resolution_scale", "auto"},
                                  {"ts4_msaa", "4"},
                                  {"ts4_anisotropic_filtering", "16"},
                                  {"ts4_sharpening", "off"}};

SettingChange* Find(std::vector<SettingChange>& changes, std::string_view key) {
  for (SettingChange& change : changes) {
    if (change.key == key) return &change;
  }
  return nullptr;
}

}  // namespace

// ---- Presets (ticket 05) ----

TEST_CASE("All defaults read Original on the Preset row, the first row of Graphics") {
  SettingsModel model(Context(), {}, Defaults());
  const Row row = model.Rows(Section::kGraphics).front();
  CHECK(row.kind == RowKind::kPreset);
  CHECK(row.label == "Graphics preset");
  CHECK(row.value == "original");
  CHECK(row.value_label == "Original");
  REQUIRE(row.choices.size() == 3);  // Original, Enhanced, Custom
  CHECK(row.choices[1].label == "Enhanced");
  CHECK(row.choices[2].label == "Custom");
  CHECK(row.mark == Mark::kNow);
  CHECK(row.help.find("Enhanced: Resolution scale Auto, MSAA 4x, Anisotropic filtering 16x") !=
        std::string::npos);
}

TEST_CASE("Choosing Enhanced sets its four settings at once, saved and applied") {
  SettingsModel model(Context(), {}, Defaults());
  const auto changes = model.Change(kPresetKey, "enhanced");
  REQUIRE(changes.size() == 3);  // sharpening is off already
  for (const SettingChange& change : changes) {
    CHECK(change.set_cvar);
    CHECK(change.save);
    CHECK(kEnhancedValues.at(change.key) == change.value);
  }
  for (const auto& [key, value] : kEnhancedValues) CHECK(model.RowFor(key).value == value);
  CHECK(model.RowFor(kPresetKey).value == "enhanced");
  CHECK(model.Change(kPresetKey, "enhanced").empty());
  CHECK(model.Change(kPresetKey, "custom").empty());  // Custom is shown, never chosen
}

TEST_CASE("Changing one covered setting reads Custom; returning to the Preset's values reads it again") {
  SettingsModel model(Context(), {}, Defaults());
  model.Change(kPresetKey, "enhanced");
  model.Change("ts4_msaa", "8");
  const Row row = model.RowFor(kPresetKey);
  CHECK(row.value == "custom");
  CHECK(row.value_label == "Custom");
  model.Change("ts4_msaa", "4");
  CHECK(model.RowFor(kPresetKey).value == "enhanced");
  // Back to the original values by hand, without touching the Preset row.
  for (const char* key : {"ts4_resolution_scale", "ts4_msaa", "ts4_anisotropic_filtering"}) {
    model.Change(key, std::string(FindSetting(key)->default_value));
  }
  CHECK(model.RowFor(kPresetKey).value == "original");
  // A setting no Preset covers does not matter.
  model.Change("ts4_renderer", "native");
  CHECK(model.RowFor(kPresetKey).value == "original");
}

TEST_CASE("The Preset row steps between Original and Enhanced, never onto Custom") {
  SettingsModel model(Context(), {}, WithValues({{"ts4_msaa", "8"}}));
  CHECK(model.RowFor(kPresetKey).value == "custom");
  CHECK(model.Step(kPresetKey, 1).size() == 1);  // Custom steps on to Original: msaa goes back
  CHECK(model.RowFor(kPresetKey).value == "original");
  model.Step(kPresetKey, 1);
  CHECK(model.RowFor(kPresetKey).value == "enhanced");
  model.Step(kPresetKey, 1);
  CHECK(model.RowFor(kPresetKey).value == "original");
  model.Step(kPresetKey, -1);
  CHECK(model.RowFor(kPresetKey).value == "enhanced");
  model.Change("ts4_msaa", "8");
  model.Step(kPresetKey, -1);  // Custom steps back to the last Preset
  CHECK(model.RowFor(kPresetKey).value == "enhanced");
}

TEST_CASE("On Classic, Enhanced's Native-only parts are unavailable and do not make the row Custom") {
  SettingsModel model(Context(kRendererClassic), {}, Defaults());
  CHECK(model.RowFor(kPresetKey).value == "original");
  // Native-only settings at other values do not matter on Classic.
  SettingsModel odd(Context(kRendererClassic), {},
                    WithValues({{"ts4_msaa", "8"}, {"ts4_resolution_scale", "3"}}));
  CHECK(odd.RowFor(kPresetKey).value == "original");
  // The two filtering settings alone decide.
  SettingsModel enhanced(Context(kRendererClassic), {},
                         WithValues({{"ts4_anisotropic_filtering", "16"}}));
  CHECK(enhanced.RowFor(kPresetKey).value == "enhanced");
  CHECK_FALSE(model.RowFor("ts4_msaa").available);
  CHECK(model.RowFor(kPresetKey).note.find("Resolution scale and MSAA need the Native renderer") !=
        std::string::npos);
  // Choosing Enhanced still sets all four, so a later Native start matches.
  CHECK(model.Change(kPresetKey, "enhanced").size() == 3);
  CHECK(model.RowFor(kPresetKey).value == "enhanced");
  // On Native the Preset row carries no such note.
  SettingsModel native(Context(), {}, Defaults());
  CHECK(native.RowFor(kPresetKey).note.empty());
}

TEST_CASE("A Preset row is marked by its settings: Launch option, Safe start") {
  ModelContext pinned = Context();
  pinned.pinned = {"ts4_msaa"};
  SettingsModel model(pinned, {}, Defaults());
  CHECK(model.RowFor(kPresetKey).mark == Mark::kLaunchOption);
  for (const SettingChange& change : model.Change(kPresetKey, "enhanced")) {
    CHECK(change.save == (change.key != "ts4_msaa"));
  }
  ModelContext safe = Context();
  safe.safe_start = true;
  CHECK(SettingsModel(safe, {}, Defaults()).RowFor(kPresetKey).mark == Mark::kSafeStart);
}

TEST_CASE("The session report names the Safe start and the Preset") {
  CHECK(SessionSettingsJson(false, "original") ==
        R"("settings": {"safe_start": false, "preset": "original"})");
  CHECK(SessionSettingsJson(true, ActivePreset(kEnhancedValues, kRendererNative)) ==
        R"("settings": {"safe_start": true, "preset": "enhanced"})");
  CHECK(ActivePreset({{"ts4_sharpening", "high"}}, kRendererNative) == "custom");
  CHECK(ActivePreset({}, kRendererNative) == "original");
}

// ---- Safe start (ticket 09) ----

TEST_CASE("Under Safe start every row shows its default, changes are not saved and rows say so") {
  ModelContext context = ControlsContext();
  context.safe_start = true;
  // A file full of non-default values; the game ignores it, so the cvars stay at their defaults.
  const ValueMap file = {{"ts4_renderer", "native"}, {"ts4_msaa", "8"}, {"ts4_pad_a", "b"},
                         {"ts4_pad_b", "a"},         {"mnk_mode", "true"}, {"keybind_a", "J"}};
  SettingsModel model(context, file, Defaults());
  for (Section section : {Section::kGraphics, Section::kControls}) {
    for (const Row& row : model.Rows(section)) {
      if (row.kind != RowKind::kSetting) continue;
      CHECK(row.value == row.setting->default_value);
      CHECK(row.mark == Mark::kSafeStart);
      CHECK_FALSE(row.pending);
      CHECK(row.note.rfind("Safe start:", 0) == 0);
    }
  }
  CHECK(model.RowFor(kPresetKey).value == "original");
  // Whatever the player changes lasts only until the game closes.
  std::vector<SettingChange> all;
  for (const auto& batch :
       {model.Change(kPresetKey, "enhanced"), model.Change("ts4_renderer", "native"),
        model.Change("ts4_pad_a", "x"), model.Change("mnk_mode", "true"),
        model.BindKey("keybind_a", "J"), model.ResetControls()}) {
    all.insert(all.end(), batch.begin(), batch.end());
  }
  REQUIRE_FALSE(all.empty());
  for (const SettingChange& change : all) CHECK_FALSE(change.save);
  CHECK(model.Change("ts4_msaa", "8").front().set_cvar);  // a live one still applies this session
}

TEST_CASE("A Launch option still outranks Safe start's default, and shows as one") {
  ModelContext context = Context();
  context.safe_start = true;
  context.pinned = {"ts4_msaa"};
  ValueMap in_effect = Defaults();
  in_effect["ts4_msaa"] = "8";
  SettingsModel model(context, {{"ts4_msaa", "4"}}, in_effect);
  CHECK(model.RowFor("ts4_msaa").value == "8");
  CHECK(model.RowFor("ts4_msaa").mark == Mark::kLaunchOption);
  CHECK(model.RowFor("ts4_sharpening").mark == Mark::kSafeStart);
}

// ---- Controls (ticket 06) ----

TEST_CASE("Controls list the pad rows with the player's names and the next-start mark") {
  SettingsModel model(ControlsContext(), {}, Defaults());
  const std::vector<Row> rows = model.Rows(Section::kControls);
  // Keyboard and mouse is off: its two switches, the 16 pad rows and the reset row.
  REQUIRE(rows.size() == 2 + 16 + 1);
  CHECK(rows[0].label == "Keyboard and mouse");
  const Row a = model.RowFor("ts4_pad_a");
  CHECK(a.label == "Pad: A");
  CHECK(a.value_label == "A");
  CHECK(a.mark == Mark::kNextStart);
  CHECK(a.choices.size() == 16);
  CHECK(model.RowFor("ts4_pad_ls").value_label == "LS click");
  CHECK(model.RowFor("ts4_pad_up").choices[12].label == "D-pad up");
  CHECK(rows.back().kind == RowKind::kAction);
}

TEST_CASE("A pad remap is saved for the next start and not applied now") {
  SettingsModel model(ControlsContext(), {}, Defaults());
  auto changes = model.Change("ts4_pad_a", "rt");
  REQUIRE(changes.size() == 2);  // rt's own binding swaps in
  CHECK(*Find(changes, "ts4_pad_a") == SettingChange{"ts4_pad_a", "rt", false, true});
  CHECK(model.RowFor("ts4_pad_a").pending);
  CHECK(model.RowFor("ts4_pad_a").note == "Applies the next time the game starts.");
}

TEST_CASE("A pad input already in use swaps bindings, as pad::Rebind does") {
  SettingsModel model(ControlsContext(), {}, Defaults());
  auto changes = model.Change("ts4_pad_a", "x");
  REQUIRE(changes.size() == 2);
  CHECK(Find(changes, "ts4_pad_a")->value == "x");
  CHECK(Find(changes, "ts4_pad_x")->value == "a");
  topspin4::pad::Bindings bindings = topspin4::pad::Bindings::Default();
  topspin4::pad::Rebind(bindings, topspin4::pad::Input::kA, topspin4::pad::Input::kX);
  for (size_t i = 0; i < topspin4::pad::kInputCount; ++i) {
    const auto input = topspin4::pad::Input(i);
    CHECK(model.RowFor(topspin4::pad::SettingKey(input)).value ==
          topspin4::pad::InputName(bindings.Source(input)));
  }
  // No two actions share an input after a chain of remaps.
  model.Change("ts4_pad_b", "x");
  model.Change("ts4_pad_start", "a");
  std::set<std::string> used;
  for (size_t i = 0; i < topspin4::pad::kInputCount; ++i) {
    used.insert(model.RowFor(topspin4::pad::SettingKey(topspin4::pad::Input(i))).value);
  }
  CHECK(used.size() == topspin4::pad::kInputCount);
  CHECK(model.Change("ts4_pad_b", "x").empty());  // already shown
}

TEST_CASE("Key bindings show only with keyboard and mouse on, and a key press rebinds one") {
  SettingsModel model(ControlsContext(), {}, Defaults());
  CHECK(model.Rows(Section::kControls).size() == 19);
  model.Change("mnk_mode", "true");
  REQUIRE(model.Rows(Section::kControls).size() == 19 + 24);
  const Row a = model.RowFor("keybind_a");
  CHECK(a.capture);
  CHECK(a.editable);
  CHECK(a.mark == Mark::kNow);
  CHECK(a.value_label == "Semicolon,Space");

  auto changes = model.BindKey("keybind_a", "J");
  REQUIRE(changes.size() == 1);
  CHECK(changes[0] == SettingChange{"keybind_a", "J", true, true});
  CHECK(model.RowFor("keybind_a").value == "J");
  CHECK(model.BindKey("keybind_a", "J").empty());
  CHECK(model.BindKey("keybind_a", "").empty());
  CHECK(model.BindKey("ts4_msaa", "J").empty());
  CHECK(model.Step("keybind_a", 1).empty());  // left and right do not step a key row
}

TEST_CASE("A key another binding uses is taken from it; one left bare takes the old keys") {
  SettingsModel model(ControlsContext(), {}, Defaults());
  model.Change("mnk_mode", "true");
  // Q is one of left trigger's keys ("Q,I"): it loses Q and keeps I.
  auto changes = model.BindKey("keybind_x", "Q");
  REQUIRE(changes.size() == 2);
  CHECK(Find(changes, "keybind_x")->value == "Q");
  CHECK(Find(changes, "keybind_left_trigger")->value == "I");
  // F was left stick press's only key: it takes Y's old key, P.
  changes = model.BindKey("keybind_y", "F");
  REQUIRE(changes.size() == 2);
  CHECK(Find(changes, "keybind_lstick_press")->value == "P");
  for (const Row& row : model.Rows(Section::kControls)) {
    if (row.capture) CHECK_FALSE(row.value.empty());
  }
}

TEST_CASE("Reset restores every binding and leaves keyboard and mouse as it is") {
  SettingsModel model(ControlsContext(), {}, Defaults());
  model.Change("mnk_mode", "true");
  model.Change("ts4_pad_a", "x");
  model.Change("ts4_pad_lt", "rt");
  model.BindKey("keybind_a", "J");
  model.BindKey("keybind_b", "Q");
  const auto changes = model.ResetControls();
  REQUIRE_FALSE(changes.empty());
  for (const SettingChange& change : changes) {
    CHECK(change.value == FindSetting(change.key)->default_value);
    CHECK(change.save);
  }
  for (const Row& row : model.Rows(Section::kControls)) {
    if (row.kind == RowKind::kSetting && row.setting->key != "mnk_mode") {
      CHECK(row.value == row.setting->default_value);
    }
  }
  CHECK(model.RowFor("mnk_mode").value == "true");
  CHECK(model.ResetControls().empty());
}

TEST_CASE("The page rebinds by pad and key: A starts capture, a key ends it, B cancels") {
  PageModel page{SettingsModel(ControlsContext(), {}, Defaults())};
  page.Open(0);
  page.Update(Pad{}, 200);
  uint64_t now = 210;
  auto press = [&](uint16_t buttons) {
    auto changes = page.Update(Pad{buttons, 0, 0}, now += 10);
    page.Update(Pad{}, now += 10);
    return changes;
  };
  press(PageModel::kRB);
  REQUIRE(page.section() == Section::kControls);
  press(PageModel::kDown);
  press(PageModel::kDown);  // past the two keyboard-and-mouse switches
  REQUIRE(page.rows()[page.selected_index()].key == "ts4_pad_a");
  auto changes = press(PageModel::kRight);
  REQUIRE(changes.size() == 2);  // the next input, swapped in
  CHECK(page.model().RowFor("ts4_pad_a").value == "b");

  // Keyboard and mouse on (row 0) brings in the key rows.
  press(PageModel::kUp);
  press(PageModel::kUp);
  REQUIRE(page.rows()[page.selected_index()].key == "mnk_mode");
  CHECK(press(PageModel::kA).size() == 1);
  CHECK(page.model().RowFor("mnk_mode").value == "true");

  // Up from the first row wraps to the reset row, then to the last key row.
  press(PageModel::kUp);
  REQUIRE(page.rows()[page.selected_index()].kind == RowKind::kAction);
  press(PageModel::kUp);
  const std::string key_row = page.rows()[page.selected_index()].key;
  REQUIRE(key_row.starts_with("keybind_"));
  CHECK(press(PageModel::kA).empty());
  CHECK(page.capturing());
  CHECK(page.capture_key() == key_row);
  // While it waits for a key the pad changes nothing; B cancels instead of closing the page.
  CHECK(press(PageModel::kDown).empty());
  CHECK(page.capture_key() == key_row);
  press(PageModel::kB);
  CHECK_FALSE(page.capturing());
  CHECK(page.is_open());
  press(PageModel::kA);
  const auto bound = page.BindKey("Shift+J");
  REQUIRE(bound.size() == 1);
  CHECK(bound[0] == SettingChange{key_row, "Shift+J", true, true});
  CHECK_FALSE(page.capturing());
  CHECK(page.BindKey("K").empty());  // nothing is waiting any more
}

TEST_CASE("The reset row needs a deliberate A") {
  PageModel page{SettingsModel(ControlsContext(), {}, Defaults())};
  page.Open(0);
  page.Update(Pad{}, 200);
  uint64_t now = 210;
  auto press = [&](uint16_t buttons) {
    auto changes = page.Update(Pad{buttons, 0, 0}, now += 10);
    page.Update(Pad{}, now += 10);
    return changes;
  };
  press(PageModel::kRB);
  press(PageModel::kDown);
  press(PageModel::kDown);
  press(PageModel::kRight);  // ts4_pad_a to b, swapped with b
  press(PageModel::kUp);
  press(PageModel::kUp);
  press(PageModel::kUp);  // wraps to the reset row
  REQUIRE(page.rows()[page.selected_index()].kind == RowKind::kAction);
  CHECK(press(PageModel::kRight).empty());
  CHECK(press(PageModel::kLeft).empty());
  CHECK(press(PageModel::kA).size() == 2);
  CHECK(page.model().RowFor("ts4_pad_a").value == "a");
}

TEST_CASE("A remap cannot lock the player out of the page's own navigation") {
  // The page's navigation takes a pad snapshot and reads no binding; the game-side Remap is
  // undone before the page sees the pad (pad::Unmap), so a shuffled layout navigates the same.
  topspin4::pad::Bindings bindings = topspin4::pad::Bindings::Default();
  for (size_t i = 0; i < topspin4::pad::kInputCount; ++i) {
    bindings.Set(topspin4::pad::Input(i),
                 topspin4::pad::Input((i + 1) % topspin4::pad::kInputCount));
  }
  PageModel page{SettingsModel(ControlsContext(), {}, Defaults())};
  page.Open(0);
  page.Update(Pad{}, 200);
  topspin4::pad::Pad physical;
  physical.buttons = PageModel::kDown;
  const topspin4::pad::Pad seen =
      topspin4::pad::Unmap(topspin4::pad::Remap(physical, bindings), bindings);
  CHECK(page.Update(Pad{seen.buttons, seen.thumb_lx, seen.thumb_ly}, 210).empty());
  CHECK(page.selected_index() == 1);
  page.Update(Pad{}, 220);
  page.Update(Pad{PageModel::kStart, 0, 0}, 230);
  CHECK_FALSE(page.is_open());
}
