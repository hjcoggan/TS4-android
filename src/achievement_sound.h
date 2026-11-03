// Which sound the achievement toast plays: the player's own userdata\achievement.wav if they put
// one there, else the one the game ships (sounds\achievement.wav beside topspin4.exe, from
// data/sounds in the repo), else none.

#pragma once

#include <filesystem>
#include <system_error>

namespace topspin4::achievement_toast {

inline std::filesystem::path PlayerSoundIn(const std::filesystem::path& user_data_root) {
  return user_data_root / "achievement.wav";
}

inline std::filesystem::path ShippedSoundIn(const std::filesystem::path& game_folder) {
  return game_folder / "sounds" / "achievement.wav";
}

// Empty when neither file is there.
inline std::filesystem::path ChooseSound(const std::filesystem::path& user_data_root,
                                         const std::filesystem::path& game_folder) {
  std::error_code error;
  for (const auto& candidate : {PlayerSoundIn(user_data_root), ShippedSoundIn(game_folder)}) {
    if (!candidate.parent_path().empty() && std::filesystem::is_regular_file(candidate, error)) {
      return candidate;
    }
  }
  return {};
}

}  // namespace topspin4::achievement_toast
