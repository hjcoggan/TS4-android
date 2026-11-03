// Windows implementation of platform/audio.h: PlaySoundW, asynchronous.

#include "platform/audio.h"

#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <mmsystem.h>

namespace topspin4::platform {

void PlaySoundFileAsync(const std::filesystem::path& wav) {
  if (wav.empty()) return;
  PlaySoundW(wav.c_str(), nullptr, SND_FILENAME | SND_ASYNC | SND_NODEFAULT);
}

}  // namespace topspin4::platform
