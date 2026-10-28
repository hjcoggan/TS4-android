# 13: Fixes from the branch code review

**What to build:** The two-axis code review of `6046ee7...0deb073` (standards and spec), run on 2026-10-27, found the items below. Fix them in one pass. Item 1 needs the owner's decision first. Spec: `../spec.md`.

**Blocked by:** None (item 1 needs the owner)

**Status:** resolved

1. **Owner decision: the default look in a window larger than 720p.** `MakePlan` sets `lod_bias = log2(720 / shown height)` at 1x whenever the presenter upscales. A default session in a 1080p window gets -0.58 on Native, while Classic gets 0. The spec's bias rule (spec.md, "Texture sharpness bias rule") asks for exactly this, but ADR-0001 and the spec's own "With every setting at its default, both renderers look like the Oracle again" say the default must look original. No scene run catches it, because all scene runs are 1280x720. Either keep the bias at 0 when the scale is at its default (1x), or record an exception to ADR-0001. Add a default-plan test at a 1080p window either way.
2. **The LOD bias limit is not read from the device.** `LimitsOf` hard-codes `max_lod_bias = 2.0f`, and `textures.cpp` passes a literal `2.0f` to `ChooseSampler`. Read `maxSamplerLodBias` from the device and pass the plan's limit through.
3. **Live sharpening can fail silently.** When `present_effect` is anything but bilinear, `ApplyImageSettings` returns before it registers the `ts4_sharpening` callback. A live change then does nothing and logs nothing, against user story 22 ("I want to be told"). Register the callback anyway, and log why sharpening is not applied.
4. **The descriptor index's flag bits.** The pack masks the index with `0x1FFFFFFF`, which clears bit 31 (`kPwlFetchFlag`) along with the offset-scale bits 29-30. Confirm that `xrFetchGamma` reads the flag from the unmasked index, and that a PWL fetch from a resolved texture keeps both the flag and the scale. Add a test if there is a seam.
5. **A way to pin a target to 1x.** The spec says "A target shown to break when scaled is pinned to 1x by a per-target rule, as Edge does". Ticket 05 found no target that breaks, but `GetRenderTarget` scales every target and offers no hook for a pin. Add the rule, with an empty list.
6. **Settings that apply to one renderer.** `settings::Setting` has no field saying which renderer a setting applies to, so the settings UI (pc-port ticket 10) cannot mark Resolution scale and MSAA unavailable on Classic. Add one.
7. **The comparison page upscales with PIL.** `scripts/compare_page.py` upscales the original frame with PIL bilinear, but image-quality ticket 10 asks for "the original upscaled by the presenter as a player would see it". Capture the presenter's output instead (`scripts/rdoc/query.py present`, or a dump at the output size).
8. **`--anisotropic_override` given directly.** It overrides the setting on Classic, while Native ignores it, yet the report counts it as filtering forced on both. Make both renderers follow it, or report it per renderer.
9. **Code shape (judgement calls):**
   - `image_quality::MakePlan(ImageSettings(), 0, 0, {}).cas` and `.texture_filtering` appear four times in `native_renderer.cpp`; use small helpers.
   - A new setting means editing five places: `registry.h`, `settings_cvars.cpp`, the `image_quality.h` choice lists, the `ApplyImageSettings` loop and `kEnhancements`. Gather them where it is cheap.
   - `FitHeight` repeats the 16:9-area calculation in `guest::PlanOutput`.
   - The display-text formatting (`"{}x"` or "original") is repeated in three places.
   - `InstallScriptInput`'s `std::function` restates `ScriptInputDriver::Setter`.
   - Log prefixes mix "image quality:" and "native renderer:".
   - The `BeginFrame` comment in `textures.h` runs past the line width.
10. **Off the spec's scope:** `docs/research/agent-setup.md` (Claude Code subagents) has no place on this branch; move it to its own commit on main. Patch rexglue 0039 is a renderer fix outside the spec; it stays, with ticket 12 covering its gaps.

## Comments

- 2026-10-27 (Claude): opened from the code review. Fix items 2-9 together, once item 1 is decided.
- 2026-10-28 (owner, via Claude): decisions. Item 1: keep the bias at 0 when Resolution scale is 1x; sharpen only at 2x and above. ADR-0001 stands, no exception. Add a default-plan test at a 1080p window. Item 7: take the upscaled original from a presenter dump at the output size during the normal unattended run, not RenderDoc. Item 8: both renderers follow `--anisotropic_override`. Item 10: `docs/research/agent-setup.md` moves to its own commit on main and leaves this branch. Items 2-6 and 9 as written; the per-target pin list starts empty.
- 2026-10-28 (Claude), item 7, the script side: `--ts4_dump_presented=WxH` (`src/frame_dumps.*`) saves each dump as the presenter shows it at the output size, and `scripts/compare_page.py` takes that dump as the original, using PIL only as a flagged fallback. `scripts/scene_runner_shots.py` is the command (ticket 10). Open: the presenter needs `CapturePresentedOutput(RawImage&, width, height)` (rexglue), so the item closes when that lands and the combined run shows presented images.
- 2026-10-28 (Claude): items 1-6, 8 and 9 implemented (implementer); host tests (300 cases) and `build.ps1 -Action Check` pass. QA: pass with notes. Item 4 confirmed by reading: `xrFetchGamma` tests the unmasked index (`.tools/xenosrecomp/XenosRecomp/shader_common.h:242`); the one raw lookup (`shadowCmp2D`) is compiled only under `REBLUE_RECOMP`, not this build. QA notes fixed: the startup line now logs the device's texture sharpness bias limit; the last two "native renderer:" image-setting lines use "image quality:"; the `texture_filtering` comment admits 0..5. Notes kept: Native's sampler bias clamp moves from ±2 to the device limit (same as Classic); a future pin needs both targets of a colour/depth pair; `g_sharpening_applies` is read once at startup, so a live `present_effect` change is not followed (developer flag only). Runtime checks wait for the combined game run.
- 2026-10-28 (Claude): resolved. Smoke and Oracle: `analysis/scenes/iq09/practice-ground-default-0041/` (build 8442446 + f311d9e): dual verdict PASS at both frames; against Edge's practice-ground frame (`crop_verdict.txt` there) Native passes floor, baseline and full frame, Classic floor and baseline (full frame fails around the moving player, as on ticket 02's run). The startup line logs `texture sharpness bias limit 15.996094` (item 2); no "texture sharpness bias" line at the default 1x (item 1). Ticket 10's 12 shot runs at 2560x1440 all exit 0.
