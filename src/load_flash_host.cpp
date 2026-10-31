// The exe's side of the load flash fix (load_flash.h): fills the sink the Swing hooks call.

#include <rex/cvar.h>
#include <rex/logging.h>

#include "load_flash.h"

REXCVAR_DECLARE(bool, ts4_fix_glitches);

namespace topspin4::load_flash {

namespace {

uint32_t HeldPresents() { return REXCVAR_GET(ts4_fix_glitches) ? kHeldPresents : 0; }

void Log(const char* message) { REXLOG_INFO("{}", message); }

}  // namespace

void InstallHost() {
  Sink& sink = GetSink();
  sink.held_presents = &HeldPresents;
  sink.log = &Log;
}

}  // namespace topspin4::load_flash
