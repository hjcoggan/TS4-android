// How the main menu row hooks (main_menu_rows_hooks.cpp, built into the Swing Module's DLL)
// reach the exe. The DLL cannot link against the exe, so both sides link the D3D sink's shared
// library (d3d/sink.h), which holds this too. The exe fills in the functions before any guest
// code runs; a null one is not called.

#pragma once

#include "d3d/sink.h"

namespace topspin4::settings::main_menu {

struct MenuSink {
  // PC SETTINGS was chosen: open the Settings page. Called on the guest thread.
  void (*open_page)();
  // EXIT TO DESKTOP was chosen: close the window, as its X does. Called on the guest thread.
  void (*exit_to_desktop)();
  // A line for the log: the hooks' first hits, so a run confirms the route.
  void (*log)(const char* message);
};

TS4_D3D_SINK_API MenuSink& GetMenuSink();

}  // namespace topspin4::settings::main_menu
