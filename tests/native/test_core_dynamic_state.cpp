// native/core_dynamic_state.h: viewport, scissor, blend constants and stencil reference and masks
// are set on the command buffer only when they change. Setting them before every draw cost the
// crowd shots of a match ~3 us of GPU time per draw on an RX 7900 XT, at any resolution
// (docs/research/native-draw-cost.md).

#include <catch_amalgamated.hpp>

#include "native/core_dynamic_state.h"

using namespace topspin4::native;

namespace {

CoreDynamicState Crowd() {
  CoreDynamicState s;
  s.viewport = {0, 2880, 5120, -2880, 0, 1};
  s.scissor = {0, 0, 5120, 2880};
  s.blend = {0, 0, 0, 0};
  s.stencil[0] = {0x80, 0xFF, 0xFF};
  s.stencil[1] = {0x80, 0xFF, 0xFF};
  return s;
}

}  // namespace

TEST_CASE("the first draw on a command buffer sets everything") {
  CoreDynamicStateCache cache;
  const CoreDynamicStateCache::Changes c = cache.Update(Crowd());
  CHECK(c.viewport);
  CHECK(c.scissor);
  CHECK(c.blend);
  for (int face = 0; face < 2; ++face) {
    CHECK(c.reference[face]);
    CHECK(c.compare_mask[face]);
    CHECK(c.write_mask[face]);
  }
}

TEST_CASE("a draw with the same state sets nothing") {
  CoreDynamicStateCache cache;
  cache.Update(Crowd());
  const CoreDynamicStateCache::Changes c = cache.Update(Crowd());
  CHECK_FALSE(c.Any());
}

TEST_CASE("only what changed is set") {
  CoreDynamicStateCache cache;
  cache.Update(Crowd());
  CoreDynamicState next = Crowd();
  next.scissor.width = 2560;
  next.stencil[1].reference = 0x81;
  const CoreDynamicStateCache::Changes c = cache.Update(next);
  CHECK_FALSE(c.viewport);
  CHECK(c.scissor);
  CHECK_FALSE(c.blend);
  CHECK_FALSE(c.reference[0]);
  CHECK(c.reference[1]);
  CHECK_FALSE(c.compare_mask[1]);
  CHECK_FALSE(c.write_mask[1]);
}

TEST_CASE("a new command buffer starts with nothing set") {
  CoreDynamicStateCache cache;
  cache.Update(Crowd());
  cache.Reset();
  CHECK(cache.Update(Crowd()).viewport);
}

TEST_CASE("a NaN blend constant is set again, never assumed equal") {
  CoreDynamicStateCache cache;
  CoreDynamicState s = Crowd();
  s.blend[2] = std::numeric_limits<float>::quiet_NaN();
  cache.Update(s);
  // Compared by bits: the same NaN is the same value.
  CHECK_FALSE(cache.Update(s).blend);
}
