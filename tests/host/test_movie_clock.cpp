// src/movie_clock.h: the movie player's millisecond clock, quantized to a 60 Hz grid placed
// away from where the player's own reads fall.

#include <catch_amalgamated.hpp>

#include <cmath>
#include <cstdint>
#include <random>
#include <vector>

#include "movie_clock.h"

using topspin4::movie_clock::GridLockedClock;

namespace {

constexpr double kSwapUs = 1e6 / 60;

// The movie's own rule (Loader_DLL 83224e38, 83231b38): its media clock starts at 0 from a read
// at the start, and a frame is taken on the first read where its presentation time (whole ms in
// the file: 0, 33, 66, 100, ...) minus the media clock is at most 38 ms. The player reads the
// clock once a swap at `phase_us` after the swap, give or take `jitter_us`. Returns the swap each
// frame was taken on.
std::vector<uint64_t> TakenOnSwaps(bool locked, double phase_us, double jitter_us, uint32_t seed) {
  std::mt19937 random(seed);
  std::uniform_real_distribution<double> jitter(-jitter_us, jitter_us);
  GridLockedClock clock;
  const auto read = [&](uint64_t swap) {
    const double now_us = 5e6 + swap * kSwapUs + phase_us + jitter(random);
    return locked ? clock.Read(int64_t(now_us)) : int64_t(now_us / 1000);
  };
  std::vector<uint64_t> taken;
  const uint64_t start = 3 + seed % 7;
  const int64_t start_ms = read(start);
  uint32_t next_frame = 0;
  for (uint64_t swap = start; swap < start + 600; ++swap) {
    const int64_t media = read(swap) - start_ms;
    const int64_t pts = int64_t(next_frame) * 1000 / 30;
    if (pts - media <= 38) {
      taken.push_back(swap);
      ++next_frame;
    }
  }
  return taken;
}

// Picture changes not two swaps apart, after the start: taking frames up to 38 ms early, the
// player shows its first three on consecutive swaps on any clock, a 360's too.
int OffStep(const std::vector<uint64_t>& taken) {
  int off = 0;
  for (size_t i = 3; i < taken.size(); ++i) off += taken[i] - taken[i - 1] != 2;
  return off;
}

}  // namespace

TEST_CASE("the game's own read of the wall clock shows the bug") {
  // Measured: reads within +-2.5 ms of one point in the frame; two reads +-3 ms apart already
  // use up the 4.67 ms a frame has before the swap boundary.
  int off = 0;
  for (uint32_t seed = 1; seed <= 8; ++seed) off += OffStep(TakenOnSwaps(false, 2400, 3000, seed));
  CHECK(off > 5);
}

TEST_CASE("on the grid clock a 30 fps movie changes on every second swap, at any phase") {
  for (double phase = 0; phase < kSwapUs; phase += 1000) {
    for (uint32_t seed = 1; seed <= 3; ++seed) {
      INFO("phase " << phase << " us, seed " << seed);
      CHECK(OffStep(TakenOnSwaps(true, phase, 2500, seed)) == 0);
    }
  }
}

TEST_CASE("reads around one point in a swap get one time, and a swap adds 1000/60 ms") {
  GridLockedClock clock;
  const int64_t first = clock.Read(1'000'000);
  CHECK(clock.Read(1'002'000) == first);
  CHECK(clock.Read(998'000) == first);
  // 1000/60 ms in whole milliseconds: 16 or 17 per swap, 33 or 34 per two, exactly 100 per six.
  const int64_t one = clock.Read(1'016'667) - first;
  CHECK((one == 16 || one == 17));
  const int64_t two = clock.Read(1'033'333 + 1500) - first;
  CHECK((two == 33 || two == 34));
  CHECK(clock.Read(1'100'000 - 1500) == first + 100);
}

TEST_CASE("it follows the wall clock through a load") {
  GridLockedClock clock;
  const int64_t first = clock.Read(0);
  const int64_t after = clock.Read(1'000'000);
  CHECK(std::abs((after - first) - 1000) <= 9);
}

TEST_CASE("it never runs backwards") {
  GridLockedClock clock;
  std::mt19937 random(7);
  std::uniform_int_distribution<int> step(0, 40'000);
  int64_t now = 0;
  int64_t last = clock.Read(now);
  for (int i = 0; i < 2000; ++i) {
    now += step(random);
    const int64_t read = clock.Read(now);
    CHECK(read >= last);
    last = read;
  }
}

TEST_CASE("a re-sync within a frame of the media clock keeps the media clock") {
  using topspin4::movie_clock::ResyncedMedia;
  // media 150 as of the read at 1000, now 1017: the clock has run on to 167.
  CHECK(ResyncedMedia(150, 1000, 1017, 166, 1.0f) == 167);  // rounding
  CHECK(ResyncedMedia(150, 1000, 1017, 183, 1.0f) == 167);  // a slot off on another thread
  CHECK(ResyncedMedia(150, 1000, 1017, 151, 1.0f) == 167);
  // A seek, or audio that really drifted: the target wins.
  CHECK(ResyncedMedia(150, 1000, 1017, 5000, 1.0f) == 5000);
  CHECK(ResyncedMedia(150, 1000, 1017, 130, 1.0f) == 130);
  // Paused (rate 0): the media clock stands still, so the target is compared with 150.
  CHECK(ResyncedMedia(150, 1000, 1017, 160, 0.0f) == 150);
}
