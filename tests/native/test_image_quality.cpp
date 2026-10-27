// The image-quality plan (src/native/image_quality.h): what Native draws at for given image
// settings, window and device.

#include <catch_amalgamated.hpp>

#include "native/image_quality.h"

using namespace topspin4::native::image_quality;

namespace {

constexpr DeviceLimits kDesktop = {};

}  // namespace

TEST_CASE("the default settings give the original plan") {
  const Plan plan = MakePlan({}, 1280, 720, kDesktop);
  CHECK(plan.scale == 1);
  CHECK_FALSE(plan.clamped);
  CHECK(plan.output.output_width == 1280);
  CHECK(plan.output.output_height == 720);
  CHECK_FALSE(plan.output.downsample);
  CHECK(plan.lod_bias == 0.0f);
  CHECK(plan.texture_filtering == -1);
  CHECK_FALSE(plan.cas.enabled);
  CHECK(plan.SamplesFor(1) == 1);
  CHECK(plan.SamplesFor(2) == 2);
}

TEST_CASE("a Resolution scale draws every target at that multiple and fits the window") {
  Settings settings;
  settings.resolution_scale = 2;
  const Plan plan = MakePlan(settings, 1920, 1080, kDesktop);
  CHECK(plan.scale == 2);
  CHECK(plan.output.downsample);
  CHECK(plan.output.output_width == 1920);
  CHECK(plan.output.output_height == 1080);
}

TEST_CASE("a scale whose targets do not fit the device is clamped, and says so") {
  // Scale 4 needs 5120-wide targets; Vulkan guarantees only 4096.
  Settings settings;
  settings.resolution_scale = 4;
  DeviceLimits small = kDesktop;
  small.max_image_size = 4096;
  const Plan clamped = MakePlan(settings, 3840, 2160, small);
  CHECK(clamped.scale == 3);
  CHECK(clamped.clamped);
  DeviceLimits large = kDesktop;
  large.max_image_size = 8192;
  const Plan fits = MakePlan(settings, 3840, 2160, large);
  CHECK(fits.scale == 4);
  CHECK_FALSE(fits.clamped);
  settings.resolution_scale = 3;
  CHECK_FALSE(MakePlan(settings, 3840, 2160, small).clamped);
}

TEST_CASE("the texture sharpness bias is 0 when the frame is drawn at or above the window") {
  Settings settings;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).lod_bias == 0.0f);  // the original
  settings.resolution_scale = 2;
  CHECK(MakePlan(settings, 2560, 1440, kDesktop).lod_bias == 0.0f);  // equal size
  CHECK(MakePlan(settings, 1920, 1080, kDesktop).lod_bias == 0.0f);  // supersampled
  settings.resolution_scale = 4;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).lod_bias == 0.0f);
}

TEST_CASE("the texture sharpness bias sharpens by the presenter's upscale") {
  Settings settings;
  // 720p shown at 1440p: one mip sharper.
  CHECK(MakePlan(settings, 2560, 1440, kDesktop).lod_bias == -1.0f);
  // 1440p shown at 2160p: log2(1440 / 2160).
  settings.resolution_scale = 2;
  CHECK(MakePlan(settings, 3840, 2160, kDesktop).lod_bias ==
        Catch::Approx(-0.585).margin(0.001));
  // The window's 16:9 area counts, not its whole height.
  settings.resolution_scale = 1;
  CHECK(MakePlan(settings, 1280, 1440, kDesktop).lod_bias == 0.0f);
  // Never sharper than the device allows.
  DeviceLimits device = kDesktop;
  device.max_lod_bias = 2.0f;
  CHECK(MakePlan(settings, 7680, 4320, device).lod_bias == -2.0f);
  // Before the window has a size, nothing is upscaled.
  CHECK(MakePlan(settings, 0, 0, kDesktop).lod_bias == 0.0f);
}

TEST_CASE("anisotropic filtering at Original leaves the game's own filtering") {
  CHECK(MakePlan({}, 1280, 720, kDesktop).texture_filtering == -1);
}

// The level is the plan's; the device clamp is ChooseSampler's (test_sampler_choice.cpp), the one
// place a sampler's anisotropy is decided.
TEST_CASE("a forced anisotropic filtering level is the plan's, in anisotropic_override's form") {
  Settings settings;
  settings.anisotropic_filtering = 16;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).texture_filtering == 5);
  settings.anisotropic_filtering = 4;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).texture_filtering == 3);
  settings.anisotropic_filtering = 8;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).texture_filtering == 4);
}

TEST_CASE("filtering is forced when the setting or a direct anisotropic_override forces it") {
  // Neither: the game's own (-1), on both.
  CHECK_FALSE(FilteringForced(-1, "-1"));
  CHECK_FALSE(FilteringForced(-1, ""));
  // The setting alone, or the override alone (Classic takes it over the setting).
  CHECK(FilteringForced(5, "-1"));
  CHECK(FilteringForced(-1, "5"));
  // 0 forces filtering off, which is not the game's own.
  CHECK(FilteringForced(-1, "0"));
  // An override that is not a number forces nothing.
  CHECK_FALSE(FilteringForced(-1, "high"));
}

TEST_CASE("sharpening levels map to fixed CAS strengths, and Off leaves CAS off") {
  Settings settings;
  CHECK_FALSE(MakePlan(settings, 1280, 720, kDesktop).cas.enabled);
  settings.sharpening = Sharpening::kLow;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).cas.sharpness == 0.25);
  settings.sharpening = Sharpening::kMedium;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).cas.sharpness == 0.5);
  settings.sharpening = Sharpening::kHigh;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).cas.sharpness == 0.8);
}

TEST_CASE("a --present_effect of cas sharpens, and bilinear does not") {
  CHECK(PresentEffectSharpens("cas"));
  CHECK_FALSE(PresentEffectSharpens("bilinear"));
  CHECK_FALSE(PresentEffectSharpens(""));
}

TEST_CASE("the settings' values parse, and a value they do not allow is the default") {
  const Settings parsed = ParseSettings({.anisotropic_filtering = "16", .sharpening = "high"});
  CHECK(parsed.anisotropic_filtering == 16);
  CHECK(parsed.sharpening == Sharpening::kHigh);
  const Settings typo = ParseSettings({.anisotropic_filtering = "15", .sharpening = "hgih"});
  CHECK(typo.anisotropic_filtering == 0);
  CHECK(typo.sharpening == Sharpening::kOff);
  CHECK(ParseSettings({.anisotropic_filtering = "original", .sharpening = "off"}).anisotropic_filtering == 0);
}

TEST_CASE("the Resolution scale parses, and an invalid one is 1x") {
  CHECK(ParseSettings({.resolution_scale = "3"}).resolution_scale == 3);
  CHECK(ParseSettings({.resolution_scale = "1"}).resolution_scale == 1);
  CHECK(ParseSettings({.resolution_scale = "5"}).resolution_scale == 1);
  CHECK(ParseSettings({.resolution_scale = "22"}).resolution_scale == 1);
}

TEST_CASE("texel offsets into a resolved texture are measured in guest texels") {
  // A resolve target drawn at 2x is 2x the guest size: a one-texel offset spans two.
  CHECK(TextureOffsetScale(2, true) == 2);
  // A texture the game loaded from memory is its guest size at any scale.
  CHECK(TextureOffsetScale(1, false) == 1);
  // A resolve target made before a scale change keeps its own scale.
  CHECK(TextureOffsetScale(3, true) == 3);
  // At 1x, offsets are as before.
  CHECK(TextureOffsetScale(1, true) == 1);
}

TEST_CASE("MSAA raises only the targets the game multisamples") {
  Settings settings;
  settings.msaa = 4;
  const Plan plan = MakePlan(settings, 1280, 720, kDesktop);
  CHECK(plan.SamplesFor(2) == 4);  // the match pass
  CHECK(plan.SamplesFor(1) == 1);  // everything else stays single-sampled
  settings.msaa = 8;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).SamplesFor(2) == 8);
  CHECK(MakePlan(settings, 1280, 720, kDesktop).SamplesFor(1) == 1);
}

TEST_CASE("an MSAA count the device lacks falls back to the highest it has below it") {
  Settings settings;
  settings.msaa = 8;
  DeviceLimits no_eight = kDesktop;
  no_eight.sample_counts = 1 | 2 | 4;
  CHECK(MakePlan(settings, 1280, 720, no_eight).SamplesFor(2) == 4);
  DeviceLimits required_only = kDesktop;
  required_only.sample_counts = 1 | 4;  // what Vulkan guarantees
  CHECK(MakePlan({}, 1280, 720, required_only).SamplesFor(2) == 1);
  CHECK(MakePlan(settings, 1280, 720, required_only).SamplesFor(2) == 4);
}

TEST_CASE("the MSAA setting parses, and an invalid one is Original") {
  CHECK(ParseSettings({.msaa = "4"}).msaa == 4);
  CHECK(ParseSettings({.msaa = "8"}).msaa == 8);
  CHECK(ParseSettings({.msaa = "original"}).msaa == 0);
  CHECK(ParseSettings({.msaa = "2"}).msaa == 0);
}

TEST_CASE("Auto picks the smallest multiple of 1280x720 that covers the window") {
  Settings settings;
  settings.resolution_scale = kAutoScale;
  CHECK(MakePlan(settings, 1280, 720, kDesktop).scale == 1);
  CHECK(MakePlan(settings, 1920, 1080, kDesktop).scale == 2);
  CHECK(MakePlan(settings, 2560, 1440, kDesktop).scale == 2);
  CHECK(MakePlan(settings, 3840, 2160, kDesktop).scale == 3);
  CHECK(MakePlan(settings, 5120, 2880, kDesktop).scale == 4);
  // Ultrawide: its width needs three times 1280.
  CHECK(MakePlan(settings, 3440, 1440, kDesktop).scale == 3);
  // Capped at 4, then clamped to the device.
  CHECK(MakePlan(settings, 7680, 4320, kDesktop).scale == 4);
  DeviceLimits small = kDesktop;
  small.max_image_size = 4096;
  const Plan clamped = MakePlan(settings, 7680, 4320, small);
  CHECK(clamped.scale == 3);
  CHECK(clamped.clamped);
}

TEST_CASE("Auto follows the window: a resize gives the new window's scale") {
  Settings settings;
  settings.resolution_scale = kAutoScale;
  CHECK(MakePlan(settings, 1600, 900, kDesktop).scale == 2);
  CHECK(MakePlan(settings, 3840, 2160, kDesktop).scale == 3);
  CHECK(MakePlan(settings, 1280, 720, kDesktop).scale == 1);
}

TEST_CASE("Auto on a small window, or before the window has a size, stays at 1x") {
  Settings settings;
  settings.resolution_scale = kAutoScale;
  CHECK(MakePlan(settings, 960, 540, kDesktop).scale == 1);
  CHECK(MakePlan(settings, 0, 0, kDesktop).scale == 1);
  CHECK_FALSE(MakePlan(settings, 960, 540, kDesktop).clamped);
}

TEST_CASE("Auto parses") {
  CHECK(ParseSettings({.resolution_scale = "auto"}).resolution_scale == kAutoScale);
}
