# 05: Graphics presets

**What to build:** A Graphics Preset row offers Original and Enhanced. Choosing one sets all its settings at once; changing any covered setting makes the row read Custom. Original (every covered setting at its default) is the default, so the first start looks like the 360 game (ADR-0001). Enhanced is Auto Resolution scale, 16x anisotropic filtering, 4x MSAA and sharpening off. On Classic, Enhanced's Native-only parts show as unavailable and don't make the row read Custom. The session report records the active Preset, or Custom. Spec: `.scratch/game-settings/spec.md` (Presets).

**Blocked by:** 03 (Settings page tracer bullet)

**Status:** ready-for-agent

- [ ] Model tests: defaults read Original; choosing Enhanced sets its four settings; changing one gives Custom; returning to the Preset's values gives the Preset back; Classic behaves as above
- [ ] A run picks Enhanced on Native, and the frame and the Settings file show all four values
- [ ] The session report lists the Preset (Original, Enhanced or Custom)
