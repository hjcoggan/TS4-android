// 60 fps cutscenes (ts4_cutaways_60, the Graphics setting "60 fps cutscenes"; off by default).
//
// Swing picks a render mode per scene; mode 1 (match intro, close-ups between points, replays,
// the changeover) is the 30 fps one (docs/research/cutaway-rate.md):
//   88739a40  the frame's render setup: present interval 2 at device+0x35f0 in mode 1, else 1
//   887c6b98  the world step: dt 0.03333f (0x880d1088) in mode 1, else 0.01666f, passed in f1
//             to 881e5de8 once per rendered frame
// In mode 1 the game reaches its cutaway services (the cutscene and animation managers among
// them) through a scheduler list with two parents, so the cutaway logic steps at 60 Hz and
// every other step is presented. With the setting on, mode 1 presents at interval 1, the extra
// step's scheduler (886117e8) is skipped so that logic runs once a frame, the world steps by
// 1/60, and the face player (8817c608), which steps once per presented frame, holds on odd
// frames.
// The cutaways then run at 60 at their original speed (the intro: 61.5 s against 62.2 s).
//
// The hooks are built into the Swing Module's DLL (cutaway_rate_hooks.cpp); they reach the exe
// through this sink, which lives in the D3D sink's shared library (d3d/sink.h).

#pragma once

#include <cstdint>

#include "cutaway_rate_rules.h"
#include "d3d/sink.h"

namespace topspin4::cutaway_rate {

struct Sink {
  // Whether cutaways run at 60 (ts4_cutaways_60).
  bool (*sixty)();
  // A line for the log: the hooks' first hits, so a run confirms the route.
  void (*log)(const char* message);
};

TS4_D3D_SINK_API Sink& GetSink();

// The exe fills the sink (cutaway_rate_host.cpp), before any guest code runs.
void InstallHost();

}  // namespace topspin4::cutaway_rate
