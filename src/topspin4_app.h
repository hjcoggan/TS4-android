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
    // The Classic renderer is the FSI render-target path, as in WWE All Stars (its ADR-0003).
    // On the host render-target path Swing's frames (2x MSAA in two predicated tiles) come
    // out black. The cvar belongs to the GPU plugin, which registers it after this runs, so
    // it is set the way a late registration reads it: from REX_<NAME>, which a
    // --render_target_path_vulkan on the command line still overrides.
    if (!rex::platform::env::get("REX_RENDER_TARGET_PATH_VULKAN")) {
      rex::platform::env::set("REX_RENDER_TARGET_PATH_VULKAN", "fsi");
    }
    return rex::ReXApp::SetupEnvironment();
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
    topspin4::InstallScriptInput(config, &rex::system::GuestSwapCount);
  }

  void OnPostSetup() override { topspin4::frame_dumps::Install(runtime()); }
};
