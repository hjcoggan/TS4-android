// The movie player's timing, held to the 60 Hz swaps (src/movie_clock.h). Built into the Loader
// Module's DLL (cmake/d3d_hooks.cmake). Three hooks:
//   83224fe8  the media clock's "now" (only reached through its vtable at 8302b25c): rounded to
//             a 60 Hz grid placed away from where the player's reads fall
//   83225040  the media clock's re-sync (media = *(this+0x130), last = now): a correction
//             within a frame keeps the media clock running
//   8311dad8  XUI's element SetTimer: the video element's timer (id 0xaabbccdd, armed by
//             8321c1b8 with the milliseconds to the next frame) fires on every UI pass instead,
//             so the player decides each frame on the media clock rather than when a wall-clock
//             timer due right at a swap boundary happens to fire

#include <chrono>
#include <cstdint>
#include <cstring>
#include <mutex>

#include <rex/hook.h>

#include "movie_clock.h"

DECLARE_REX_FUNC(sub_83224FE8);
DECLARE_REX_FUNC(sub_83225040);
DECLARE_REX_FUNC(sub_8311DAD8);

namespace {

namespace movie_clock = topspin4::movie_clock;

std::mutex g_mutex;
movie_clock::GridLockedClock g_clock;

int64_t ReadClock() {
  const int64_t now_us = std::chrono::duration_cast<std::chrono::microseconds>(
                             std::chrono::steady_clock::now().time_since_epoch())
                             .count();
  std::lock_guard lock(g_mutex);
  return g_clock.Read(now_us);
}

uint64_t Load64(const uint8_t* base, uint32_t address) {
  uint64_t value;
  std::memcpy(&value, base + address, sizeof(value));
  return __builtin_bswap64(value);
}

void Store64(uint8_t* base, uint32_t address, uint64_t value) {
  value = __builtin_bswap64(value);
  std::memcpy(base + address, &value, sizeof(value));
}

float LoadFloat(const uint8_t* base, uint32_t address) {
  uint32_t bits;
  std::memcpy(&bits, base + address, sizeof(bits));
  bits = __builtin_bswap32(bits);
  float value;
  std::memcpy(&value, &bits, sizeof(value));
  return value;
}

// The media clock object (83224e38): media ms at +0x120, the clock read it was last advanced
// at +0x128, the re-sync target at +0x130, the rate (float) at +0x154.
constexpr uint32_t kMedia = 0x120;
constexpr uint32_t kLast = 0x128;
constexpr uint32_t kTarget = 0x130;
constexpr uint32_t kRate = 0x154;

}  // namespace

// The player only takes differences of these reads (83224e38), so host time serves.
REX_HOOK_RAW(sub_83224FE8) { ctx.r3.u64 = uint64_t(ReadClock()); }

REX_HOOK_RAW(sub_83225040) {
  const uint32_t self = ctx.r3.u32;
  const int64_t now = ReadClock();
  const int64_t media = movie_clock::ResyncedMedia(
      int64_t(Load64(base, self + kMedia)), int64_t(Load64(base, self + kLast)), now,
      int64_t(Load64(base, self + kTarget)), LoadFloat(base, self + kRate));
  Store64(base, self + kMedia, uint64_t(media));
  Store64(base, self + kLast, uint64_t(now));
  ctx.r3.u64 = 0;
}

// SetTimer(element, id, interval_ms).
REX_HOOK_RAW(sub_8311DAD8) {
  if (ctx.r4.u32 == movie_clock::kVideoTimerId) ctx.r5.u64 = 0;
  __imp__sub_8311DAD8(ctx, base);
}
