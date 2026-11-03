#include <memory>

#if defined(TOPSPIN4_NO_GUEST_CODE)

#include <cstdio>

#include <rex/ui/windowed_app.h>
#include <rex/ui/windowed_app_context.h>

// A tree without codegen output links no guest code: the host only proves the toolchain
// and the SDK build, then quits (ticket 03).
class NoGuestCodeApp : public rex::ui::WindowedApp {
 public:
  explicit NoGuestCodeApp(rex::ui::WindowedAppContext& ctx) : WindowedApp(ctx, "topspin4") {}

  static std::unique_ptr<rex::ui::WindowedApp> Create(rex::ui::WindowedAppContext& ctx) {
    return std::make_unique<NoGuestCodeApp>(ctx);
  }

  bool OnInitialize() override {
    // Logging is set up by ReXApp, which this app does not run; stdout reaches the smoke run.
    std::fputs("topspin4: built without recompiled code; run scripts/build.ps1 -Action Codegen\n", stdout);
    std::fflush(stdout);
    app_context().RequestDeferredQuit();
    return true;
  }
};

REX_DEFINE_APP(topspin4, NoGuestCodeApp::Create)

#else

#include "generated/boot/topspin4_init.h"

#include "topspin4_app.h"

REX_DEFINE_APP(topspin4, TopSpin4App::Create)

#endif
