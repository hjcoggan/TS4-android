# 24: Remapping and keyboard/mouse

**What to build:** Every game action can be rebound on a controller and on keyboard and mouse, and bindings are saved per user.

**Blocked by:** 10 (Settings system and renderer switch), 13 (Full Exhibition match vs CPU)

**Status:** ready-for-agent

- [x] XInput controllers work out of the box
- [ ] Every action can be rebound on a controller
- [ ] Keyboard and mouse play works with default and custom bindings
- [ ] Bindings persist per user
- [ ] Unit tests cover binding resolution and conflicts

## Comments
- 2026-10-25 (Claude), ticket audit: ticked the XInput box on a run. A ViGEm virtual Xbox 360 controller (`scripts/edge_pad.py` playing `scripts/input/boot-to-main-menu.txt`) drove a headless run with real pads enabled (`smoke.ps1 -InputScript ''`). SDL detected it with no configuration ("Xbox 360 Controller", connection order 0); START skipped the intro videos and passed the title screen, and frame 1400 shows the practice ground loading. The run needed `SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS=1` only because the headless window had no focus: SDL drops controller events for an unfocused window (a first run without the hint never left the intro). A player's focused window needs no hint. Gameplay input is covered by the match tickets (13, 17).
