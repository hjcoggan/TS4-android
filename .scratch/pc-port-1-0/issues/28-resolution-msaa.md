# 28: Resolution scale and MSAA

**What to build:** Native-only Enhancements for internal resolution scaling and MSAA, defaulting to original behaviour.

**Blocked by:** 27 (Native Exhibition parity)

**Status:** resolved

- [x] Internal resolution can be raised and the game stays stable (no device loss)
- [x] MSAA can be enabled
- [x] Both are shown as unavailable on Classic
- [x] Defaults match the original look
- [x] Scenes at higher resolution show no new artifacts

## Comments
- 2026-10-25 (Claude), status check: unblocked, since ticket 27 is resolved. For the defaults box: Native now creates real 2x MSAA targets where the game asks for them (`f8a55ea`), with the game's alpha-to-mask (`86197b2`), so the default look is the original's. That is the original behaviour, not this ticket's Enhancement: no setting raises resolution or MSAA yet, so no box is ticked.
- 2026-10-27 (Claude), resolved by the image-quality effort (`.scratch/image-quality/`); the evidence is in those tickets' comments:
  - Resolution raised and stable: ticket 04 (scale 2 to 4, live change), 06 (Auto), 09 box 1 (all six 1.0 scenes at scale 4, 5120x2880, unclamped, with 8x MSAA, 16x AF and High CAS: PASS, no device loss, `analysis/scenes/iq09/max-<scene>/`).
  - MSAA: tickets 07 (4x) and 08 (8x, with the fallback).
  - Unavailable on Classic: ticket 04 box 6 and ticket 07 (Classic logs both as unavailable and draws the game's own).
  - Defaults: ticket 02 (Oracle crop verdict at the defaults) and ticket 09 box 2 (the six scenes at defaults on the current build, `analysis/scenes/iq09/default-<scene>/`).
  - No new artifacts at higher resolution: ticket 05 (texture offsets at scale) and ticket 09 box 1 (only edge differences against the 1x frames of the same run).
