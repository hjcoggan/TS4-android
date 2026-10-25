// The cvars that carry the player settings (settings/registry.h), loaded from the settings file
// at start (topspin4_app.h). Each default must be the registry's.

#include <rex/cvar.h>

#include "settings/registry.h"

REXCVAR_DEFINE_STRING(ts4_renderer, topspin4::settings::kRendererClassic.data(), "TopSpin4",
                      "Renderer: classic (Xenos GPU emulation) or native (not built yet; Classic "
                      "draws until it is)")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

// Read by the Agassi outfit unlock (ticket 25) when the game builds its unlock list.
REXCVAR_DEFINE_BOOL(ts4_unlock_agassi_outfit, false, "TopSpin4",
                    "Make the Agassi preorder outfit selectable")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

// Controller remapping (ticket 24, pad_bindings.h): the physical input that drives each input
// the game sees. Read when the input system is built (remapped_input.cpp).
#define TS4_PAD_BINDING(input)                                                                 \
  REXCVAR_DEFINE_STRING(ts4_pad_##input, #input, "TopSpin4/Controller",                        \
                        "Controller: the physical input that drives the game's " #input)       \
      .lifecycle(rex::cvar::Lifecycle::kRequiresRestart)
TS4_PAD_BINDING(a);
TS4_PAD_BINDING(b);
TS4_PAD_BINDING(x);
TS4_PAD_BINDING(y);
TS4_PAD_BINDING(lb);
TS4_PAD_BINDING(rb);
TS4_PAD_BINDING(lt);
TS4_PAD_BINDING(rt);
TS4_PAD_BINDING(ls);
TS4_PAD_BINDING(rs);
TS4_PAD_BINDING(back);
TS4_PAD_BINDING(start);
TS4_PAD_BINDING(up);
TS4_PAD_BINDING(down);
TS4_PAD_BINDING(left);
TS4_PAD_BINDING(right);
