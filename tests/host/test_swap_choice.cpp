// rex/system/swap_choice.h (patches/rexglue 0013, 0039): which Native frame a guest swap shows.
// A frame's sequence is the guest swap clock at its Present, so swap N should show frame N: then
// the presenter's image of a swap is the frame Native drew for it, as frame dumps assume.

#include <catch_amalgamated.hpp>

#include <cstdint>
#include <optional>

#include <rex/system/swap_choice.h>

using rex::system::external_frame::ChooseSwapFrame;
using rex::system::external_frame::SwapChoice;

TEST_CASE("a swap shows its own frame, whether the GPU is level with the app or a frame behind") {
  // Level: frame 10 is the newest.
  SwapChoice c = ChooseSwapFrame(std::optional<uint64_t>(9), 10, 10, 1);
  CHECK(c.show == 10);
  CHECK_FALSE(c.repeated);
  CHECK(c.skipped == 0);
  // A frame behind: the app has published 11 before the GPU reaches swap 10.
  c = ChooseSwapFrame(std::optional<uint64_t>(9), 10, 11, 1);
  CHECK(c.show == 10);
  CHECK(c.skipped == 0);
}

TEST_CASE("the first swap shows its own frame, not the newest one") {
  // The old rule showed the newest (11) and stayed a frame off the swap clock after it.
  const SwapChoice c = ChooseSwapFrame(std::nullopt, 10, 11, 1);
  CHECK(c.show == 10);
}

TEST_CASE("after a repeat or a skip the next swap is back on its own frame") {
  // The previous swap showed 12 (a frame ahead of its own); swap 13 still shows 13, and the
  // one after a skip shows its own as well.
  CHECK(ChooseSwapFrame(std::optional<uint64_t>(12), 13, 14, 1).show == 13);
  CHECK(ChooseSwapFrame(std::optional<uint64_t>(10), 13, 13, 1).show == 13);
  CHECK(ChooseSwapFrame(std::optional<uint64_t>(10), 13, 13, 1).skipped == 2);
}

TEST_CASE("a swap whose frame the app may be drawing into again shows the oldest intact one") {
  // Three frames behind with a lag of 1: frame 10 is no longer intact, 13 is.
  const SwapChoice c = ChooseSwapFrame(std::optional<uint64_t>(9), 10, 14, 1);
  CHECK(c.show == 13);
  CHECK(c.skipped == 3);
}

TEST_CASE("a swap the app published no frame for shows the frame before it") {
  // Frame 11 was never published (that Present did not reach the app's renderer); 10 is still
  // intact at a lag of 2.
  auto available = [](uint64_t s) { return s != 11; };
  SwapChoice c = ChooseSwapFrame(std::optional<uint64_t>(10), 11, 12, 2, available);
  CHECK(c.show == 10);
  CHECK(c.repeated);
  // A swap ahead of every published frame shows the newest.
  c = ChooseSwapFrame(std::optional<uint64_t>(12), 14, 12, 1);
  CHECK(c.show == 12);
  CHECK(c.repeated);
}
