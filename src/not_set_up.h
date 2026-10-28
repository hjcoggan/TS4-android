// The game started without setup done (ticket 06): no game folders on the command line and the
// prepared disc or Title Update 1 not found (user_paths.h, SetupNeededReason). It starts the
// Launcher beside the exe with --not-set-up <reason> and ends; with no Launcher beside it, a
// message box says setup is needed. A headless run (the developer scripts, unattended) shows
// neither: it writes the reason to stderr and ends.

#pragma once

#include <filesystem>
#include <string_view>

namespace topspin4 {

// Ends the process; does not return. `user_data_root` is passed on to the Launcher when the
// command line gave one, so the Launcher reads the same folder as the game would have.
[[noreturn]] void ReportNotSetUp(std::string_view reason, const std::filesystem::path& user_data_root);

}  // namespace topspin4
