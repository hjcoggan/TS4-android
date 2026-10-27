# 04: Resolution scale setting (fixed steps)

**What to build:** A player setting for Resolution scale on the Native renderer: 1x (default), 2x, 3x or 4x. A scale whose render targets don't fit the device's maximum image size is clamped down, and the port logs and reports the clamp; it never fails. The texture sharpness bias follows the new rule: 0 when the render is at or above the output, log2(render height / output height) when the presenter upscales. The game's own per-fetch biases are still added. The scale changes live by rebuilding targets between frames; if that proves unstable mid-match, it takes effect at the next start and says so. On Classic it is ignored and logged as unavailable. Spec: `../spec.md`.

**Blocked by:** 01 (Image-quality plan prefactor)

**Status:** ready-for-agent

- [ ] The setting loads from the settings file and the command line, and an invalid value falls back to 1x
- [ ] Scales 2 and 3 run each scene stably, with no device loss
- [ ] Clamping is unit-tested on 4096 and 8192 image limits and logged when it happens
- [ ] The bias rule is unit-tested when supersampling, at equal size and when upscaling; at 1x on a 720p output the bias is 0
- [ ] A live change of scale mid-match works, or the setting is marked next-start with a log line
- [ ] On Classic the setting does nothing and the log says it is unavailable
- [ ] Frames at 2x, shrunk to 1280x720, are compared with the 1x frame of the same run: only edge differences, no area differences except those ticket 05 fixes
- [ ] The setting is listed in the session report's enhancements and in the scene runner's list
