# 07: Pad combo opens the Settings page

**What to build:** A controller player opens the Settings page mid-match with a pad combo the game never reads. First confirm which inputs Top Spin 4 reads. Clicking both sticks is ruled out, because the game uses them. Then pick a combo, such as holding Back and Start, and record the evidence. Spec: `.scratch/game-settings/spec.md` (Opening the page).

**Blocked by:** 03 (Settings page tracer bullet)

**Status:** ready-for-human

- [ ] Evidence, cited in Comments, that the chosen combo triggers nothing in game (menus, match, pause)
- [ ] A scene run opens the page with the combo mid-match and closes it again
- [ ] The combo doesn't also reach the game as input when it opens the page

## Comments

**2026-10-28, implementation (not run).** Combo: hold **Back + Start** (`src/settings/open_combo.h`, wired in `HoldGuestInput` in `src/settings/settings_page.cpp`).

Static evidence of the game's input use (Swing_DLL, generated code):
- The one real pad reader is `sub_888865D0`. It calls `XamInputGetState` through `sub_8857B170`, then turns every `XINPUT_GAMEPAD` bit of the state into an engine input channel with `sub_88609640`: A (bit 12), B (13), X (14), Y (15), LB (8), RB (9), Start (bit 4), Back (bit 5), left and right stick click (6, 7), the four D-pad bits, both triggers (bytes at +42/+43) and both sticks (`sub_88609550`). Back and Start are translated like the rest, so the game can see them.
- The Rui front end names its pad inputs as controls: the strings `DPad`, `RightStick`, `LeftStick`, `RTrigger`, `LTrigger`, `Back`, `Start`, `YButton`, `XButton`, `WhiteButton`, `BlackButton`, `BButton`, `AButton` sit together next to `ControlsInfo` and `GlobalInputs`. Which scene binds which of them is in the Lua scenes inside the compressed `.arc` archives, which were not unpacked here.
- Result: **no button is provably unread**, so "the game never reads it" cannot be shown statically. The sticks are ruled out (clicks are read). Back and Start were chosen because a chord of two buttons that nothing else needs together is the least likely to be bound as a chord.
- Not done: the scene run (no game run was made) and the on-pad test for "no input reached the game". The first box (evidence that the combo triggers nothing in the game) is therefore not met as written.

**2026-10-28, review fix: the combo filter moved to the pad-state level.** The first version held the whole pad from the game for 100 ms whenever Back or Start went down alone (`HoldGuestInput`), so a tap shorter than 100 ms never reached the game and every other input froze for the window. Now:
- `OpenComboFilter` (`src/settings/open_combo.h`, pure) masks only the Back and Start bits of a lone press for `kComboWindowMs` (100 ms); sticks, triggers and every other button pass at once.
- Released inside the window: the button is replayed, shown pressed for `kReplayMs` (50 ms) after the release, so a tap is late, not lost. Still held at the window's end: it passes through from then on, and a later partner opens nothing (shown for at least `kReplayMs` in all if released right after).
- Both down: the page opens (once per press) and the game sees neither until both are up.
- The filter runs in `RemappedDriver::GetDeviceState` (`src/remapped_input.cpp`), on the physical pad before the player's bindings. The wrapper is now always installed, with default bindings it changes nothing else. Back and Start keystrokes (XUI screens read keystrokes) are taken from the filtered state, not from the controller driver, so they are held and replayed the same way. A scripted run's player 1 pad has the same filter (`ScriptInputDriver::EnableOpenCombo`), so a script can open the page with `press back+start`.
- `HoldGuestInput` now only holds while the page is open (`g.hold`); the filter calls the page through `SetOpenComboHandler`.
- Host tests: `tests/host/test_options_row.cpp` (chord, hold pass-through, tap replay, masking only those bits). Unit tests pass; no game run.
- Not verified: a real pad (SDL and XInput) and a scripted `press back+start`; the keyboard-and-mouse driver is not filtered (F4 opens the page there); a lone Start held past 100 ms reaches the game 100 ms late, which may matter for pause timing. Any pad on the driver can open the page, not just player 1.

**2026-10-28, implementation (/implement-spec, branch `game-settings`):** Implemented in cea0404, combo filter reworked in b694c42; review fixes in b694c42. Host or Launcher tests cover: chord, hold pass-through, tap replay, masking only Back/Start. Needs the owner's run: Back+Start opens the page mid-match and nothing reaches the game; quick Start taps still work in menus; static evidence shows the game reads Back and Start, so box 1 needs run evidence. No game run was made (owner tests).
