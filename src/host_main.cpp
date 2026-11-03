// Built when TS4_WITH_REXGLUE=OFF: proves the toolchain, the NDK and the presets without the SDK.
// With the SDK on, src/main.cpp (app creator) and src/android_main.cpp (SDL_main) take over.
#include <cstdio>

#if defined(__ANDROID__)
// SDLActivity looks for this symbol; without the SDK there is nothing to run.
extern "C" int SDL_main(int, char**) {
  std::puts("topspin4: built without the ReXGlue SDK (TS4_WITH_REXGLUE=OFF)");
  return 1;
}
#else
int main() {
  std::puts("topspin4: built without the ReXGlue SDK (TS4_WITH_REXGLUE=OFF)");
  return 0;
}
#endif
