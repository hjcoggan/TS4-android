# 07: MSAA 4x

**What to build:** A player setting for MSAA on the Native renderer: Original (the game's own 2x on the match pass) or 4x. It raises the sample count only for targets the game creates multisampled; targets the game creates at 1x stay at 1x. A sample count the device doesn't support falls back to the highest supported count at or below the request. It changes live, or takes effect at the next start if a live change proves unstable. On Classic it is ignored and logged as unavailable. Spec: `../spec.md`.

**Blocked by:** 01 (Image-quality plan prefactor)

**Status:** ready-for-agent

- [ ] The setting loads from the settings file and the command line, and an invalid value falls back to Original
- [ ] At 4x, the match pass renders at 4 samples and 1x passes are unchanged (render-target log lines)
- [ ] Alpha-masked surfaces (Academy trees) look right at 4x
- [ ] The match frame at 4x is compared with the Original frame of the same run: only edge differences
- [ ] Unit tests cover the plan's sample count for 1x and 2x targets and the unsupported-count fallback
- [ ] The setting is listed in the session report's enhancements and in the scene runner's list
