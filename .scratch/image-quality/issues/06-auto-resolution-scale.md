# 06: Auto Resolution scale

**What to build:** An Auto choice for Resolution scale. It picks the smallest whole multiple of 1280x720 that covers the window (capped at 4, then clamped to the device). It is recomputed whenever the output size changes: on resize, when switching to fullscreen, or after moving the window to another monitor. It uses the live rebuild from ticket 04. Spec: `../spec.md`.

**Blocked by:** 04 (Resolution scale setting)

**Status:** ready-for-agent

- [ ] Auto is accepted in the settings file and on the command line
- [ ] Unit tests cover Auto at 720p, 1080p, 1440p, 4K and ultrawide outputs, and after a resize
- [ ] A run that resizes the window changes the scale, and the log shows the new scale
- [ ] Auto on a small window stays at 1x
