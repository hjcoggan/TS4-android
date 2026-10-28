// How the OPTIONS row hooks (options_row_hooks.cpp, built into the Swing Module's DLL) reach the
// exe. The DLL cannot link against the exe, so both sides link the D3D sink's shared library
// (d3d/sink.h), which holds this too. The exe fills in the functions before any guest code
// runs; a null one is not called.

#pragma once

#include "d3d/sink.h"

namespace topspin4::settings::options_row {

struct RowSink {
  // The row was chosen: open the Settings page. Called on the guest thread.
  void (*open_page)();
  // A line for the log: the hooks' first hits, so a run confirms the route.
  void (*log)(const char* message);
};

TS4_D3D_SINK_API RowSink& GetRowSink();

}  // namespace topspin4::settings::options_row
