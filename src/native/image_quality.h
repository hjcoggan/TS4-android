// The image-quality plan (.scratch/image-quality/spec.md): every image-quality choice Native
// applies, made in one place from the image settings, the window and the device. The renderer
// reads its outputs (the scale its targets draw at, the samples a target gets, the global
// texture sharpness bias, the anisotropic filtering, CAS and the texture-offset scale) and
// decides none of them itself.

#pragma once

#include <algorithm>
#include <bit>
#include <charconv>
#include <cmath>
#include <cstdint>
#include <span>
#include <string_view>
#include <vector>

#include "native/guest_decode.h"

namespace topspin4::native::image_quality {

enum class Sharpening : uint8_t { kOff, kLow, kMedium, kHigh };

// Resolution scale Auto: the smallest multiple of 1280x720 that covers the window.
inline constexpr uint32_t kAutoScale = 0;

// The image settings.
struct Settings {
  uint32_t resolution_scale = 1;  // 1..4, or kAutoScale
  uint32_t msaa = 0;  // 0 = Original (the game's own 2x on its match pass); else 4 or 8
  // 0 = Original (the game's own filtering); else the level forced on linear, mipmapped fetches.
  uint32_t anisotropic_filtering = 0;
  Sharpening sharpening = Sharpening::kOff;
};

// The player settings' values (settings/registry.h), as their cvars hold them.
struct SettingValues {
  std::string_view resolution_scale = "1";              // 1, auto, 2, 3, 4
  std::string_view msaa = "original";                   // original, 4, 8
  std::string_view anisotropic_filtering = "original";  // original, 4, 8, 16
  std::string_view sharpening = "off";                  // off, low, medium, high
};

// The values each setting allows, as settings/registry.h lists them (a test ties the two).
inline constexpr std::string_view kResolutionScaleChoices[] = {"1", "auto", "2", "3", "4"};
inline constexpr std::string_view kMsaaChoices[] = {"original", "4", "8"};
inline constexpr std::string_view kAnisotropicFilteringChoices[] = {"original", "4", "8", "16"};
inline constexpr std::string_view kSharpeningChoices[] = {"off", "low", "medium", "high"};

// "N" for a level of N (1..16), by bit_width(N) - 1.
inline constexpr std::string_view kLevelNames[] = {"1", "2", "4", "8", "16"};

// A value a setting does not allow (a typo on the command line) gives its default.
constexpr Settings ParseSettings(const SettingValues& values) {
  Settings settings;
  if (values.resolution_scale == "auto") settings.resolution_scale = kAutoScale;
  for (uint32_t scale : {2u, 3u, 4u}) {
    if (values.resolution_scale.size() == 1 && values.resolution_scale[0] == char('0' + scale)) {
      settings.resolution_scale = scale;
    }
  }
  for (uint32_t samples : {4u, 8u}) {
    if (values.msaa == kLevelNames[std::bit_width(samples) - 1]) settings.msaa = samples;
  }
  for (uint32_t level : {4u, 8u, 16u}) {
    if (values.anisotropic_filtering == kLevelNames[std::bit_width(level) - 1]) {
      settings.anisotropic_filtering = level;
    }
  }
  for (uint32_t i = 0; i < 4; ++i) {
    if (values.sharpening == kSharpeningChoices[i]) settings.sharpening = Sharpening(i);
  }
  return settings;
}

// The settings whose value is none the setting allows (a typo on the command line or in a
// `set`), by cvar name: ParseSettings gives them their default, and the log says so.
inline std::vector<std::string_view> IgnoredSettings(const SettingValues& values) {
  struct Allowed {
    std::string_view name;
    std::string_view value;
    std::span<const std::string_view> choices;
  };
  const Allowed settings[] = {
      {"ts4_resolution_scale", values.resolution_scale, kResolutionScaleChoices},
      {"ts4_msaa", values.msaa, kMsaaChoices},
      {"ts4_anisotropic_filtering", values.anisotropic_filtering, kAnisotropicFilteringChoices},
      {"ts4_sharpening", values.sharpening, kSharpeningChoices},
  };
  std::vector<std::string_view> ignored;
  for (const Allowed& setting : settings) {
    if (std::find(setting.choices.begin(), setting.choices.end(), setting.value) ==
        setting.choices.end()) {
      ignored.push_back(setting.name);
    }
  }
  return ignored;
}

// The CAS strength of each sharpening level, tuned by eye (image-quality ticket 03).
constexpr double CasStrength(Sharpening sharpening) {
  switch (sharpening) {
    case Sharpening::kLow: return 0.25;
    case Sharpening::kMedium: return 0.5;
    case Sharpening::kHigh: return 0.8;
    default: return 0.0;
  }
}

// What the device allows (VkPhysicalDeviceLimits and features).
struct DeviceLimits {
  uint32_t max_image_size = 16384;  // maxImageDimension2D and maxFramebufferWidth/Height
  float max_lod_bias = 2.0f;        // maxSamplerLodBias
  // VkSampleCountFlags a render target supports (TargetSampleCounts).
  uint32_t sample_counts = 1 | 2 | 4 | 8;
};

// The VkPhysicalDeviceLimits sample counts a render target's usage needs: every target is a
// colour or depth-stencil attachment and is also sampled (resolves read it), so a count must be
// in the framebuffer and the sampled-image limits of all three aspects. Vulkan guarantees only
// 1 and 4 in each.
struct TargetSampleLimits {
  uint32_t framebuffer_color = 0;
  uint32_t framebuffer_depth = 0;
  uint32_t framebuffer_stencil = 0;
  uint32_t sampled_color = 0;
  uint32_t sampled_depth = 0;
  uint32_t sampled_stencil = 0;
  // ts4_native_debug_max_samples: counts above it are treated as unsupported (0 = no cap).
  uint32_t debug_max_samples = 0;
};

// The VkSampleCountFlags a render target can have on the device (DeviceLimits::sample_counts).
constexpr uint32_t TargetSampleCounts(const TargetSampleLimits& l) {
  uint32_t counts = l.framebuffer_color & l.framebuffer_depth & l.framebuffer_stencil &
                    l.sampled_color & l.sampled_depth & l.sampled_stencil;
  if (l.debug_max_samples) {
    // Keep each count at or below the cap (counts are single bits, so bit <= cap).
    uint32_t below = 0;
    for (uint32_t bit = 1; bit && bit <= l.debug_max_samples; bit <<= 1) below |= bit;
    counts &= below;
  }
  return counts | 1;
}

struct Plan {
  uint32_t scale = 1;  // the Resolution scale every render target draws at
  bool clamped = false;  // the setting asked for more than the device's image size holds
  guest::OutputPlan output;
  // The global texture sharpness bias (negative is sharper), added to each fetch's own.
  float lod_bias = 0.0f;
  // In anisotropic_override's form, as both renderers take it: -1 = the game's own filtering,
  // 3..5 = 4x..16x. The device's maximum clamps it where a sampler is chosen (ChooseSampler).
  int32_t texture_filtering = -1;
  guest::CasSetting cas;
  uint32_t msaa = 0;  // as Settings::msaa
  uint32_t sample_counts = 1 | 2 | 4 | 8;

  // The sample count a target the game creates with guest_samples (1, 2 or 4) gets: the MSAA
  // setting's for a target the game multisamples, never more for one it does not, then the
  // highest count at or below that the device supports.
  constexpr uint32_t SamplesFor(uint32_t guest_samples) const {
    uint32_t samples = guest_samples > 1 && msaa > guest_samples ? msaa : guest_samples;
    while (samples > 1 && !(sample_counts & samples)) samples >>= 1;
    return std::max(samples, 1u);
  }
};

// Whether the session forces texture filtering on either renderer: the setting's plan_filtering
// (Plan::texture_filtering), or a --anisotropic_override given directly, which Classic takes
// over the setting. override_value is that flag's text; -1 is the game's own, and anything that
// is not a number forces nothing.
inline bool FilteringForced(int32_t plan_filtering, std::string_view override_value) {
  int32_t forced = -1;
  const char* end = override_value.data() + override_value.size();
  if (std::from_chars(override_value.data(), end, forced).ptr != end) forced = -1;
  return plan_filtering >= 0 || forced >= 0;
}

// Whether a --present_effect given directly sharpens the frame (CAS), as ts4_sharpening does.
inline bool PresentEffectSharpens(std::string_view present_effect) {
  return present_effect == "cas";
}

// What a fetch's texel offset is multiplied by before the shader divides it by the texture's
// host size: a resolved texture drawn at texture_scale is that much larger than the guest's,
// and its offsets (blur, bloom and shadow filter taps) are in guest texels, as Xenia Edge's
// draw_resolution_scaled_texture_offsets does. Ordinary textures are their guest size.
constexpr uint32_t TextureOffsetScale(uint32_t texture_scale, bool resolved) {
  return resolved ? std::clamp(texture_scale, 1u, 4u) : 1;
}

// The longest side of any render target the game creates (1280x720 and 1040x1024), which a
// scaled target must fit in the device's maximum image size.
inline constexpr uint32_t kLargestTargetSide = 1280;

// The height of the window's 16:9 area, where the presenter shows the frame; 0 without a window.
constexpr uint32_t FitHeight(uint32_t window_width, uint32_t window_height) {
  return uint64_t(window_width) * 9 > uint64_t(window_height) * 16
             ? window_height
             : uint32_t(uint64_t(window_width) * 9 / 16);
}

// window_width x window_height: the window's pixel size, 0 before the window has one.
inline Plan MakePlan(const Settings& settings, uint32_t window_width, uint32_t window_height,
                        const DeviceLimits& device) {
  Plan plan;
  uint32_t requested = settings.resolution_scale;
  if (requested == kAutoScale) {
    requested = 1;
    while (requested < 4 && (1280 * requested < window_width || 720 * requested < window_height)) {
      ++requested;
    }
  }
  requested = std::clamp(requested, 1u, 4u);
  const uint32_t fits = std::max(device.max_image_size / kLargestTargetSide, 1u);
  plan.scale = std::min(requested, fits);
  plan.clamped = plan.scale < requested;
  plan.output = guest::PlanOutput(plan.scale, window_width, window_height);
  // When the presenter scales the frame up, textures are sharpened by as much (the vendors'
  // upscaler rule without its temporal term); a frame drawn at or above the window is
  // supersampled, which sharpens them already.
  const uint32_t render_height = 720 * plan.scale;
  const uint32_t shown_height = FitHeight(window_width, window_height);
  if (shown_height > render_height) {
    plan.lod_bias = std::max(std::log2(float(render_height) / float(shown_height)),
                             -device.max_lod_bias);
  }
  if (settings.anisotropic_filtering) {
    plan.texture_filtering = std::bit_width(settings.anisotropic_filtering);  // 4x = 3
  }
  plan.cas = guest::CasEffect(CasStrength(settings.sharpening));
  plan.msaa = settings.msaa;
  plan.sample_counts = device.sample_counts;
  return plan;
}

}  // namespace topspin4::native::image_quality
