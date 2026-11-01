// 60 fps cutaways (ts4_cutaways_60): a developer switch, off by default, not a player setting.
//
// Swing picks a render mode per scene; mode 1 (match intro, close-ups between points, replays,
// the changeover) is the 30 fps one (docs/research/cutaway-rate.md):
//   88739a40  the frame's render setup: present interval 2 at device+0x35f0 in mode 1, else 1
//   887c6b98  the world step: dt 0.03333f (0x880d1088) in mode 1, else 0.01666f, passed in f1
//             to 881e5de8 once per rendered frame
// With the switch on, the hooks present mode 1 at interval 1 and step the world by the 60 fps
// dt. That is not enough: the cutaways advance per rendered frame in many other systems too, so
// the match intro took 35.6 s instead of the original 62.2 s (31.4 s at interval 1 alone). A
// correct-speed 60 needs interpolation between the 30 Hz states; these hooks are its handles.
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
