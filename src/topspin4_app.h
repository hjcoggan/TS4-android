#pragma once

#include <fmt/format.h>
#include <rex/cvar.h>
#include <rex/graphics/graphics_system.h>
#include <rex/logging.h>
#include <rex/memory.h>
#include <rex/platform/env.h>
#include <rex/rex_app.h>
#include <rex/runtime.h>
#include <rex/system/guest_swaps.h>
#include <rex/system/host_gpu_loss.h>
#include <rex/ui/vulkan/provider.h>
#include <rex/ui/window.h>

#include "crash_report.h"
#include "d3d/host.h"
#include "fault_log.h"
#include "frame_dumps.h"
#include "native/native_renderer.h"
#include "online_unavailable.h"
#include "remapped_input.h"
#include "script_input.h"
#include "settings/config_file.h"
#include "settings/renderer_choice.h"
#include "settings/settings_file.h"
#include "user_paths.h"

REXCVAR_DECLARE(std::string, user_data_root);
REXCVAR_DECLARE(std::string, cache_root);
REXCVAR_DECLARE(std::string, ts4_renderer);

class TopSpin4App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(rex::ui::WindowedAppContext& ctx) {
    // First: in the crash report's dump helper (a second copy of the game), this writes the
    // dump and exits.
    topspin4::crash::Install();
    topspin4::fault_log::Install();
    return std::unique_ptr<TopSpin4App>(new TopSpin4App(ctx, "topspin4", PPCImageConfig));
  }

 protected:
  bool SetupEnvironment() override {
    // The Classic renderer uses host render targets (fbo), the Oracle's "performance" path:
    // FSI is correct too but GPU-bound, about 40 fps in rallies on an RX 7900 XT (ticket 13).
    // Host render targets need draw extents found by running unclipped draws' vertex shaders
    // on the CPU, as Edge does. Estimated at full height instead, Swing's depth-only
    // occlusion-query pass (EDRAM base 720, 4x MSAA) wraps around the EDRAM and takes the
    // front buffer's tiles from the colour target, so every Swing frame resolves black.
    // These cvars belong to the GPU plugin, which registers them after this runs, so they are
    // set the way a late registration reads them: from REX_<NAME>, which the same option on
    // the command line still overrides.
    if (!rex::platform::env::get("REX_RENDER_TARGET_PATH_VULKAN")) {
      rex::platform::env::set("REX_RENDER_TARGET_PATH_VULKAN", "fbo");
    }
    if (!rex::platform::env::get("REX_EXECUTE_UNCLIPPED_DRAW_VS_ON_CPU")) {
      rex::platform::env::set("REX_EXECUTE_UNCLIPPED_DRAW_VS_ON_CPU", "true");
    }
    return rex::ReXApp::SetupEnvironment();
  }

  // Saves and settings go to the per-user folder and the shader cache beside the launcher's
  // prepared data (src/user_paths.h), instead of the SDK's Documents/topspin4. The developer
  // scripts pass both folders on the command line, which wins.
  void OnConfigurePaths(rex::PathConfig& paths) override {
    if (REXCVAR_GET(user_data_root).empty()) {
      paths.user_data_root = topspin4::user_paths::DefaultUserDataRoot();
    }
    if (REXCVAR_GET(cache_root).empty()) {
      paths.cache_root = topspin4::user_paths::DefaultCacheRoot();
    }
  }

  // The player's settings (ticket 10), once logging can report on them and before the runtime
  // and the GPU plugin read them. A setting also given on the command line or as a REX_<NAME>
  // environment variable keeps that value.
  void OnPostInitLogging() override {
    // Top Spin 4 presents its cutaways at 30 fps. Repeating the held frame on each guest vblank
    // between them made the 360 Hz variable-refresh panel show 10 ms / 28 ms pairs through
    // every cutaway; skipping that present showed an even 33 ms, with play unchanged (PresentMon,
    // ticket 16). So off here, unless the command line, environment or config set it.
    SetUnlessSetElsewhere("present_repeat_held_vblanks", "false");
    // There is no Xbox LIVE to sign in to. Where the game still opens the sign-in UI (its LIVE
    // prompt in another language, src/online_unavailable.h), the SDK's stub broadcast a sign-in
    // change that never happened, too early for the game's listener, and the menu waited
    // forever. The picker closing with no change returns the game to its menu (ticket 15).
    SetUnlessSetElsewhere("signin_ui_closes_unchanged", "true");
    namespace settings = topspin4::settings;
    const auto path = settings::SettingsPath(user_data_root());
    topspin4::crash::SetSettingsFile(path);
    const settings::LoadReport report =
        settings::LoadSettingsFile(path, [](std::string_view key, std::string_view value) {
          const rex::cvar::FlagEntry* flag = rex::cvar::GetFlagInfo(key);
          if (flag && (flag->source == rex::cvar::Source::kCommandLine ||
                       flag->source == rex::cvar::Source::kEnvironment)) {
            return true;
          }
          return rex::cvar::SetFlagByName(key, value);
        });
    for (const std::string& warning : report.warnings) {
      REXLOG_WARN("{}", warning);
    }
    // The registry's defaults are the original behaviour (tests/host/test_settings.cpp); the
    // cvars state theirs again (settings_cvars.cpp, and the SDK's for keyboard and mouse).
    for (const settings::Setting& setting : settings::PlayerSettings()) {
      const rex::cvar::FlagEntry* flag = rex::cvar::GetFlagInfo(setting.key);
      if (!flag) {
        REXLOG_ERROR("Settings: {} has no cvar", setting.key);
      } else if (flag->default_value != setting.default_value) {
        REXLOG_ERROR("Settings: {} defaults to {} as a cvar but to {} in the registry",
                     setting.key, flag->default_value, setting.default_value);
      }
    }
    if (report.file == settings::LoadReport::File::kLoaded) {
      REXLOG_INFO("Settings: {}", settings::Utf8(path));
    }
    const settings::RendererChoice renderer =
        settings::ChooseRenderer(REXCVAR_GET(ts4_renderer), true);
    if (!renderer.note.empty()) {
      REXLOG_WARN("{}", renderer.note);
    }
    topspin4::native::Request(renderer.renderer == settings::Renderer::kNative);
  }

  // A cvar's value for Top Spin 4, unless the command line, environment or config set it.
  static void SetUnlessSetElsewhere(std::string_view name, std::string_view value) {
    if (const rex::cvar::FlagEntry* flag = rex::cvar::GetFlagInfo(name);
        flag && flag->source == rex::cvar::Source::kDefault) {
      rex::cvar::SetFlagByName(name, value);
    }
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
    topspin4::native::ApplyPresenterSettings();
    rex::kernel::xam::SetMessageBoxRewriter(&topspin4::online::RewriteLivePrompt);
    // The player's controller bindings (ticket 24); a scripted run then replaces the whole
    // input system, so no binding can steer it.
    topspin4::InstallRemappedInput(config);
    topspin4::InstallScriptInput(config, &rex::system::GuestSwapCount);
  }

  void OnPostSetup() override {
    // A device loss the SDK sees is a GPU failure under Native.
    rex::system::host_gpu_loss::SetHandler(&topspin4::native::OnHostGpuLoss);
    topspin4::native::Configure(runtime());
    topspin4::d3d::InstallHost(runtime()->cache_root());
    topspin4::frame_dumps::SetSessionEnd([]() -> std::string {
      topspin4::d3d::OnSessionEnd();
      return "\"renderer\": " + topspin4::native::ReportJson();
    });
    topspin4::frame_dumps::Install(runtime());
    // The crash report's facts known only now. Only the Vulkan backend is built, so the
    // provider is a VulkanProvider.
    if (auto* graphics = runtime()->graphics_system()) {
      auto* provider = static_cast<rex::ui::vulkan::VulkanProvider*>(graphics->provider());
      if (provider && provider->vulkan_device()) {
        const auto& properties = provider->vulkan_device()->properties();
        topspin4::crash::SetGpu(properties.deviceName,
                                fmt::format("0x{:08X}", properties.driverVersion));
      }
    }
    topspin4::crash::SetGuestMemory(runtime()->memory()->virtual_membase(),
                                    runtime()->memory()->physical_membase());
    if (auto* game_window = window()) {
      topspin4::crash::SetWindow(game_window->GetNativeWindowHandle());
    }
    topspin4::crash::CrashIfAsked();
  }

  // Closing the window hard-exits the process without OnShutdown, so the session's last
  // lines are written here; OnShutdown covers the guest exiting.
  bool OnWindowCloseRequested() override {
    topspin4::d3d::OnSessionEnd();
    return true;
  }

  void OnShutdown() override {
    topspin4::d3d::OnSessionEnd();
    topspin4::native::Shutdown();
  }

  // Native fits its output to the window; the render thread reads the size handed over here.
  void OnWindowPixelSizeChanged(uint32_t pixel_width, uint32_t pixel_height) override {
    rex::ReXApp::OnWindowPixelSizeChanged(pixel_width, pixel_height);
    topspin4::native::SetWindowSize(pixel_width, pixel_height);
  }
};
