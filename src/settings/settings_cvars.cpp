// The cvars that carry the player settings (settings/registry.h), loaded from the settings file
// at start (topspin4_app.h). Each default must be the registry's.

#include <rex/cvar.h>

#include "settings/registry.h"

REXCVAR_DEFINE_STRING(ts4_renderer, topspin4::settings::kRendererClassic.data(), "TopSpin4",
                      "Renderer: classic (Xenos GPU emulation) or native (not built yet; Classic "
                      "draws until it is)")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

// Image Enhancements (image-quality spec; src/native/image_quality.h). Each changes while the game
// runs: Native reads them as each frame begins, and the change callbacks of the two that Classic
// has too set its filtering and the presenter's CAS (native_renderer.cpp, ApplyImageSettings).
REXCVAR_DEFINE_STRING(ts4_resolution_scale, "1", "TopSpin4",
                      "Resolution scale (Native renderer): 1, 2, 3 or 4 times 1280x720, or auto "
                      "(the smallest that covers the window's 16:9 area)")
    .lifecycle(rex::cvar::Lifecycle::kHotReload);
REXCVAR_DEFINE_STRING(ts4_msaa, "original", "TopSpin4",
                      "MSAA (Native renderer): original (the game's own), 4 or 8")
    .lifecycle(rex::cvar::Lifecycle::kHotReload);
REXCVAR_DEFINE_STRING(ts4_anisotropic_filtering, "original", "TopSpin4",
                      "Anisotropic filtering: original (the game's own), 4, 8 or 16")
    .lifecycle(rex::cvar::Lifecycle::kHotReload);
REXCVAR_DEFINE_STRING(ts4_sharpening, "off", "TopSpin4",
                      "Sharpening (CAS): off, low, medium or high")
    .lifecycle(rex::cvar::Lifecycle::kHotReload);

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
