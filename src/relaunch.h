// Going back to the title screen relaunches the game (relaunch_plan.h). Xenia does that inside
// its window; the SDK can only end the title, so the port hands over to a new copy without the
// player seeing a window close:
//   - the old copy writes the guest's launch data, starts the new copy (its window hidden, sized
//     and placed like the old one, fullscreen or not) and keeps its own window up;
//   - the new copy hands the launch data to XamLoaderGetLaunchData, and on its first shown frame
//     shows its window in the same place, takes the focus and tells the old copy;
//   - the old copy then hides its window, waits for the new copy and exits with its exit code,
//     so whoever started the game (the Launcher, a scene run) keeps one process to wait on and
//     still sees a crash.
// Windows only; elsewhere the game ends.

#pragma once

#include <cstdint>
#include <functional>

namespace rex::system {
class KernelState;
}
namespace rex::ui {
class Window;
}

namespace topspin4::relaunch {

// Before the guest runs: hands the launch data the previous copy left (--ts4_launch_data) to
// XamLoaderGetLaunchData, and removes the file. `window` is the one shown in the old one's
// place; `run_on_ui` runs a function on the UI thread, where it is shown.
void Configure(rex::system::KernelState& kernel_state, rex::ui::Window* window,
               std::function<void(std::function<void()>)> run_on_ui);

// True while this copy, started by a relaunch, waits for its first shown frame.
bool WaitingForFirstFrame();

// From the frame-shown observer (frame_dumps.cpp).
void OnFrameShown(uint64_t frame);

// After the guest's main thread has ended: if it asked to launch its own executable, hands over
// to a new copy and does not return (unless that copy cannot be started).
void OnGuestExit(rex::system::KernelState& kernel_state);

}  // namespace topspin4::relaunch
