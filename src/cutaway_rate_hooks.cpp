// 60 fps cutaways' hooks (cutaway_rate.h), built into the Swing Module's DLL:
//   88739a40  the frame's render setup (r3 the render state, mode at +0x218): after it, the
//             present interval at device+0x35f0 becomes 1 for a cutaway at 60
//   881e5de8  the world step (f1 dt): the cutaway's 0.03333 becomes 0.01666 at 60
//   886117e8  a scheduler, and 88611d90 a service list (r3 the instance): at 60 each runs once
//             per game frame, so the cutaway logic the game reaches twice a frame steps at 60 Hz
//   8817c608  the face player: at 60 it holds on odd game frames, so faces keep their 30 Hz

#include <algorithm>
#include <atomic>
#include <bit>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <vector>

#include <rex/hook.h>

#include "cutaway_rate.h"

DECLARE_REX_FUNC(sub_88739A40);
DECLARE_REX_FUNC(sub_881E5DE8);
DECLARE_REX_FUNC(sub_886117E8);
DECLARE_REX_FUNC(sub_88611D90);
DECLARE_REX_FUNC(sub_8817C608);

namespace {

namespace cutaway_rate = topspin4::cutaway_rate;

// The renderer global (88d45ad4): the render state at +0x20, the D3D device at +0x15720.
constexpr uint32_t kRenderer = 0x88d45ad4;
constexpr uint32_t kRenderState = 0x20;
constexpr uint32_t kDevice = 0x15720;
constexpr uint32_t kMode = 0x218;
constexpr uint32_t kPresentInterval = 0x35f0;
// The game frame: the counter at *(88d45b1c)+0x270 that the schedulers' frame divisors use. It
// steps once per swap while the game presents, and on through a load, when swaps stop.
constexpr uint32_t kGameState = 0x88d45b1c;
constexpr uint32_t kGameFrame = 0x270;
// The face player's slots (8817c608): a count at 88d45cc4, an on byte at 88d45cc8, slots of
// 0x19c bytes from 88d45ccc with a playing byte at +7, the clip frame at +8, the clip at +0x150
// and a clip waiting to start at +0x154.
constexpr uint32_t kFaceSlotCount = 0x88d45cc4;
constexpr uint32_t kFaceOn = 0x88d45cc8;
constexpr uint32_t kFaceSlots = 0x88d45ccc;
constexpr uint32_t kFaceSlotSize = 0x19c;
constexpr uint32_t kFacePlaying = 0x7;
constexpr uint32_t kFaceFrame = 0x8;
constexpr uint32_t kFaceClip = 0x150;
constexpr uint32_t kFaceNextClip = 0x154;
constexpr uint32_t kMaxFaceSlots = 8;

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

void Log(const char* message) {
  if (auto log = cutaway_rate::GetSink().log) log(message);
}

void LogOnce(std::atomic<bool>& logged, const char* message) {
  if (!logged.exchange(true)) Log(message);
}

uint32_t Mode(const uint8_t* base) {
  return Load32(base, Load32(base, Load32(base, kRenderer) + kRenderState) + kMode);
}

// Whether this frame is a cutaway at 60, and if so its game frame number.
bool SixtyCutaway(const uint8_t* base, uint32_t& game_frame) {
  if (!Sixty() || !cutaway_rate::SixtyCutaway(Mode(base), true)) return false;
  const uint32_t state = Load32(base, kGameState);
  if (!state) return false;
  game_frame = Load32(base, state + kGameFrame);
  return true;
}

std::atomic<bool> g_interval_logged{false};
std::atomic<bool> g_step_logged{false};
std::atomic<bool> g_face_logged{false};
std::atomic<bool> g_in_cutaway{false};

std::mutex g_once_mutex;
cutaway_rate::OncePerFrame g_once;      // guarded by g_once_mutex
std::vector<uint32_t> g_logged_repeats;  // guarded by g_once_mutex

// Whether a scheduler or list instance runs now: false for its second run in a game frame of a
// cutaway at 60. Each instance it holds back is logged once, so a run shows the route.
bool Runs(const uint8_t* base, uint32_t instance) {
  uint32_t frame;
  if (!SixtyCutaway(base, frame)) return true;
  std::lock_guard lock(g_once_mutex);
  if (g_once.Admit(frame, instance)) return true;
  if (std::find(g_logged_repeats.begin(), g_logged_repeats.end(), instance) ==
      g_logged_repeats.end()) {
    g_logged_repeats.push_back(instance);
    char line[96];
    std::snprintf(line, sizeof(line), "cutaways at 60: scheduler %08x runs once per frame",
                  instance);
    Log(line);
  }
  return false;
}

}  // namespace

REX_HOOK_RAW(sub_88739A40) {
  const uint32_t render_state = ctx.r3.u32;
  __imp__sub_88739A40(ctx, base);
  const uint32_t mode = Load32(base, render_state + kMode);
  // Each cutaway's start and end in the log, at 30 or 60, so a run can time one against the
  // original's.
  if (const bool cutaway = mode == cutaway_rate::kCutawayMode;
      cutaway != g_in_cutaway.exchange(cutaway)) {
    Log(cutaway ? "cutaways: cutaway starts" : "cutaways: cutaway ends");
  }
  if (!Sixty()) return;
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
    const uint32_t bits = std::bit_cast<uint32_t>(float(ctx.f1.f64));
    const uint32_t chosen = cutaway_rate::StepBits(bits, Mode(base), true);
    if (chosen != bits) {
      ctx.f1.f64 = double(std::bit_cast<float>(chosen));
      LogOnce(g_step_logged, "cutaways at 60: world step 1/60");
    }
  }
  __imp__sub_881E5DE8(ctx, base);
}

REX_HOOK_RAW(sub_886117E8) {
  if (Runs(base, ctx.r3.u32)) __imp__sub_886117E8(ctx, base);
}

REX_HOOK_RAW(sub_88611D90) {
  if (Runs(base, ctx.r3.u32)) __imp__sub_88611D90(ctx, base);
}

REX_HOOK_RAW(sub_8817C608) {
  uint32_t frame;
  if (!SixtyCutaway(base, frame) || !base[kFaceOn]) {
    __imp__sub_8817C608(ctx, base);
    return;
  }
  // The slots the call will step: playing, with a clip.
  const uint32_t count = std::min(Load32(base, kFaceSlotCount), kMaxFaceSlots);
  bool at_start = false;
  for (uint32_t i = 0; i < count; ++i) {
    const uint32_t slot = kFaceSlots + i * kFaceSlotSize;
    if (!base[slot + kFacePlaying] || !Load32(base, slot + kFaceClip)) continue;
    // A clip waiting to start takes the current frame, so it counts as a slot at its start.
    at_start |= Load32(base, slot + kFaceFrame) == 0 || Load32(base, slot + kFaceNextClip) != 0;
  }
  switch (cutaway_rate::FaceStepFor(cutaway_rate::kCutawayMode, true, frame, at_start)) {
    case cutaway_rate::FaceStep::kRun:
      break;
    case cutaway_rate::FaceStep::kSkip:
      return;
    case cutaway_rate::FaceStep::kHold:
      for (uint32_t i = 0; i < count; ++i) {
        const uint32_t slot = kFaceSlots + i * kFaceSlotSize;
        if (!base[slot + kFacePlaying] || !Load32(base, slot + kFaceClip)) continue;
        Store32(const_cast<uint8_t*>(base), slot + kFaceFrame,
                Load32(base, slot + kFaceFrame) - 1);
      }
      LogOnce(g_face_logged, "cutaways at 60: face player at 30 Hz");
      break;
  }
  __imp__sub_8817C608(ctx, base);
}
