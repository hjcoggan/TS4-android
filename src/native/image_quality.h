// The image-quality plan (.scratch/image-quality/spec.md): every image-quality choice Native
// applies, made in one place from the image settings, the window and the device. The renderer
// reads its outputs (the scale its targets draw at, the samples a target gets, the global
// texture sharpness bias, the anisotropic filtering, CAS and the texture-offset scale) and
// decides none of them itself.

#pragma once

#include <algorithm>
#include <array>
#include <bit>
#include <charconv>
#include <cmath>
#include <cstdint>
#include <iterator>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <vector>

#include "native/guest_decode.h"

namespace topspin4::native::image_quality {

enum class Sharpening : uint8_t { kOff, kLow, kMedium, kHigh };

// Resolution scale Auto: the smallest multiple of 1280x720 that covers the window's 16:9 area.
inline constexpr uint32_t kAutoScale = 0;

// The largest Resolution scale: Auto stops here and any request is clamped to it. At 4x with 8x
// MSAA heavy frames cost about 15 ms of GPU time and stutter on a variable-refresh display, while
// 3x with 8x does not (docs/research/native-draw-cost.md).
inline constexpr uint32_t kMaxResolutionScale = 3;

// The image settings.
struct Settings {
  uint32_t resolution_scale = 1;  // 1..kMaxResolutionScale, or kAutoScale
  uint32_t msaa = 0;  // 0 = Original (the game's own 2x on its match pass); else 4 or 8
  // 0 = Original (the game's own filtering); else the level forced on linear, mipmapped fetches.
  uint32_t anisotropic_filtering = 0;
  Sharpening sharpening = Sharpening::kOff;
};

// The player settings' values (settings/registry.h), as their cvars hold them.
struct SettingValues {
  std::string_view resolution_scale = "1";              // 1, auto, 2, 3
  std::string_view msaa = "original";                   // original, 4, 8
  std::string_view anisotropic_filtering = "original";  // original, 4, 8, 16
  std::string_view sharpening = "off";                  // off, low, medium, high
};

// The values each setting allows, as settings/registry.h lists them (a test ties the two).
inline constexpr std::string_view kResolutionScaleChoices[] = {"1", "auto", "2", "3"};
inline constexpr std::string_view kMsaaChoices[] = {"original", "4", "8"};
inline constexpr std::string_view kAnisotropicFilteringChoices[] = {"original", "4", "8", "16"};
inline constexpr std::string_view kSharpeningChoices[] = {"off", "low", "medium", "high"};

// Each image setting in one place: the cvar and settings key, the values it allows and whether
// only Native applies it. The order is SettingValues'. A test ties the table to
// settings/registry.h, so a new setting is added there, in settings_cvars.cpp, in SettingValues
// and Settings, here and in the renderer's kEnhancements.
struct SettingInfo {
  std::string_view name;
  std::span<const std::string_view> choices;
  bool native_only;
};
inline constexpr SettingInfo kSettings[] = {
    {"ts4_resolution_scale", kResolutionScaleChoices, true},
    {"ts4_msaa", kMsaaChoices, true},
    {"ts4_anisotropic_filtering", kAnisotropicFilteringChoices, false},
    {"ts4_sharpening", kSharpeningChoices, false},
};

// "Nx" for a level above 0 (a scale, a sample count or a filtering level); else none ("original",
// "auto").
inline std::string LevelText(uint32_t level, std::string_view none) {
  return level ? std::to_string(level) + "x" : std::string(none);
}

// "N" for a level of N (1..16), by bit_width(N) - 1.
inline constexpr std::string_view kLevelNames[] = {"1", "2", "4", "8", "16"};

// A value a setting does not allow (a typo on the command line) gives its default.
constexpr Settings ParseSettings(const SettingValues& values) {
  Settings settings;
  if (values.resolution_scale == "auto") settings.resolution_scale = kAutoScale;
  for (uint32_t scale = 2; scale <= kMaxResolutionScale; ++scale) {
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
  const std::string_view given[] = {values.resolution_scale, values.msaa,
                                    values.anisotropic_filtering, values.sharpening};
  static_assert(std::size(given) == std::size(kSettings));
  std::vector<std::string_view> ignored;
  for (size_t i = 0; i < std::size(kSettings); ++i) {
    const std::span<const std::string_view> choices = kSettings[i].choices;
    if (std::find(choices.begin(), choices.end(), given[i]) == choices.end()) {
      ignored.push_back(kSettings[i].name);
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
  float max_lod_bias = 2.0f;  // the device's limit on a sampler's total bias
  // In anisotropic_override's form, as both renderers take it: -1 = the game's own filtering,
  // 0..5 = 1x..16x (the setting writes only 3..5; a developer's override may give any). The device's maximum clamps it where a sampler is chosen (ChooseSampler).
  int32_t texture_filtering = -1;
  guest::CasSetting cas;
  uint32_t msaa = 0;  // as Settings::msaa
  uint32_t sample_counts = 1 | 2 | 4 | 8;

  // The sample count asked for a target the game creates with guest_samples (1, 2 or 4): the
  // MSAA setting's for a target the game multisamples, never more for one it does not.
  constexpr uint32_t AskedSamples(uint32_t guest_samples) const {
    return guest_samples > 1 && msaa > guest_samples ? msaa : guest_samples;
  }

  // The sample count the target gets: AskedSamples, then the highest count at or below it the
  // device supports.
  constexpr uint32_t SamplesFor(uint32_t guest_samples) const {
    uint32_t samples = AskedSamples(guest_samples);
    while (samples > 1 && !(sample_counts & samples)) samples >>= 1;
    return std::max(samples, 1u);
  }
};

// The anisotropic_override flag's value, from its text; none when it is not a number (or the
// flag does not exist), which leaves the setting's.
inline std::optional<int32_t> ParseFilteringOverride(std::string_view text) {
  int32_t value = 0;
  if (text.empty()) return std::nullopt;
  const char* end = text.data() + text.size();
  const auto result = std::from_chars(text.data(), end, value);
  if (result.ec != std::errc() || result.ptr != end) return std::nullopt;
  return value;
}

// The filtering both renderers use: the anisotropic_override flag's value when it is a number
// (the setting writes its own level there; a --anisotropic_override given directly, or set while
// playing, is a developer's choice that wins), else the plan's (Plan::texture_filtering). -1 is
// the game's own.
constexpr int32_t EffectiveFiltering(int32_t plan_filtering,
                                     std::optional<int32_t> override_value) {
  return override_value ? *override_value : plan_filtering;
}

// Whether the session forces texture filtering on either renderer (the report's Enhancement).
constexpr bool FilteringForced(int32_t plan_filtering, std::optional<int32_t> override_value) {
  return EffectiveFiltering(plan_filtering, override_value) >= 0;
}

// Whether a --present_effect given directly sharpens the frame (CAS), as ts4_sharpening does.
inline bool PresentEffectSharpens(std::string_view present_effect) {
  return present_effect == "cas";
}

// Whether ts4_sharpening reaches the presenter: only while --present_effect is left at its
// bilinear default; any other effect is a developer's, and the setting does nothing.
inline bool SharpeningApplies(std::string_view present_effect) {
  return present_effect == "bilinear";
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

// A render target the game creates, by its guest size and kind.
struct TargetShape {
  uint32_t guest_width = 0;
  uint32_t guest_height = 0;
  bool is_depth = false;
  constexpr bool operator==(const TargetShape&) const = default;
};

// The targets shown to break when scaled, which stay at 1x whatever the Resolution scale (the
// spec's per-target pin, as Edge does). Empty: ticket 05 found no target that breaks. Add a
// target here with the frame evidence for why.
inline constexpr std::array<TargetShape, 0> kPinnedTargets = {};

// The scale a render target draws at: the plan's, or 1 for a pinned target.
constexpr uint32_t TargetScale(uint32_t plan_scale, const TargetShape& target,
                               std::span<const TargetShape> pinned = kPinnedTargets) {
  for (const TargetShape& pin : pinned) {
    if (pin == target) return 1;
  }
  return plan_scale;
}

// window_width x window_height: the window's pixel size, 0 before the window has one.
inline Plan MakePlan(const Settings& settings, uint32_t window_width, uint32_t window_height,
                        const DeviceLimits& device) {
  Plan plan;
  uint32_t requested = settings.resolution_scale;
  const uint32_t shown_height = guest::FitWindow(window_width, window_height).height;
  if (requested == kAutoScale) {
    // The 16:9 area is shown_height tall and at most 16/9 of it wide, so covering its height
    // covers its width; bars beside or above it don't count.
    requested = 1;
    while (requested < kMaxResolutionScale && 720 * requested < shown_height) ++requested;
  }
  requested = std::clamp(requested, 1u, kMaxResolutionScale);
  const uint32_t fits = std::max(device.max_image_size / kLargestTargetSide, 1u);
  plan.scale = std::min(requested, fits);
  plan.clamped = plan.scale < requested;
  plan.output = guest::PlanOutput(plan.scale, window_width, window_height);
  // Above 1x, when the presenter still scales the frame up, textures are sharpened by as much
  // (the vendors' upscaler rule without its temporal term); a frame drawn at or above the window
  // is supersampled, which sharpens them already. At 1x the bias is 0 whatever the window, so the
  // default look stays the original's (ADR-0001; owner decision 2026-10-28).
  const uint32_t render_height = 720 * plan.scale;
  plan.max_lod_bias = device.max_lod_bias;
  if (plan.scale > 1 && shown_height > render_height) {
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
