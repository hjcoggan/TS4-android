// The load flash fix's hooks (load_flash.h), built into the Swing Module's DLL:
//   88905f70  the loading screen's loop: presents until the load is done; its return arms the
//             hold
//   8875f498  the renderer's Present wrapper (r3 the renderer, the only game path to D3D's
//             Present in the frame loop): returns at once while the byte at renderer+0x15649
//             is set, which the hold uses to skip a present

#include <atomic>
#include <cstdint>
#include <cstdio>

#include <rex/hook.h>

#include "load_flash.h"

DECLARE_REX_FUNC(sub_88905F70);
DECLARE_REX_FUNC(sub_8875F498);

namespace {

namespace load_flash = topspin4::load_flash;

// The renderer's "skip this present" byte; the game's own handler (887025b8) sets it too.
constexpr uint32_t kSkipPresent = 0x15649;

load_flash::Hold g_hold;

void Log(const char* message) {
  if (auto log = load_flash::GetSink().log) log(message);
}

}  // namespace

REX_HOOK_RAW(sub_88905F70) {
  __imp__sub_88905F70(ctx, base);
  const auto held = load_flash::GetSink().held_presents;
  const uint32_t presents = held ? held() : 0;
  g_hold.Arm(presents);
  char line[96];
  std::snprintf(line, sizeof(line), "load flash: loading screen done, holding %u presents",
                presents);
  Log(line);
}

REX_HOOK_RAW(sub_8875F498) {
  const uint32_t renderer = ctx.r3.u32;
  if (!g_hold.TakePresent()) {
    __imp__sub_8875F498(ctx, base);
    return;
  }
  uint8_t* skip = const_cast<uint8_t*>(base) + renderer + kSkipPresent;
  const uint8_t previous = *skip;
  *skip = 1;
  __imp__sub_8875F498(ctx, base);
  *skip = previous;
  Log("load flash: present held");
}
