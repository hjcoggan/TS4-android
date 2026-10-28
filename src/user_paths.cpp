#include "user_paths.h"

#include <rex/filesystem.h>

namespace topspin4::user_paths {

// The folder that holds topspin4.exe. Under Wine it is the exe's Z: or C: path in the prefix's view of
// the drive, which is where the player put the game folder, so no environment lookup is needed.
std::filesystem::path ExecutableFolder() { return rex::filesystem::GetExecutableFolder(); }

std::filesystem::path DefaultUserDataRoot() { return UserDataRootIn(ExecutableFolder()); }

std::filesystem::path DefaultPreparedDataRoot() { return PreparedDataRootIn(ExecutableFolder()); }

std::filesystem::path DefaultCacheRoot() { return CacheRootIn(ExecutableFolder()); }

}  // namespace topspin4::user_paths
