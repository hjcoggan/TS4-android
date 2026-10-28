// The OPTIONS row's three hooks, built into the Swing Module's DLL (CMakeLists.txt). They turn the
// game's PS3 Move calibration row in S4MainMenuSubOptions into a PC SETTINGS row that opens the
// Settings page (ticket 11; the route and its addresses are docs/research/game-settings.md,
// Addendum 2). Each hook logs its first hit through the sink, so a run confirms the route.
//
//   IsMoveController   8877B2B8  the stub answered 0; answers 1 only while the caller's scene is
//                                S4MainMenuSubOptions, so the row appears there and nowhere else
//   text resolver      881EC108  answers "PC SETTINGS" for the text ID PS3MoveCalibrate
//   ForceMoveRecalibrate 8877B288  the stub answered 1; opens the page

#include <atomic>
#include <cctype>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <set>
#include <string>

#include <rex/hook.h>

#include "settings/options_row.h"
#include "settings/options_row_sink.h"

DECLARE_REX_FUNC(sub_8877B2B8);
DECLARE_REX_FUNC(sub_8877B288);
DECLARE_REX_FUNC(sub_881EC108);
DECLARE_REX_FUNC(sub_8819A5C8);
DECLARE_REX_FUNC(sub_8819BAD0);
DECLARE_REX_FUNC(sub_88151AE0);

namespace {

namespace row = topspin4::settings::options_row;

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

void Log(const std::string& message) {
  if (auto log = row::GetRowSink().log) log(message.c_str());
}

// Logs only the first time (per hook).
bool FirstHit(std::atomic<bool>& seen) { return !seen.exchange(true); }

}  // namespace

// IsMoveController(controller): r3 is the command context. Answers 0, as the stub did. The first
// run showed the route does not hold: OPTIONS never called this, and the guessed scene lookup
// (881CF4C0 on the context's second word) crashed when Exhibition called it. Logged so a later
// RE pass sees who calls it.
REX_HOOK_RAW(sub_8877B2B8) {
  static std::atomic<bool> first{false};
  if (FirstHit(first)) Log("options row: IsMoveController is called (answers 0)");
  ctx.r3.u64 = 0;
}

// The text resolver: r3 the UI manager, r4 the text ID (a guest String), r5 the Text it fills.
REX_HOOK_RAW(sub_881EC108) {
  static std::atomic<bool> first{false};
  const uint32_t text_id = ctx.r4.u32;
  const uint32_t out = ctx.r5.u32;
  bool is_move_label = false;
  if (Plausible(text_id) && Plausible(out)) {
    is_move_label = row::IsMoveLabelId(ReadCString(base, Load32(base, text_id), 32));
  }
  __imp__sub_881EC108(ctx, base);
  if (!is_move_label) return;
  if (FirstHit(first)) Log("options row: the label PS3MoveCalibrate is resolved");
  rex::CallFrame frame(ctx);
  frame.ctx.r1.u32 -= 0x100;
  const uint32_t text = frame.ctx.r1.u32 + 0x80;   // UTF-16, big-endian, terminated
  const uint32_t temp = frame.ctx.r1.u32 + 0xE0;   // a guest String
  uint32_t at = text;
  for (const char c : row::kLabel) {
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

// ForceMoveRecalibrate(controller): the row was chosen.
REX_HOOK_RAW(sub_8877B288) {
  Log("options row: ForceMoveRecalibrate is called, opening the Settings page");
  if (auto open = row::GetRowSink().open_page) open();
  ctx.r3.u64 = 1;
}
