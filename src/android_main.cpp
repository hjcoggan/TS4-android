// Android entry point.
//
// The game is a shared library (libtopspin4.so) loaded by TopSpin4Activity (an SDLActivity
// subclass). SDL3's Java layer calls the native function named by SDLActivity.getMainFunction(),
// "SDL_main"; <SDL3/SDL_main.h> renames our main() to SDL_main on Android and supplies the JNI
// glue (SDL_MAIN_EXPORTED). The SDK's own src/ui/windowed_app_main_sdl.cpp defines a plain
// main(), which SDLActivity cannot find, so on Android that file is not compiled and this one
// takes its place.
//
// Adapted from rexglue-sdk src/ui/windowed_app_main_sdl.cpp (BSD-3-Clause, Tom Clay), minus the
// Win32 branch.

#include <SDL3/SDL_main.h>
#include <SDL3/SDL_system.h>

#include <android/log.h>

#include <algorithm>
#include <cstdlib>
#include <map>
#include <memory>
#include <string>
#include <vector>

#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/platform.h>
#include <rex/ui/windowed_app.h>
#include <rex/ui/windowed_app_context_sdl.h>

namespace {

int RunWindowedApp(int argc, char** argv) {
  // SDL is linked statically into librexruntime.so by the SDK, so its JNI_OnLoad (which captures
  // the JavaVM) lives there, not in this library. ART calls JNI_OnLoad only for the library named
  // in System.loadLibrary(), so the activity must load the runtime library before "topspin4"
  // (getLibraries() = { "rexruntimed", "topspin4" } in a Debug build). Without it SDL video init
  // fails with no clear reason; say so.
  if (SDL_GetAndroidJNIEnv() == nullptr) {
    __android_log_print(ANDROID_LOG_ERROR, "topspin4",
                        "no JavaVM: load the ReXGlue runtime library (librexruntimed.so) before "
                        "libtopspin4.so in TopSpin4Activity.getLibraries()");
  }

  auto remaining = rex::cvar::Init(argc, argv);
  rex::cvar::ApplyEnvironment();
  rex::InitLoggingEarly();

  int result;
  {
    rex::ui::SDLWindowedAppContext app_context;
    if (!app_context.Initialize()) {
      return EXIT_FAILURE;
    }

    // In a library (Android), apps register themselves by name (REX_DEFINE_APP) instead of
    // each executable exporting GetWindowedAppCreator.
#if XE_UI_WINDOWED_APPS_IN_LIBRARY
    rex::ui::WindowedApp::Creator creator = rex::ui::WindowedApp::GetCreator("topspin4");
#else
    rex::ui::WindowedApp::Creator creator = rex::ui::GetWindowedAppCreator();
#endif
    if (!creator) {
      REXLOG_ERROR("topspin4: no windowed app registered");
      return EXIT_FAILURE;
    }
    std::unique_ptr<rex::ui::WindowedApp> app = creator(app_context);

    // Match remaining positional args to the app's expected options.
    const auto& option_names = app->GetPositionalOptions();
    std::map<std::string, std::string> parsed;
    size_t count = std::min(remaining.size(), option_names.size());
    for (size_t i = 0; i < count; ++i) {
      parsed[option_names[i]] = remaining[i];
    }
    app->SetParsedArguments(std::move(parsed));

    result = app->OnInitialize() ? app_context.RunMainMessageLoop() : EXIT_FAILURE;

    app->InvokeOnDestroy();
  }

  return result;
}

}  // namespace

int main(int argc, char* argv[]) { return RunWindowedApp(argc, argv); }
