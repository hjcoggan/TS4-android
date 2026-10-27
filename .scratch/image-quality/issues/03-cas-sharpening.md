# 03: CAS sharpening setting

**What to build:** A player setting for CAS sharpening on both renderers: Off (default), Low, Medium or High. The levels map to fixed strengths, roughly 0.25, 0.5 and 0.8, tuned by eye. It runs through the shared presenter and changes live. Spec: `../spec.md`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The setting loads from the settings file and the command line, and an invalid value falls back to Off
- [ ] Each level visibly sharpens on both Classic and Native, and Off leaves the frame unchanged
- [ ] One level is compared with Edge's CAS post-process at a similar strength
- [ ] The setting is listed in the session report's enhancements and in the scene runner's list, so a parity run with it on fails
