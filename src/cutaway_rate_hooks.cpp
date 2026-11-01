// 60 fps cutaways' hooks (cutaway_rate.h), built into the Swing Module's DLL:
//   88739a40  the frame's render setup (r3 the render state, mode at +0x218): after it, the
//             present interval at device+0x35f0 becomes 1 for a cutaway at 60
//   881e5de8  the world step (f1 dt): the cutaway's 0.03333 becomes 0.01666 at 60

#include <atomic>
#include <bit>
#include <cstdint>
#include <cstring>

#include <rex/hook.h>

#include "cutaway_rate.h"

DECLARE_REX_FUNC(sub_88739A40);
DECLARE_REX_FUNC(sub_881E5DE8);

namespace {

namespace cutaway_rate = topspin4::cutaway_rate;

// The renderer global (88d45ad4): the render state at +0x20, the D3D device at +0x15720.
constexpr uint32_t kRenderer = 0x88d45ad4;
constexpr uint32_t kRenderState = 0x20;
constexpr uint32_t kDevice = 0x15720;
constexpr uint32_t kMode = 0x218;
constexpr uint32_t kPresentInterval = 0x35f0;

uint32_t Load32(const uint8_t* base, uint32_t address) {
  uint32_t value;
  std::memcpy(&value, base + address, sizeof(value));
  return __builtin_bswap32(value);
}

void Store32(uint8_t* base, uint32_t address, uint32_t value) {
  value = __builtin_bswap32(value);
  std::memcpy(base + address, &value, sizeof(value));
}

bool Sixty() {
  const auto sixty = cutaway_rate::GetSink().sixty;
  return sixty && sixty();
}

void LogOnce(std::atomic<bool>& logged, const char* message) {
  if (logged.exchange(true)) return;
  if (auto log = cutaway_rate::GetSink().log) log(message);
}

std::atomic<bool> g_interval_logged{false};
std::atomic<bool> g_step_logged{false};
std::atomic<bool> g_in_cutaway{false};

}  // namespace

REX_HOOK_RAW(sub_88739A40) {
  const uint32_t render_state = ctx.r3.u32;
  __imp__sub_88739A40(ctx, base);
  if (!Sixty()) return;
  const uint32_t mode = Load32(base, render_state + kMode);
  // Each cutaway's start and end in the log, so a run can time one against the original's.
  if (const bool cutaway = mode == cutaway_rate::kCutawayMode;
      cutaway != g_in_cutaway.exchange(cutaway)) {
    if (auto log = cutaway_rate::GetSink().log) {
      log(cutaway ? "cutaways at 60: cutaway starts" : "cutaways at 60: cutaway ends");
    }
  }
  const uint32_t interval = Load32(base, Load32(base, kRenderer) + kDevice) + kPresentInterval;
  const uint32_t game_interval = Load32(base, interval);
  const uint32_t chosen = cutaway_rate::PresentInterval(game_interval, mode, true);
  if (chosen != game_interval) {
    Store32(const_cast<uint8_t*>(base), interval, chosen);
    LogOnce(g_interval_logged, "cutaways at 60: present interval 1");
  }
}

REX_HOOK_RAW(sub_881E5DE8) {
  if (Sixty()) {
    const uint32_t mode = Load32(base, Load32(base, Load32(base, kRenderer) + kRenderState) + kMode);
    const uint32_t bits = std::bit_cast<uint32_t>(float(ctx.f1.f64));
    const uint32_t chosen = cutaway_rate::StepBits(bits, mode, true);
    if (chosen != bits) {
      ctx.f1.f64 = double(std::bit_cast<float>(chosen));
      LogOnce(g_step_logged, "cutaways at 60: world step 1/60");
    }
  }
  __imp__sub_881E5DE8(ctx, base);
}
