// The white flash after a Swing loading screen (an original glitch, kept unless
// ts4_fix_glitches is on). When Swing's loading screen ends, the first frames of the new scene
// are presented before any world draws: the frame holds only a full-screen quad textured with
// the renderer's 1x1 white "InternalNullTexture", so the screen flashes solid white. Xenia Edge
// shows it too.
//
// The fix holds the loading screen's last frame instead: Swing's Present wrapper (8875f498)
// skips the first presents after the loading screen's loop (88905f70) returns. Built into the
// Swing Module's DLL (load_flash_hooks.cpp); it reaches the exe through this sink, which lives
// in the D3D sink's shared library (d3d/sink.h).

#pragma once

#include <cstdint>

#include "d3d/sink.h"
#include "load_flash_hold.h"

namespace topspin4::load_flash {

// Presents skipped after a loading screen ends.
inline constexpr uint32_t kHeldPresents = 3;

struct Sink {
  // Presents to skip after the next loading screen: kHeldPresents with the fix on, else 0.
  uint32_t (*held_presents)();
  // A line for the log: the hooks' first hits, so a run confirms the route.
  void (*log)(const char* message);
};

TS4_D3D_SINK_API Sink& GetSink();

// The exe fills the sink (load_flash_host.cpp), before any guest code runs.
void InstallHost();

}  // namespace topspin4::load_flash
