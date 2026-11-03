#include "d3d/sink.h"

#include <atomic>

namespace topspin4::d3d {
namespace {

Sink g_sink{};
std::atomic<uint64_t> g_calls[size_t(Module::kCount)][size_t(Fn::kCount)];

}  // namespace

Sink& GetSink() { return g_sink; }

void Count(Module module, Fn fn) {
  g_calls[size_t(module)][size_t(fn)].fetch_add(1, std::memory_order_relaxed);
}

uint64_t Calls(Module module, Fn fn) {
  return g_calls[size_t(module)][size_t(fn)].load(std::memory_order_relaxed);
}

}  // namespace topspin4::d3d
