// The movie player's millisecond clock, quantized to a 60 Hz grid.
//
// The boot logos and other movies (WMV3, 30 fps) are played by Loader_DLL's XUI video element.
// Each UI pass its timer may fire (8311dad8 SetTimer, 8321c1b8); the player then reads its media
// clock (83224fe8: timebase / (KeQueryPerformanceFrequency / 1000), in ms) and takes the next
// frame when that frame's presentation time minus the clock is at most 38 ms (83231b38). Movie
// frames fall every second 60 Hz swap, so each is due right at a swap boundary: a millisecond or
// two of jitter in when the timer fired and the clock was read moved frames to the swap before
// or after, which showed them for 50 ms then 16 ms where each should last 33 ms
// (docs/research/movie-cadence.md).
//
// This clock rounds each read to a 60 Hz grid whose boundaries sit half a period from where the
// player's reads fall (it learns that point from the reads themselves), so reads in one swap all
// see one time and reads a swap apart see 1000/60 ms more, as on a 360 that never misses a
// vblank. The grid follows the host clock, so the movie keeps time with its audio through loads.
// Pure: the caller passes the host time in microseconds.

#pragma once

#include <cmath>
#include <cstdint>

namespace topspin4::movie_clock {

// The id XUI's video element arms its frame timer with (Loader_DLL 8321c1b8).
inline constexpr uint32_t kVideoTimerId = 0xaabbccdd;

// The media clock after a re-sync (83225040, about every 166 ms): the game sets it to `target`
// and restarts it from `now`. The re-sync reads the clock on another thread than the player,
// so on the grid clock its `now` can be a slot (16 or 17 ms) off the player's, and taking the
// target then moved the media clock by a whole swap and the next frame with it. A target within
// kResyncDeadbandMs of where the media clock has run to (media as of `last`, advanced at `rate`)
// keeps the media clock; a seek or real drift still takes the target. Returns the new media time.
inline constexpr int64_t kResyncDeadbandMs = 20;

inline int64_t ResyncedMedia(int64_t media, int64_t last, int64_t now, int64_t target,
                             float rate) {
  const int64_t advanced = media + int64_t(float(now - last) * rate);
  const int64_t correction = target - advanced;
  return correction > kResyncDeadbandMs || correction < -kResyncDeadbandMs ? target : advanced;
}

class GridLockedClock {
 public:
  // Milliseconds, on the same scale as the host time passed in.
  int64_t Read(int64_t now_us) {
    if (!started_) {
      started_ = true;
      phase_us_ = std::fmod(double(now_us), kPeriodUs);
    } else {
      // Follow where reads fall, slowly, so one late read does not move the grid. Not wrapped
      // to one period: that would renumber the slots.
      phase_us_ += kFollow * Deviation(double(now_us));
    }
    // The slot whose centre is nearest this read: boundaries half a period from the phase.
    const int64_t slot =
        int64_t(std::floor((double(now_us) - phase_us_ + kPeriodUs / 2) / kPeriodUs));
    // Whole milliseconds without drift: 16, 17, 17, 16, 17, 17, ... per slot.
    const int64_t ms = slot * 1000 / 60;
    if (ms > last_ms_) last_ms_ = ms;
    return last_ms_;
  }

 private:
  static constexpr double kPeriodUs = 1e6 / 60;
  static constexpr double kFollow = 0.05;

  // How far a read falls from the learnt phase, in (-period/2, period/2].
  double Deviation(double now_us) const {
    double deviation = std::fmod(now_us - phase_us_, kPeriodUs);
    if (deviation > kPeriodUs / 2) deviation -= kPeriodUs;
    if (deviation <= -kPeriodUs / 2) deviation += kPeriodUs;
    return deviation;
  }

  bool started_ = false;
  double phase_us_ = 0;
  int64_t last_ms_ = INT64_MIN;
};

}  // namespace topspin4::movie_clock
