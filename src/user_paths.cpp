#include "user_paths.h"

#include "platform/paths.h"

namespace topspin4::user_paths {

// The folder the install is rooted at. Windows: the folder that holds topspin4.exe (under Wine,
// the exe's Z: or C: path in the prefix's view of the drive). Android: the app's data folder,
// set by the activity as REX_APP_FOLDER, because /proc/self/exe is /system/bin/app_process64 and
// has no writable neighbours. platform/paths.h owns that difference.
std::filesystem::path ExecutableFolder() { return platform::BaseFolder(); }

std::filesystem::path DefaultUserDataRoot() { return UserDataRootIn(ExecutableFolder()); }

std::filesystem::path DefaultPreparedDataRoot() { return PreparedDataRootIn(ExecutableFolder()); }

std::filesystem::path DefaultCacheRoot() { return CacheRootIn(ExecutableFolder()); }

}  // namespace topspin4::user_paths
