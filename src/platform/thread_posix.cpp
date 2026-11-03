// POSIX / Android implementation of platform/thread.h.
//
// setpriority(PRIO_PROCESS, 0, ...) needs no privileges when raising the nice value (kLow) and
// only CAP_SYS_NICE (absent on Android) to lower it, so kHigh stays at the default nice level
// there. Windows uses THREAD_PRIORITY_*.

#include "platform/thread.h"

#include <sys/resource.h>
#include <sys/types.h>

namespace topspin4::platform {

void SetCurrentThreadPriority(ThreadPriority priority) {
  int nice_value = 0;
  switch (priority) {
    case ThreadPriority::kLow:
      nice_value = 10;
      break;
    case ThreadPriority::kHigh:
      nice_value = -10;
      break;
    case ThreadPriority::kNormal:
    default:
      nice_value = 0;
      break;
  }
  setpriority(PRIO_PROCESS, 0, nice_value);
}

}  // namespace topspin4::platform
