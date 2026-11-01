// The cvars that carry the player settings (settings/registry.h), loaded from the settings file
// at start (topspin4_app.h). Each default must be the registry's.

#include <rex/cvar.h>

#include "settings/registry.h"

REXCVAR_DEFINE_STRING(ts4_renderer, topspin4::settings::kRendererClassic.data(), "TopSpin4",
                      "Renderer: classic (Xenos GPU emulation) or native (not built yet; Classic "
                      "draws until it is)")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

// Safe start (ticket 09): one session on all defaults. The Settings file is neither applied nor
// written, and the log and session report say so. The Launcher's Safe start button passes it.
REXCVAR_DEFINE_BOOL(safe, false, "TopSpin4",
                    "Safe start: run this session on all default settings, without reading or "
                    "writing the Settings file");

// Image Enhancements (image-quality spec; src/native/image_quality.h). Each changes while the game
// runs: Native reads them as each frame begins, and the change callbacks of the two that Classic
// has too set its filtering and the presenter's CAS (native_renderer.cpp, ApplyImageSettings).
REXCVAR_DEFINE_STRING(ts4_resolution_scale, "1", "TopSpin4",
                      "Resolution scale (Native renderer): 1, 2 or 3 times 1280x720, or auto "
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

// Fixes of the original game's own glitches (load_flash.h), off by default: the original bugs
// are kept unless the player asks.
REXCVAR_DEFINE_BOOL(ts4_fix_glitches, false, "TopSpin4",
                    "Fix original glitches: hold the loading screen over the white flash before "
                    "a new scene draws")
    .lifecycle(rex::cvar::Lifecycle::kHotReload);

// 60 fps cutscenes (cutaway_rate.h): the game's 30 fps cutaways presented at 60, at their
// original speed (docs/research/cutaway-rate.md). Off keeps the original 30.
REXCVAR_DEFINE_BOOL(ts4_cutaways_60, false, "TopSpin4",
                    "60 fps cutscenes: the match intro, close-ups, replays and changeovers at "
                    "60 fps instead of 30, at their original speed")
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
