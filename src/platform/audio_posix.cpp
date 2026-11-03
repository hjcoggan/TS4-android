// POSIX / Android implementation of platform/audio.h: no-op.
//
// The game's audio goes through the SDK's SDL audio driver. The toast's WAV is a one-shot effect
// outside it; on Android there is no PlaySound equivalent without opening a second SDL audio
// stream. Left silent, and logged once so the gap is visible.

#include "platform/audio.h"

#include <mutex>

#include <rex/logging.h>

namespace topspin4::platform {

void PlaySoundFileAsync(const std::filesystem::path& wav) {
  static std::mutex mutex;
  static bool warned = false;
  std::lock_guard lock(mutex);
  if (warned) return;
  warned = true;
  REXLOG_INFO("audio: UI sounds are not implemented on this platform ({} would play)", wav.string());
}

}  // namespace topspin4::platform
