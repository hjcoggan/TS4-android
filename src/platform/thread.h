// Current-thread priority without Win32 in shared code (native/pipelines.cpp).

#pragma once

namespace topspin4::platform {

enum class ThreadPriority { kLow, kNormal, kHigh };

// Changes the calling thread's priority. Best effort: an unprivileged Android app cannot use
// SCHED_FIFO, so Android lowers/raises the nice value instead.
void SetCurrentThreadPriority(ThreadPriority priority);

}  // namespace topspin4::platform
