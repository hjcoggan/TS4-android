// Going back to the title screen relaunches the game (relaunch_plan.h). When the guest asks for
// that, the process ends as usual and then starts a new copy with the guest's launch data, waits
// for it and exits with its exit code, so whoever started the game (the Launcher, a scene run)
// keeps one process to wait on and still sees a crash. Windows only; elsewhere the game ends.

#pragma once

namespace rex::system {
class KernelState;
}

namespace topspin4::relaunch {

// Before the guest runs: hands the launch data the previous copy left (--ts4_launch_data) to
// XamLoaderGetLaunchData, and removes the file.
void RestoreLaunchData(rex::system::KernelState& kernel_state);

// After the guest's main thread has ended: if it asked to launch its own executable, the new
// copy starts once this process has shut down.
void OnGuestExit(rex::system::KernelState& kernel_state);

}  // namespace topspin4::relaunch
