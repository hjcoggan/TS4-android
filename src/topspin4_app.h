#pragma once

#include <rex/platform/env.h>
#include <rex/rex_app.h>
#include <rex/system/guest_swaps.h>

#include "fault_log.h"
#include "frame_dumps.h"
#include "script_input.h"

class TopSpin4App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(rex::ui::WindowedAppContext& ctx) {
    topspin4::fault_log::Install();
    return std::unique_ptr<TopSpin4App>(new TopSpin4App(ctx, "topspin4", PPCImageConfig));
  }

 protected:
  bool SetupEnvironment() override {
    // The Classic renderer uses host render targets (fbo), the Oracle's "performance" path:
    // FSI is correct too but GPU-bound, about 40 fps in rallies on an RX 7900 XT (ticket 13).
    // Host render targets need draw extents found by running unclipped draws' vertex shaders
    // on the CPU, as Edge does. Estimated at full height instead, Swing's depth-only
    // occlusion-query pass (EDRAM base 720, 4x MSAA) wraps around the EDRAM and takes the
    // front buffer's tiles from the colour target, so every Swing frame resolves black.
    // These cvars belong to the GPU plugin, which registers them after this runs, so they are
    // set the way a late registration reads them: from REX_<NAME>, which the same option on
    // the command line still overrides.
    if (!rex::platform::env::get("REX_RENDER_TARGET_PATH_VULKAN")) {
      rex::platform::env::set("REX_RENDER_TARGET_PATH_VULKAN", "fbo");
    }
    if (!rex::platform::env::get("REX_EXECUTE_UNCLIPPED_DRAW_VS_ON_CPU")) {
      rex::platform::env::set("REX_EXECUTE_UNCLIPPED_DRAW_VS_ON_CPU", "true");
    }
    return rex::ReXApp::SetupEnvironment();
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
    topspin4::InstallScriptInput(config, &rex::system::GuestSwapCount);
  }

  void OnPostSetup() override { topspin4::frame_dumps::Install(runtime()); }
};
