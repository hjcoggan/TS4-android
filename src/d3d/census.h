// --ts4_d3d_census: logs how often each hooked D3D function ran in each Module, so a session
// log can be compared with the PM4 draws the command processor executes (--gpu_draw_census).
// Equal draw totals mean every draw the GPU sees came through a hook the Native renderer can
// take. Adapted from WWE All Stars' src/d3d_census.cpp (ADR-0004).

#pragma once

#include <cstdint>

#include "d3d/sink.h"

namespace topspin4::d3d::census {

bool Enabled();

// From the sink's Present: logs the census every 60 presents of a Module.
void OnPresent(Module module);

// From the sink's mirror commit: logs each distinct register group once (the mirror layout).
void OnMirrorCommit(Module module, uint32_t device, uint32_t r4, uint32_t first_register,
                    uint32_t mirror);

// The census so far, at session end.
void LogFinal();

}  // namespace topspin4::d3d::census
