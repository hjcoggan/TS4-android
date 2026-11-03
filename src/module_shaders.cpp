#include "module_shaders.h"

#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <string>

#include <fmt/format.h>
#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/runtime.h>
#include <rex/system/kernel_state.h>
#include <rex/system/user_module.h>

#include "native/shader_topup.h"

REXCVAR_DEFINE_BOOL(ts4_prepare_modules, false, "TopSpin4",
                    "Write the shader containers in the Loader and Swing Modules' data to the shader "
                    "top-up folder, print the result and exit (the Launcher's shader step; "
                    "src/module_shaders.h)");

namespace topspin4::module_shaders {

namespace {

// The two Modules the title loads after Boot, by the paths Boot and Loader load them with.
constexpr const char* kModules[] = {"game:\\Loader_DLL.xex", "game:\\Swing_DLL.xex"};

void Print(const std::string& line) {
  std::fwrite(line.data(), 1, line.size(), stdout);
  std::fflush(stdout);
}

[[noreturn]] void Finish(const std::string& result, int code) {
  REXLOG_INFO("module shaders: {}", result);
  Print("result " + result + "\n");
  rex::FlushLogging();
  std::_Exit(code);
}

[[noreturn]] void Fail(const char* reason, long long ms) {
  Finish(fmt::format("status=failed reason={} ms={}", reason, ms), 2);
}

}  // namespace

bool Requested() { return REXCVAR_GET(ts4_prepare_modules); }

void WriteAndExit(rex::Runtime& runtime) {
  const auto start = std::chrono::steady_clock::now();
  const auto elapsed_ms = [&start] {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
               std::chrono::steady_clock::now() - start)
        .count();
  };
  Print("topspin4-prepare-modules 1\n");
  if (runtime.cache_root().empty()) {
    Fail("no_cache_root", elapsed_ms());
  }
  native::ShaderTopUp topup(runtime.cache_root());
  const std::filesystem::path folder =
      std::filesystem::path(runtime.cache_root()) / native::ShaderTopUp::kFolderName;
  const uint8_t* membase = runtime.memory()->virtual_membase();
  size_t containers = 0;
  for (const char* path : kModules) {
    // Loaded the way Boot's XexLoadImage loads them: the Title Update's patch is applied, the recompiled
    // code is wired, and DllMain is not run (call_entry false), so no guest code executes.
    auto module = runtime.kernel_state()->LoadUserModule(path, /*call_entry=*/false);
    if (!module) {
      REXLOG_ERROR("module shaders: could not load {}", path);
      Fail("module_load", elapsed_ms());
    }
    size_t in_module = 0;
    for (const PESection& section : module->xex_module()->pe_sections()) {
      // Only read-only data is searched: the containers the game passes to Create*Shader sit in .rdata.
      if (section.flags &
          (kXEPESectionContainsCode | kXEPESectionMemoryExecute | kXEPESectionMemoryWrite)) {
        continue;
      }
      const std::span<const uint8_t> memory(membase + section.address, section.size);
      for (const Found& found : Scan(memory)) {
        const uint8_t* container = memory.data() + found.offset;
        topup.Offer(container, found.size, found.pixel, /*pack_holds=*/false);
        // Offer is false both for a file already there and for a failed write; only the file tells.
        const std::string name = native::ShaderTopUp::FileName(container, found.size, found.pixel);
        std::error_code error;
        if (!std::filesystem::exists(folder / name, error)) {
          REXLOG_ERROR("module shaders: could not write {}", name);
          Fail("write", elapsed_ms());
        }
        ++in_module;
      }
    }
    REXLOG_INFO("module shaders: {} holds {} containers", path, in_module);
    containers += in_module;
  }
  Finish(fmt::format("status=ok containers={} ms={}", containers, elapsed_ms()), 0);
}

}  // namespace topspin4::module_shaders
