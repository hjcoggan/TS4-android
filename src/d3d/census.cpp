#include "d3d/census.h"

#include <algorithm>
#include <atomic>
#include <map>
#include <mutex>
#include <string>
#include <unordered_set>
#include <utility>
#include <vector>

#include <fmt/format.h>
#include <rex/cvar.h>
#include <rex/logging.h>

REXCVAR_DEFINE_BOOL(ts4_d3d_census, false, "TopSpin4",
                    "Log both Modules' D3D hook call counts every 60 presents of either, and the "
                    "device's register-mirror groups");

namespace topspin4::d3d::census {
namespace {

std::atomic<uint64_t> g_presents[size_t(Module::kCount)];

// Draw attribution (OnCallEntry). The device each Module presents with, and the cursor after
// the last hooked call returned.
constexpr uint32_t kRingCursor = 0x30;
std::atomic<uint32_t> g_devices[size_t(Module::kCount)];
std::mutex g_attribution_mutex;
uint32_t g_last_cursor[size_t(Module::kCount)];
std::map<std::pair<int, uint32_t>, uint64_t> g_unhooked_draws;  // (module, caller) -> draws
std::map<std::pair<int, int>, uint64_t> g_inside_draws;        // (module, hook) -> draws
thread_local std::vector<uint32_t> t_entry_cursors;

uint32_t LoadBE32(const uint8_t* p) {
  return uint32_t(p[0]) << 24 | uint32_t(p[1]) << 16 | uint32_t(p[2]) << 8 | p[3];
}

uint32_t Cursor(Module module, const uint8_t* base) {
  const uint32_t device = g_devices[size_t(module)].load(std::memory_order_relaxed);
  return device ? LoadBE32(base + device + kRingCursor) : 0;
}

// Type-3 DRAW_INDX (0x22) and DRAW_INDX_2 (0x36) headers in [from, to); 0 across a wrap, a
// segment switch or an implausible span. As WWE All Stars' d3d_sweep.cpp.
uint64_t CountDraws(const uint8_t* base, uint32_t from, uint32_t to) {
  if (!from || to <= from || to - from > 0x10000) return 0;
  uint64_t draws = 0;
  for (uint32_t at = from; at + 4 <= to;) {
    const uint32_t header = LoadBE32(base + at);
    if ((header >> 30) != 3) {
      at += 4;
      continue;
    }
    const uint32_t opcode = (header >> 8) & 0x7F;
    if (opcode == 0x22 || opcode == 0x36) ++draws;
    at += 4 * (((header >> 16) & 0x3FFF) + 2);
  }
  return draws;
}

void LogAttribution(Module module, uint64_t presents) {
  std::vector<std::pair<uint64_t, std::string>> rows;
  {
    std::lock_guard lock(g_attribution_mutex);
    for (const auto& [key, n] : g_unhooked_draws) {
      if (key.first == int(module)) rows.emplace_back(n, fmt::format("unhooked<{:08X}>", key.second));
    }
    for (const auto& [key, n] : g_inside_draws) {
      if (key.first == int(module)) rows.emplace_back(n, FnName(Fn(key.second)));
    }
  }
  std::sort(rows.rbegin(), rows.rend());
  std::string line;
  for (size_t i = 0; i < rows.size() && i < 24; ++i) {
    line += fmt::format(" {}={:.1f}", rows[i].second, double(rows[i].first) / double(presents));
  }
  REXLOG_INFO("d3d-census {} draws written per present:{}", ModuleName(module),
              line.empty() ? " none" : line);
}

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

void OnPresent(Module module, uint32_t device) {
  g_devices[size_t(module)].store(device, std::memory_order_relaxed);
  const uint64_t presents = g_presents[size_t(module)].fetch_add(1, std::memory_order_relaxed) + 1;
  if (presents % 60 == 0) {
    // Both Modules: one Module's D3D can draw on while only the other presents.
    Log(Module::kLoader, "periodic");
    Log(Module::kSwing, "periodic");
  }
  if (presents % 600 == 0) {
    LogAttribution(module, presents);
  }
}

void OnCallEntry(Module module, Fn, const uint8_t* base, uint32_t caller) {
  const uint32_t cursor = Cursor(module, base);
  t_entry_cursors.push_back(cursor);
  std::lock_guard lock(g_attribution_mutex);
  if (const uint64_t draws = CountDraws(base, g_last_cursor[size_t(module)], cursor)) {
    g_unhooked_draws[{int(module), caller}] += draws;
  }
  g_last_cursor[size_t(module)] = cursor;
}

void OnCallExit(Module module, Fn fn, const uint8_t* base) {
  const uint32_t cursor = Cursor(module, base);
  const uint32_t entry = t_entry_cursors.empty() ? 0 : t_entry_cursors.back();
  if (!t_entry_cursors.empty()) t_entry_cursors.pop_back();
  std::lock_guard lock(g_attribution_mutex);
  if (const uint64_t draws = CountDraws(base, entry, cursor)) {
    g_inside_draws[{int(module), int(fn)}] += draws;
  }
  g_last_cursor[size_t(module)] = cursor;
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
