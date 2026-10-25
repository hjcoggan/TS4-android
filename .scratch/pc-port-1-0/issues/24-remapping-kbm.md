# 24: Remapping and keyboard/mouse

**What to build:** Every game action can be rebound on a controller and on keyboard and mouse, and bindings are saved per user.

**Blocked by:** 10 (Settings system and renderer switch), 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [x] XInput controllers work out of the box
- [x] Every action can be rebound on a controller
- [ ] Keyboard and mouse play works with default and custom bindings
- [x] Bindings persist per user
- [x] Unit tests cover binding resolution and conflicts

## Comments
- 2026-10-25 (Claude), ticket audit: ticked the XInput box on a run. A ViGEm virtual Xbox 360 controller (`scripts/edge_pad.py` playing `scripts/input/boot-to-main-menu.txt`) drove a headless run with real pads enabled (`smoke.ps1 -InputScript ''`). SDL detected it with no configuration ("Xbox 360 Controller", connection order 0); START skipped the intro videos and passed the title screen, and frame 1400 shows the practice ground loading. The run needed `SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS=1` only because the headless window had no focus: SDL drops controller events for an unfocused window (a first run without the hint never left the intro). A player's focused window needs no hint. Gameplay input is covered by the match tickets (13, 17).
- 2026-10-25 (Claude), no game run (owner's request). Controller remapping: `src/pad_bindings.h` (pure) says which physical input drives each of the 16 pad inputs the game sees.
  - Buttons and triggers can stand in for each other (a trigger counts past XInput's threshold of 30); sticks pass through.
  - `Rebind` swaps the two bindings instead of doubling up, `Conflicts` reports doubles in a hand-edited file, and menu keystrokes (VK_PAD_*) follow the bindings.
  - `src/remapped_input.{h,cpp}` builds the SDK's default input system with the SDL or XInput driver wrapped in `RemappedDriver`. It is used only when some binding differs from the default; a scripted run still replaces the whole input system.
  - Bindings are player settings: `ts4_pad_<input>` (a, b, x, y, lb, rb, lt, rt, ls, rs, back, start, up, down, left, right; each defaults to itself), plus the SDK's keyboard and mouse (`mnk_mode`, `mnk_mouse`, and the 24 `keybind_*` with the SDK's defaults). All are saved in `topspin4.settings.toml` in the per-user folder (ticket 10).
  - Ticked the unit-test box on `build.ps1 -Action Test` (tests/host/test_pad_bindings.cpp: identity, swaps, button and trigger cross-binding, rebind swap, conflicts, settings, keystrokes; test_settings.cpp: the binding settings save and load back). The game builds.
  - Open: no in-game or launcher screen edits the bindings yet (the settings file does); rebinding and keyboard play are unobserved in a run; and whether `mnk_mode` on beside a connected controller shows up as a second pad needs checking before it could default to on.
- 2026-10-25 (Claude), after review: a physical input that drives no action (after a hand-edited double binding) now sends no keystroke, instead of its own.
- 2026-10-25 (Claude), runs with a ViGEm pad; ticked the rebinding and per-user boxes.
  - Setup: headless, no `--user_data_root`, `%APPDATA%\TopSpin4Recomp\topspin4.settings.toml` with `ts4_pad_start = "rs"` and `ts4_pad_rs = "start"`; `scripts/edge_pad.py` pressing RS every 100 frames to frame 2401 (wall clock). The log reads `Controller: start is driven by rs` and `Controller: rs is driven by start`.
  - With the swap (`analysis/remap-20261025-123709`): the intro skips, the title passes, and frame 2800 is the Practice Ground's "Welcome to Top Spin 4" menu.
  - Control, same presses and no settings file (`analysis/remap-control-20261025-123826`): RS does nothing; frame 2800 is still an intro clip.
  - So a binding in the per-user file drives the game, through both `GetState` and the keystroke path the title uses. Every one of the 16 inputs goes through the same `Remap` (unit-tested for all of them); one swap was run. Rebinding is by settings file only; no screen edits it yet.
  - Found on the way: with the owner's own Xbox Series X pad plugged in, it took slot 0 and the virtual pad slot 1, and START on slot 1 did not skip the intro either (`analysis/remap-control-20261025-123425`, frames identical to the first, failed remap run). The runs then hid the real pad with `SDL_GAMECONTROLLER_IGNORE_DEVICES=0x045E/0x0B13`. Whether the original also ignores pad 2 at the intro is for ticket 17 to check on Edge.
  - Still open: keyboard and mouse. The SDK's keyboard driver acts only while the window has focus, which a headless run never has.
