#include "d3d/census.h"

#include <atomic>
#include <mutex>
#include <string>
#include <unordered_set>

#include <fmt/format.h>
#include <rex/cvar.h>
#include <rex/logging.h>

REXCVAR_DEFINE_BOOL(ts4_d3d_census, false, "TopSpin4",
                    "Log each Module's D3D hook call counts every 60 of its presents, and the "
                    "device's register-mirror groups");

namespace topspin4::d3d::census {
namespace {

std::atomic<uint64_t> g_presents[size_t(Module::kCount)];

void Log(Module module, const char* when) {
  std::string line;
  for (size_t i = 0; i < size_t(Fn::kCount); ++i) {
    if (const uint64_t n = Calls(module, Fn(i))) {
      line += fmt::format(" {}={}", FnName(Fn(i)), n);
    }
  }
  REXLOG_INFO("d3d-census {} {} presents={}{}", ModuleName(module), when,
              g_presents[size_t(module)].load(std::memory_order_relaxed),
              line.empty() ? " none" : line);
}

}  // namespace

bool Enabled() { return REXCVAR_GET(ts4_d3d_census); }

void OnPresent(Module module) {
  const uint64_t presents = g_presents[size_t(module)].fetch_add(1, std::memory_order_relaxed) + 1;
  if (presents % 60 == 0) {
    Log(module, "periodic");
  }
}

void OnMirrorCommit(Module module, uint32_t device, uint32_t r4, uint32_t first_register,
                    uint32_t mirror) {
  static std::mutex mutex;
  static std::unordered_set<uint64_t> seen;
  const uint64_t key = uint64_t(module) << 63 | uint64_t(first_register) << 32 | (mirror - device);
  std::lock_guard lock(mutex);
  if (seen.insert(key).second) {
    REXLOG_INFO("register-mirror {} first_reg={:04X} device_offset={:04X} r4={:08X}",
                ModuleName(module), first_register, mirror - device, r4);
  }
}

void LogFinal() {
  if (!Enabled()) {
    return;
  }
  Log(Module::kLoader, "final");
  Log(Module::kSwing, "final");
}

}  // namespace topspin4::d3d::census
