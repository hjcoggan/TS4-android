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
