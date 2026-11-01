#pragma once

#include <fmt/format.h>
#include <rex/cvar.h>
#include <rex/graphics/graphics_system.h>
#include <rex/input/input_system.h>
#include <rex/logging.h>
#include <rex/memory.h>
#include <rex/platform/env.h>
#include <rex/rex_app.h>
#include <rex/runtime.h>
#include <rex/system/guest_swaps.h>
#include <rex/system/host_gpu_loss.h>
#include <rex/ui/keybinds.h>
#include <rex/ui/vulkan/provider.h>
#include <rex/ui/window.h>

#include "achievement_toast.h"
#include "crash_report.h"
#include "d3d/host.h"
#include "developer_build.h"
#include "fault_log.h"
#include "frame_dumps.h"
#include "load_flash.h"
#include "module_shaders.h"
#include "native/native_renderer.h"
#include "online_unavailable.h"
#include "remapped_input.h"
#include "script_input.h"
#include "settings/config_file.h"
#include "settings/renderer_choice.h"
#include "settings/schema.h"
#include "settings/session.h"
#include "settings/settings_page.h"
#include "not_set_up.h"
#include "user_paths.h"

REXCVAR_DECLARE(std::string, user_data_root);
REXCVAR_DECLARE(std::string, cache_root);
REXCVAR_DECLARE(std::string, ts4_renderer);
REXCVAR_DECLARE(std::string, ts4_input_script);
REXCVAR_DECLARE(bool, safe);

class TopSpin4App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(rex::ui::WindowedAppContext& ctx) {
    // First: in the crash report's dump helper (a second copy of the game), this writes the
    // dump and exits.
    topspin4::crash::Install();
    // The launcher's headless export of the settings list: prints and exits before any window,
    // the runtime or game data.
    topspin4::settings::PrintSchemaAndExitIfAsked();
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

  // A portable install (ADR-0009): saves and settings go to userdata beside the exe, and the shader
  // cache beside the launcher's prepared data, wherever the player moved it (src/user_paths.h),
  // instead of the SDK's Documents/topspin4. The developer scripts pass both folders on the
  // command line, which wins.
  void OnConfigurePaths(rex::PathConfig& paths) override {
    // An exe folder that cannot be written to is refused, never replaced by a per-user folder: the
    // Launcher opens and says so. Not asked when the command line gave the user data folder.
    if (REXCVAR_GET(user_data_root).empty()) {
      if (!topspin4::user_paths::IsWritableFolder(topspin4::user_paths::ExecutableFolder())) {
        topspin4::ReportNotSetUp(topspin4::user_paths::kNotSetUpUnwritable, std::filesystem::path());
      }
      paths.user_data_root = topspin4::user_paths::DefaultUserDataRoot();
    }
    // Started directly after the Launcher's setup: the prepared disc and TU1 are where the
    // Launcher left them (the place its location file records after a move, else the default
    // folder), unless the command line (or config) names other folders.
    namespace user_paths = topspin4::user_paths;
    const auto recorded =
        user_paths::ReadLocationFile(paths.user_data_root / std::string(user_paths::kLocationFileName));
    // The shader cache stays beside the prepared data, so it follows the recorded place too.
    if (REXCVAR_GET(cache_root).empty()) {
      paths.cache_root = user_paths::CacheRoot(recorded, user_paths::DefaultCacheRoot());
    }
    const auto use_prepared = [&](std::filesystem::path& root, const auto& folder) {
      root = user_paths::PreparedFolder(root, recorded, user_paths::DefaultPreparedDataRoot(), folder);
    };
    // Not set up (no folders given, the prepared disc or TU1 not found): the Launcher opens on
    // its setup page with the reason, and the game ends here (src/not_set_up.h).
    if (const auto reason = user_paths::SetupNeededReason(
            paths.game_data_root, paths.update_data_root, recorded, user_paths::DefaultPreparedDataRoot())) {
      topspin4::ReportNotSetUp(*reason, REXCVAR_GET(user_data_root).empty()
                                            ? std::filesystem::path()
                                            : paths.user_data_root);
    }
    use_prepared(paths.game_data_root, user_paths::kGameDataFolder);
    use_prepared(paths.update_data_root, user_paths::kUpdateDataFolder);
  }

  // The player's settings (ticket 10), once logging can report on them and before the runtime
  // and the GPU plugin read them. A setting also given on the command line or as a REX_<NAME>
  // environment variable keeps that value.
  void OnPostInitLogging() override {
    // The SDK's default is no GPU plugin, which draws nothing. The developer scripts pass
    // --gpu_plugin=xenos; a player starting the exe or the Launcher passes nothing.
    SetUnlessSetElsewhere("gpu_plugin", "xenos");
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
    // The Settings file into the cvars (Safe start, ticket 09, skips it), and the Settings page
    // told what the file holds and which settings Launch options pin.
    const settings::LoadReport report = settings::StartSession(path, REXCVAR_GET(safe));
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
      REXLOG_INFO("Settings: {} (version {})", settings::Utf8(path), report.file_version);
      if (report.rewritten) {
        REXLOG_INFO("Settings: brought up to version {}", settings::CurrentVersion());
      }
    }
    const settings::RendererChoice renderer =
        settings::ChooseRenderer(REXCVAR_GET(ts4_renderer), true);
    if (!renderer.note.empty()) {
      REXLOG_WARN("{}", renderer.note);
    }
    topspin4::native::Request(renderer.renderer == settings::Renderer::kNative);
    // The session report repeats both at the end.
    REXLOG_INFO("Settings: {}Graphics preset {}", REXCVAR_GET(safe) ? "Safe start, " : "",
                settings::ActivePreset(
                    CvarValues(), renderer.renderer == settings::Renderer::kNative
                                      ? settings::kRendererNative
                                      : settings::kRendererClassic));
  }

  // Every player setting's value in its cvar now.
  static topspin4::settings::ValueMap CvarValues() {
    topspin4::settings::ValueMap values;
    for (const topspin4::settings::Setting& setting : topspin4::settings::PlayerSettings()) {
      values[std::string(setting.key)] = rex::cvar::GetFlagByName(setting.key);
    }
    return values;
  }

  // A cvar's value for Top Spin 4, unless the command line, environment or config set it.
  static void SetUnlessSetElsewhere(std::string_view name, std::string_view value) {
    if (const rex::cvar::FlagEntry* flag = rex::cvar::GetFlagInfo(name);
        flag && flag->source == rex::cvar::Source::kDefault) {
      rex::cvar::SetFlagByName(name, value);
    }
  }

  // The SDK has just bound its overlays (F3 debug, backtick console, F6 cvar settings, F7
  // achievements). A release build drops F6: players have the settings page instead.
  void OnCreateDialogs(rex::ui::ImGuiDrawer* drawer) override {
    rex::ReXApp::OnCreateDialogs(drawer);
    if (!topspin4::kDeveloperBuild) {
      rex::ui::UnregisterBind("bind_settings");
    }
  }

  // The Settings page's font: the game's own, from the player's game data.
  void OnConfigureFonts(ImFontAtlas* atlas) override {
    topspin4::settings::AddFonts(atlas, game_data_root());
    topspin4::achievement_toast::AddFonts(atlas);
  }

  // The "Achievement unlocked" toast in the console's manner, instead of the SDK's corner one.
  std::unique_ptr<rex::ui::AchievementNotificationDialog> CreateAchievementNotificationDialog() override {
    if (!imgui_drawer()) return nullptr;
    return std::make_unique<topspin4::achievement_toast::Toast>(
        imgui_drawer(), topspin4::settings::BodyFont(),
        topspin4::achievement_toast::ChooseSound(user_data_root(),
                                                 topspin4::user_paths::ExecutableFolder()));
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
    // The Launcher's pipeline step (--ts4_prepare_pipelines) needs Native and a hidden window.
    if (topspin4::native::PrepareRequested()) {
      topspin4::native::Request(true);
      rex::cvar::SetFlagByName("fullscreen", "false");
      rex::cvar::SetFlagByName("window_hidden", "true");
    }
    topspin4::native::ApplyImageSettings();
    rex::kernel::xam::SetMessageBoxRewriter(&topspin4::online::RewriteLivePrompt);
    // The player's controller bindings (ticket 24); a scripted run then replaces the whole
    // input system, so no binding can steer it.
    topspin4::InstallRemappedInput(config);
    // The Settings page reads the pad with that remap undone, so no binding can lock the player
    // out of the page. A scripted run has no remap (its input system replaces this one).
    if (REXCVAR_GET(ts4_input_script).empty()) {
      topspin4::settings::SetGamePadBindings(topspin4::BindingsFromCvars());
    }
    // A script's `set` changes a setting on the UI thread, where the settings page and the F6
    // overlay change theirs (the presenter's CAS must be switched there). A player setting goes
    // through the Settings page's own change path; any other cvar is set directly.
    rex::ui::WindowedAppContext* context = &app_context();
    topspin4::InstallScriptInput(
        config, &rex::system::GuestSwapCount, [this, context](std::string name, std::string value) {
          context->CallInUIThread([this, name, value] {
            // `set settings_page open|close|toggle` drives the Settings page; its pad input is
            // the script's own pad.
            if (topspin4::settings::RunScriptCommand(name, value)) {
              REXLOG_INFO("script input: {} {}", name, value);
            } else if (name == "achievement_toast") {
              // `set achievement_toast <id>` shows that achievement's toast without unlocking it.
              const bool shown = runtime() && runtime()->kernel_state() &&
                                 achievements().ShowAchievementNotification(
                                     uint32_t(std::strtoul(value.c_str(), nullptr, 10)));
              REXLOG_INFO("script input: achievement_toast {} {}", value, shown ? "shown" : "unknown");
            } else if (topspin4::settings::SetPlayerSettingFromScript(name, value)) {
              // A player setting goes through the Settings page's change path, which logs it.
            } else if (rex::cvar::SetFlagByName(name, value)) {
              REXLOG_INFO("script input: set {} = {}", name, value);
            } else {
              REXLOG_WARN("script input: set {} = {} was rejected", name, value);
            }
          });
        });
  }

  // The Launcher's Module shader step (--ts4_prepare_modules): the runtime is up and Boot is loaded, but
  // nothing has launched, so the Modules can be read without any guest code running. Writes and exits.
  void OnPreLaunchModule() override {
    if (topspin4::module_shaders::Requested()) {
      topspin4::module_shaders::WriteAndExit(*runtime());
    }
  }

  void OnPostSetup() override {
    // A device loss the SDK sees is a GPU failure under Native.
    rex::system::host_gpu_loss::SetHandler(&topspin4::native::OnHostGpuLoss);
    topspin4::native::Configure(runtime());
    // The Launcher's pipeline step: build the pack's modules and the planned pipelines, report
    // and exit before any Module launches.
    if (topspin4::native::PrepareRequested()) {
      topspin4::native::PreparePipelinesAndExit();
    }
    topspin4::d3d::InstallHost(runtime()->cache_root());
    topspin4::load_flash::InstallHost();
    topspin4::frame_dumps::SetSessionEnd([]() -> std::string {
      topspin4::d3d::OnSessionEnd();
      return "\"renderer\": " + topspin4::native::ReportJson() + ",\n " +
             topspin4::settings::SessionReportJson();
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
    // The Settings page (F4, or a script's `set settings_page open`), over the game. Needs the
    // ImGui drawer and the input system, both up by now.
    topspin4::settings::Configure(
        imgui_drawer(), static_cast<rex::input::InputSystem*>(runtime()->input_system()),
        topspin4::settings::SettingsPath(user_data_root()),
        [this](std::function<void()> function) {
          app_context().CallInUIThread(std::move(function));
        });
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
