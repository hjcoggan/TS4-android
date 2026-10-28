// Developer build or release build (ticket 12). CMake defines TOPSPIN4_DEVELOPER_BUILD unless
// configured with -DTOPSPIN4_DEVELOPER_BUILD=OFF. A developer build keeps the SDK's F6 cvar
// overlay, which lists every debug cvar and cannot be driven with a pad; a release build leaves
// it out, so players have one settings screen. The scripts and the scene runner build the
// default, a developer build.

#pragma once

namespace topspin4 {

#if defined(TOPSPIN4_DEVELOPER_BUILD)
inline constexpr bool kDeveloperBuild = true;
#else
inline constexpr bool kDeveloperBuild = false;
#endif

}  // namespace topspin4
