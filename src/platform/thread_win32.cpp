// Windows implementation of platform/thread.h.

#include "platform/thread.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

namespace topspin4::platform {

void SetCurrentThreadPriority(ThreadPriority priority) {
  int value = THREAD_PRIORITY_NORMAL;
  switch (priority) {
    case ThreadPriority::kLow:
      value = THREAD_PRIORITY_LOWEST;
      break;
    case ThreadPriority::kHigh:
      value = THREAD_PRIORITY_HIGHEST;
      break;
    case ThreadPriority::kNormal:
    default:
      value = THREAD_PRIORITY_NORMAL;
      break;
  }
  SetThreadPriority(GetCurrentThread(), value);
}

}  // namespace topspin4::platform
