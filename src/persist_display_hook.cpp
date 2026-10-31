// The black frame between the title screen and Swing's loading screen, built into the Loader
// Module's DLL. When Swing sets up its device it has the Loader tear down (Loader ordinal 3,
// 834928d0), which calls D3D's PersistDisplay (830b95a8) on the Loader's front buffer before
// releasing the device. PersistDisplay copies the front buffer with the CPU into fresh memory and
// presents the copy, so the console keeps showing the last frame while Swing starts. Here the
// title frame exists only on the host GPU, so the copy is black and stays on screen for the
// ~135 ms Swing takes to present its loading screen (on Classic and Native alike).
//
// Skipping it leaves the last presented title frame up instead, which is what the console shows.
// Its caller ignores the result, and nothing after it waits on its swap.

#include <atomic>
#include <cstdint>

#include <rex/hook.h>

#include "load_flash.h"

DECLARE_REX_FUNC(sub_830B95A8);

REX_HOOK_RAW(sub_830B95A8) {
  static std::atomic<bool> logged{false};
  if (!logged.exchange(true)) {
    if (auto log = topspin4::load_flash::GetSink().log) {
      log("persist display: skipped, the last title frame stays up");
    }
  }
  ctx.r3.u64 = 0;
}
