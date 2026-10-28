// rex/system/swap_choice.h (patches/rexglue 0013, 0039, 0040): which Native frame a guest swap shows.
// A frame's sequence is the guest swap clock at its Present, so swap N should show frame N: then
// the presenter's image of a swap is the frame Native drew for it, as frame dumps assume.

#include <catch_amalgamated.hpp>

#include <cstddef>
#include <cstdint>
#include <deque>
#include <optional>
#include <set>
#include <utility>
#include <vector>

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

TEST_CASE("a swap the app published no frame for shows the next newer one, never an older one") {
  // Frame 11 was never published (that Present did not reach the app's renderer). At the shipped
  // lag of 1 only 11 and 12 are intact, so the swap shows 12.
  auto available = [](uint64_t s) { return s != 11; };
  SwapChoice c = ChooseSwapFrame(std::optional<uint64_t>(10), 11, 12, 1, available);
  CHECK(c.show == 12);
  CHECK_FALSE(c.repeated);
  // A larger lag keeps 10 intact too, but the shown frame still does not step back to it.
  c = ChooseSwapFrame(std::optional<uint64_t>(10), 11, 12, 2, available);
  CHECK(c.show == 12);
  // The swap after it has its own frame 12 again: a repeat, not a step back.
  c = ChooseSwapFrame(std::optional<uint64_t>(12), 12, 12, 1, available);
  CHECK(c.show == 12);
  CHECK(c.repeated);
}

TEST_CASE("a swap ahead of every published frame shows the newest") {
  const SwapChoice c = ChooseSwapFrame(std::optional<uint64_t>(12), 14, 12, 1);
  CHECK(c.show == 12);
  CHECK(c.repeated);
}

TEST_CASE("the shown frame never steps back, with frames missing and at any lag") {
  for (uint64_t lag = 1; lag <= 3; ++lag) {
    for (uint64_t missing_mask = 0; missing_mask < 64; ++missing_mask) {
      // Frames 0..39 published except those whose bit in the mask (frame % 6) is set; the GPU is
      // `behind` swaps behind the app.
      auto available = [&](uint64_t s) { return !((missing_mask >> (s % 6)) & 1); };
      for (uint64_t behind = 0; behind <= 3; ++behind) {
        std::optional<uint64_t> last;
        for (uint64_t swap = 0; swap < 36; ++swap) {
          uint64_t latest = swap + behind;
          while (latest > swap && !available(latest)) {
            --latest;  // the newest published frame
          }
          const SwapChoice c = ChooseSwapFrame(last, swap, latest, lag, available);
          if (last) {
            REQUIRE(c.show >= *last);
          }
          last = c.show;
        }
      }
    }
  }
}

// The numbering across a VdSwap that issues no swap packet (an invalid front buffer): the swap is
// not counted either, so the app's frame numbers (GuestSwapCount at Present) and the GPU's swap
// numbers (FramesShownCount) stay on one clock. The model follows xboxkrnl_video.cpp: VdSwap
// counts a swap only where it writes the packet.
namespace {
struct Session {
  uint64_t guest_swaps = 0;  // GuestSwapCount
  uint64_t gpu_swaps = 0;    // FramesShownCount
  std::set<uint64_t> published;
  uint64_t latest = 0;
  std::optional<uint64_t> last_shown;
  std::deque<uint64_t> packets;  // swap packets the GPU has not processed yet
  std::vector<std::pair<uint64_t, uint64_t>> shown;  // (swap number, frame shown)

  void Present() {
    published.insert(guest_swaps);
    latest = guest_swaps;
  }
  void VdSwap(bool front_buffer_valid) {
    if (!front_buffer_valid) {
      return;  // no packet, no count
    }
    ++guest_swaps;
    packets.push_back(guest_swaps);
  }
  // The GPU processes a swap packet once it is `lag` packets behind.
  void Gpu(size_t packets_allowed_to_wait) {
    while (packets.size() > packets_allowed_to_wait) {
      packets.pop_front();
      const SwapChoice c = ChooseSwapFrame(
          last_shown, gpu_swaps, latest, 1, [&](uint64_t s) { return published.count(s) != 0; });
      shown.emplace_back(gpu_swaps, c.show);
      last_shown = c.show;
      ++gpu_swaps;
    }
  }
};
}  // namespace

TEST_CASE("the numbering recovers after a swap that issues no packet") {
  Session s;
  for (int frame = 0; frame < 30; ++frame) {
    s.Present();
    s.VdSwap(frame != 10);  // the 11th swap has an invalid front buffer
    s.Gpu(1);               // the command processor runs a frame behind
  }
  s.Gpu(0);
  REQUIRE(s.guest_swaps == s.gpu_swaps);
  // Every swap after the invalid one still shows its own frame, as before it.
  for (const auto& [swap, frame] : s.shown) {
    CHECK(frame == swap);
  }
}
