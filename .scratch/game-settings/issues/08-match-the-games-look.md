# 08: Settings page matches the game's look

**What to build:** The Settings page looks like part of Top Spin 4. It uses the game's fonts and colours, as WWE did for its page, and stays readable on the Steam Deck screen (1280x800). Spec: `.scratch/game-settings/spec.md` (Settings page UI; User Stories 9, 26).

**Blocked by:** 03 (Settings page tracer bullet)

**Status:** ready-for-agent

- [ ] The page uses the game's font and palette, loaded from the player's game data at runtime (no game data committed, ADR-0006)
- [ ] The owner reviews screenshots at 1280x720, 1920x1080 and 1280x800
- [ ] Text is readable at 1280x800, with no clipped rows

## Comments

**2026-10-28, implementation (not run).** Font: the game's own UI font, `Media\XUI\DroidSans-Bold.ttf` under the game data root, loaded at runtime (`AddFonts`, fallback Segoe UI Bold, then ImGui's). Palette: sampled from a capture of the game's main menu (white rows with dark text, orange selected row, blue background), as constants in `src/settings/page_style.h`; it is **not** read from the game data, because the front end's colours are in compressed Lua scenes. Text scales with the display height; labels and values shrink to fit their columns, rows clip to their bar, help and note text shrink to end above the panel. Unit tests check contrast and fitting; screenshots at the three sizes still need the owner.
