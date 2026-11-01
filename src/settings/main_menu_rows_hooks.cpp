// The main menu rows' five hooks, built into the Swing Module's DLL (cmake/d3d_hooks.cmake). They
// turn two hidden rows of S4MainMenu, 3D settings and the PS3 Move tutorial, into PC SETTINGS
// (opens the Settings page) and EXIT TO DESKTOP (docs/research/game-settings.md, Addendum 3).
// Each hook logs its first hit through the sink, so a run confirms the route.
//
//   Is3DAccessible      8877A338  the menu is being built: arm. Answers 0 to our PatchList, which
//                                 would add the 3D mode switch too
//   Pam.PPrint          888048F0  a no-op in retail; on the armed "S4MainMenu: EXPORT_UpdateList"
//                                 runs "$<scene>.EXPORT_PatchList()", which appends the rows
//   IsAnyMoveConnected  88778FA0  the stub answered 0; answers 1 only to our PatchList
//   text resolver       881EC108  labels the two rows
//   ChangeScene         887C3370  the rows' scene changes become our two actions; PC SETTINGS
//                                 then runs "$<scene>.On()" so the menu comes back

#include <atomic>
#include <cctype>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>
#include <string_view>

#include <rex/hook.h>

#include "settings/main_menu_rows.h"
#include "settings/main_menu_sink.h"

DECLARE_REX_FUNC(sub_8877A338);
DECLARE_REX_FUNC(sub_888048F0);
DECLARE_REX_FUNC(sub_88778FA0);
DECLARE_REX_FUNC(sub_881EC108);
DECLARE_REX_FUNC(sub_887C3370);
DECLARE_REX_FUNC(sub_8819A5C8);
DECLARE_REX_FUNC(sub_8819BAD0);
DECLARE_REX_FUNC(sub_88151AE0);
DECLARE_REX_FUNC(sub_88153240);
DECLARE_REX_FUNC(sub_881E87C0);
DECLARE_REX_FUNC(sub_881FF158);
DECLARE_REX_FUNC(sub_881871D8);
DECLARE_REX_FUNC(sub_881CF4C0);
DECLARE_REX_FUNC(sub_881CE2B8);
DECLARE_REX_FUNC(sub_881C2A58);

namespace {

namespace menu = topspin4::settings::main_menu;

// The UI manager 88908560 hands to the scene lookup.
constexpr uint32_t kUiManagerSlot = 0x88D41FC4;

menu::RowSequence g_rows;
// The last scene ChangeScene changed to.
std::mutex g_scene_mutex;
std::string g_current_scene;

void SetCurrentScene(std::string_view scene) {
  std::lock_guard lock(g_scene_mutex);
  g_current_scene = scene;
}

std::string CurrentScene() {
  std::lock_guard lock(g_scene_mutex);
  return g_current_scene;
}

uint32_t Load32(const uint8_t* base, uint32_t address) {
  uint32_t value;
  std::memcpy(&value, base + address, sizeof(value));
  return __builtin_bswap32(value);
}

void Store32(uint8_t* base, uint32_t address, uint32_t value) {
  value = __builtin_bswap32(value);
  std::memcpy(base + address, &value, sizeof(value));
}

void Store16(uint8_t* base, uint32_t address, uint16_t value) {
  value = __builtin_bswap16(value);
  std::memcpy(base + address, &value, sizeof(value));
}

// A guest pointer worth following: not null and inside the guest's address space.
bool Plausible(uint32_t address) { return address >= 0x10000 && address < 0xC0000000; }

std::string ReadCString(const uint8_t* base, uint32_t address, size_t limit = 96) {
  std::string text;
  if (!Plausible(address)) return text;
  for (size_t i = 0; i < limit; ++i) {
    const char c = char(base[address + i]);
    if (c == '\0') break;
    text.push_back(std::isprint(static_cast<unsigned char>(c)) ? c : '?');
  }
  return text;
}

// Writes text, terminated, at a guest address the caller owns.
void WriteCString(uint8_t* base, uint32_t address, std::string_view text) {
  std::memcpy(base + address, text.data(), text.size());
  base[address + text.size()] = 0;
}

void Log(const std::string& message) {
  if (auto log = menu::GetMenuSink().log) log(message.c_str());
}

// Logs only the first time (per hook).
bool FirstHit(std::atomic<bool>& seen) { return !seen.exchange(true); }

// A guest String (one word: its characters, refcounted header 16 bytes before) from text.
void MakeString(rex::CallFrame& f, uint8_t* base, uint32_t out, uint32_t text) {
  f.ctx.r3.u64 = out;
  f.ctx.r4.u64 = text;
  sub_88153240(f.ctx, base);
}

void ReleaseString(rex::CallFrame& f, uint8_t* base, uint32_t string) {
  f.ctx.r3.u64 = string;
  sub_88151AE0(f.ctx, base);
}

void ReleaseRef(rex::CallFrame& f, uint8_t* base, uint32_t object) {
  f.ctx.r3.u64 = object;
  sub_881871D8(f.ctx, base);
}

// A Lua C function's string argument: r5 the call info, whose +8 leads to the lua_State and +0xC
// holds the argument's index. The TValue (16 bytes) has the type at +0 (4, a string) and the
// TString at +8; its header is 0x18 bytes, the length at +0x14 and the characters after it.
std::string ReadLuaStringArg(const PPCContext& ctx, const uint8_t* base) {
  const uint32_t info = ctx.r5.u32;
  if (!Plausible(info)) return {};
  const uint32_t holder = Load32(base, info + 8);
  if (!Plausible(holder)) return {};
  const uint32_t lua = Load32(base, holder);
  const int32_t index = int32_t(Load32(base, info + 12));
  if (!Plausible(lua) || index <= 0 || index > 64) return {};
  const uint32_t value = Load32(base, lua + 16) + uint32_t(index - 1) * 16;
  if (!Plausible(value) || value >= Load32(base, lua + 12)) return {};
  if (Load32(base, value) != 4) return {};
  const uint32_t string = Load32(base, value + 8);
  if (!Plausible(string)) return {};
  const uint32_t length = Load32(base, string + 0x14);
  if (length > 64) return {};
  return ReadCString(base, string + 0x18, length);
}

// What 88908560 does for S4LockerRoomSelect, for the menu: find the scene, find its MainMenu
// list, and run "$<scene node>.<call>" on it. Inside a running action chain the call is queued and
// runs after this returns. False if it could not be issued.
bool RunMenuCall(PPCContext& ctx, uint8_t* base, std::string_view call) {
  rex::CallFrame f(ctx);
  f.ctx.r1.u32 -= 0x200;
  const uint32_t sp = f.ctx.r1.u32;
  const uint32_t str = sp + 0x80;
  const uint32_t scene_ref = sp + 0x84;
  const uint32_t node_ref = sp + 0x88;
  const uint32_t name_str = sp + 0x8C;
  const uint32_t action_str = sp + 0x90;
  const uint32_t text = sp + 0x100;  // up to 0x100 bytes

  WriteCString(base, text, menu::kMenuScene);
  MakeString(f, base, str, text);
  f.ctx.r3.u64 = scene_ref;
  f.ctx.r4.u64 = Load32(base, kUiManagerSlot);
  f.ctx.r5.u64 = str;
  sub_881E87C0(f.ctx, base);
  ReleaseString(f, base, str);
  const uint32_t scene = Load32(base, scene_ref);
  if (scene == 0) {
    Log("main menu rows: the scene S4MainMenu was not found");
    return false;
  }

  WriteCString(base, text, menu::kMenuList);
  MakeString(f, base, str, text);
  f.ctx.r3.u64 = node_ref;
  f.ctx.r4.u64 = scene;
  f.ctx.r5.u64 = str;
  f.ctx.r6.u64 = 1;
  sub_881FF158(f.ctx, base);
  const uint32_t node = Load32(base, f.ctx.r3.u32);
  if (const uint32_t held = Load32(base, node_ref)) {
    ReleaseRef(f, base, held);
    Store32(base, node_ref, 0);
  }
  ReleaseString(f, base, str);

  bool issued = false;
  if (node == 0) {
    Log("main menu rows: the list MainMenu was not found");
  } else {
    f.ctx.r3.u64 = name_str;
    f.ctx.r4.u64 = node;
    sub_881CF4C0(f.ctx, base);
    const std::string scene_node = ReadCString(base, Load32(base, f.ctx.r3.u32), 128);
    ReleaseString(f, base, name_str);
    const std::string action = menu::NodeCall(scene_node, call);
    if (scene_node.empty() || action.size() >= 0x100) {
      Log("main menu rows: the list's scene node has no usable name");
    } else {
      WriteCString(base, text, action);
      MakeString(f, base, action_str, text);
      f.ctx.r3.u64 = node;
      sub_881CE2B8(f.ctx, base);
      f.ctx.r4.u64 = action_str;
      f.ctx.r5.u64 = 0;
      sub_881C2A58(f.ctx, base);
      ReleaseString(f, base, action_str);
      Log("main menu rows: issued " + action);
      issued = true;
    }
  }
  ReleaseRef(f, base, scene);
  return issued;
}

// ChangeScene's argument: r3 the command context; *(ctx+0x10) holds the argument count at +0x384
// and the first argument at +0x380, an object whose +0x10 is a guest String (its characters).
std::string ReadSceneArg(const PPCContext& ctx, const uint8_t* base) {
  if (!Plausible(ctx.r3.u32)) return {};
  const uint32_t args = Load32(base, ctx.r3.u32 + 0x10);
  if (!Plausible(args) || int32_t(Load32(base, args + 0x384)) <= 0) return {};
  const uint32_t arg = Load32(base, args + 0x380);
  if (!Plausible(arg)) return {};
  return ReadCString(base, Load32(base, arg + 0x10), 64);
}

}  // namespace

// Is3DAccessible(): asked before the menu's UpdateList (the menu is being built) and by PatchList.
REX_HOOK_RAW(sub_8877A338) {
  static std::atomic<bool> first{false};
  if (g_rows.On3DQuery()) {
    ctx.r3.u64 = 0;  // our PatchList: no 3D mode switch, which Xbox never shows
    return;
  }
  __imp__sub_8877A338(ctx, base);
  if (FirstHit(first)) Log("main menu rows: Is3DAccessible is called, armed");
}

// Pam.PPrint(text): a Lua C function, a no-op in retail.
REX_HOOK_RAW(sub_888048F0) {
  static std::atomic<bool> first{false};
  static std::atomic<bool> first_rerun{false};
  const std::string text = ReadLuaStringArg(ctx, base);
  __imp__sub_888048F0(ctx, base);
  if (!menu::IsUpdateListPrint(text)) return;
  if (FirstHit(first)) Log("main menu rows: PPrint sees the menu's UpdateList");
  if (!g_rows.OnUpdateList()) {
    if (FirstHit(first_rerun)) Log("main menu rows: UpdateList ran again unarmed, rows kept as is");
    return;
  }
  if (!RunMenuCall(ctx, base, menu::kPatchListCall)) g_rows.OnPatchListFailed();
  ctx.r3.u64 = 0;
}

// IsAnyMoveConnected(): the stub answered 0.
REX_HOOK_RAW(sub_88778FA0) {
  static std::atomic<bool> first{false};
  const bool ours = g_rows.OnMoveQuery();
  ctx.r3.u64 = ours ? 1 : 0;
  if (ours && FirstHit(first)) Log("main menu rows: IsAnyMoveConnected answers 1");
}

// The text resolver: r3 the UI manager, r4 the text ID (a guest String), r5 the Text it fills.
REX_HOOK_RAW(sub_881EC108) {
  static std::atomic<bool> first{false};
  const uint32_t text_id = ctx.r4.u32;
  const uint32_t out = ctx.r5.u32;
  std::string_view label;
  if (Plausible(text_id) && Plausible(out)) {
    label = menu::LabelFor(ReadCString(base, Load32(base, text_id), 40));
  }
  __imp__sub_881EC108(ctx, base);
  if (label.empty()) return;
  if (FirstHit(first)) Log("main menu rows: a row label is resolved");
  rex::CallFrame frame(ctx);
  frame.ctx.r1.u32 -= 0x100;
  const uint32_t text = frame.ctx.r1.u32 + 0x80;  // UTF-16, big-endian, terminated
  const uint32_t temp = frame.ctx.r1.u32 + 0xE0;  // a guest String
  uint32_t at = text;
  for (const char c : label) {
    Store16(base, at, uint16_t(static_cast<unsigned char>(c)));
    at += 2;
  }
  Store16(base, at, 0);
  frame.ctx.r3.u64 = temp;
  frame.ctx.r4.u64 = text;
  sub_8819A5C8(frame.ctx, base);
  frame.ctx.r3.u64 = out;
  frame.ctx.r4.u64 = temp;
  sub_8819BAD0(frame.ctx, base);
  frame.ctx.r3.u64 = temp;
  sub_88151AE0(frame.ctx, base);
  ctx.r3.u64 = 1;
}

// ChangeScene(scene): a Lua command. A row's action drops the menu's input, plays its SceneOff
// and waits for texture packs before it.
REX_HOOK_RAW(sub_887C3370) {
  static std::atomic<bool> first{false};
  const std::string scene = ReadSceneArg(ctx, base);
  if (FirstHit(first)) Log("main menu rows: ChangeScene is called (" + scene + ")");
  switch (menu::ActionForScene(scene, CurrentScene())) {
    case menu::SceneAction::kNone:
      // An unreadable argument leaves the last scene, so the menu is not forgotten.
      if (!scene.empty()) SetCurrentScene(scene);
      __imp__sub_887C3370(ctx, base);
      return;
    case menu::SceneAction::kOpenSettings:
      Log("main menu rows: PC SETTINGS chosen, opening the Settings page");
      if (auto open = menu::GetMenuSink().open_page) open();
      // The row played the menu's SceneOff and took its input; turn it back on as a return to
      // the menu does. That rebuilds the list (the rows come back) and reselects the row.
      RunMenuCall(ctx, base, menu::kMenuOnCall);
      break;
    case menu::SceneAction::kExitToDesktop:
      Log("main menu rows: EXIT TO DESKTOP chosen, closing the window");
      if (auto exit = menu::GetMenuSink().exit_to_desktop) exit();
      break;
  }
  ctx.r3.u64 = 0;
}
