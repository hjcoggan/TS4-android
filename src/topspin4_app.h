#pragma once

#include <rex/rex_app.h>

#include "fault_log.h"

class TopSpin4App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(rex::ui::WindowedAppContext& ctx) {
    topspin4::fault_log::Install();
    return std::unique_ptr<TopSpin4App>(new TopSpin4App(ctx, "topspin4", PPCImageConfig));
  }
};
