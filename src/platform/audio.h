// One-shot UI sound (the achievement toast) without Win32 in shared code.
//
// Windows: PlaySoundW(SND_ASYNC). Android: no-op for now; SDL's audio device is owned by the
// SDK's audio driver, and mixing a second stream into it is a later task (docs/app-port.md).

#pragma once

#include <filesystem>

namespace topspin4::platform {

// Plays a WAV without blocking. Silent and harmless when the file is missing.
void PlaySoundFileAsync(const std::filesystem::path& wav);

}  // namespace topspin4::platform
