// The exe's side of 60 fps cutaways (cutaway_rate.h): fills the sink the Swing hooks call.

#include <rex/cvar.h>
#include <rex/logging.h>

#include "cutaway_rate.h"

REXCVAR_DECLARE(bool, ts4_cutaways_60);

namespace topspin4::cutaway_rate {

namespace {

bool Sixty() { return REXCVAR_GET(ts4_cutaways_60); }

void Log(const char* message) { REXLOG_INFO("{}", message); }

}  // namespace

void InstallHost() {
  Sink& sink = GetSink();
  sink.sixty = &Sixty;
  sink.log = &Log;
}

}  // namespace topspin4::cutaway_rate
