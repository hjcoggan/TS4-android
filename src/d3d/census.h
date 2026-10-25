// --ts4_d3d_census: logs how often each hooked D3D function ran in each Module, so a session
// log can be compared with the PM4 draws the command processor executes (--gpu_draw_census).
// Equal draw totals mean every draw the GPU sees came through a hook the Native renderer can
// take. Adapted from WWE All Stars' src/d3d_census.cpp (ADR-0004).

#pragma once

#include <cstdint>

#include "d3d/sink.h"

namespace topspin4::d3d::census {

bool Enabled();

// From the sink's Present: logs both Modules' census every 60 presents of either, and every 600 the
// draw attribution below.
void OnPresent(Module module, uint32_t device);

// Draw attribution: the device's ring write cursor (device +0x30) is read around every hooked
// original. DRAW_INDX/DRAW_INDX_2 headers written between one hooked call's exit and the next
// one's entry came from unhooked code, and are counted against that next call's guest caller
// (lr); those written inside a call are counted against the hook (inclusive of nested hooks).
void OnCallEntry(Module module, Fn fn, const uint8_t* base, uint32_t caller);
void OnCallExit(Module module, Fn fn, const uint8_t* base);

// From the sink's mirror commit: logs each distinct register group once (the mirror layout).
void OnMirrorCommit(Module module, uint32_t device, uint32_t r4, uint32_t first_register,
                    uint32_t mirror);

// The census so far, at session end.
void LogFinal();

}  // namespace topspin4::d3d::census
