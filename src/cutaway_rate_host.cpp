// The exe's side of 60 fps cutaways (cutaway_rate.h): fills the sink the Swing hooks call.

#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/system/guest_swaps.h>

#include "cutaway_rate.h"

REXCVAR_DECLARE(bool, ts4_cutaways_60);

namespace topspin4::cutaway_rate {

namespace {

bool Sixty() { return REXCVAR_GET(ts4_cutaways_60); }

// Each line carries the guest swap count, so a cutaway's start and end can be matched to frames.
void Log(const char* message) {
  REXLOG_INFO("{} (swap {})", message, rex::system::GuestSwapCount());
}

}  // namespace

void InstallHost() {
  Sink& sink = GetSink();
  sink.sixty = &Sixty;
  sink.log = &Log;
}

}  // namespace topspin4::cutaway_rate
