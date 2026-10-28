# 07: Pad combo opens the Settings page

**What to build:** A controller player opens the Settings page mid-match with a pad combo the game never reads. First confirm which inputs Top Spin 4 reads. Clicking both sticks is ruled out, because the game uses them. Then pick a combo, such as holding Back and Start, and record the evidence. Spec: `.scratch/game-settings/spec.md` (Opening the page).

**Blocked by:** 03 (Settings page tracer bullet)

**Status:** ready-for-agent

- [ ] Evidence, cited in Comments, that the chosen combo triggers nothing in game (menus, match, pause)
- [ ] A scene run opens the page with the combo mid-match and closes it again
- [ ] The combo doesn't also reach the game as input when it opens the page

## Comments

**2026-10-28, implementation (not run).** Combo: hold **Back + Start** (`src/settings/open_combo.h`, wired in `HoldGuestInput` in `src/settings/settings_page.cpp`).

Static evidence of the game's input use (Swing_DLL, generated code):
- The one real pad reader is `sub_888865D0`. It calls `XamInputGetState` through `sub_8857B170`, then turns every `XINPUT_GAMEPAD` bit of the state into an engine input channel with `sub_88609640`: A (bit 12), B (13), X (14), Y (15), LB (8), RB (9), Start (bit 4), Back (bit 5), left and right stick click (6, 7), the four D-pad bits, both triggers (bytes at +42/+43) and both sticks (`sub_88609550`). Back and Start are translated like the rest, so the game can see them.
- The Rui front end names its pad inputs as controls: the strings `DPad`, `RightStick`, `LeftStick`, `RTrigger`, `LTrigger`, `Back`, `Start`, `YButton`, `XButton`, `WhiteButton`, `BlackButton`, `BButton`, `AButton` sit together next to `ControlsInfo` and `GlobalInputs`. Which scene binds which of them is in the Lua scenes inside the compressed `.arc` archives, which were not unpacked here.
- Result: **no button is provably unread**, so "the game never reads it" cannot be shown statically. The sticks are ruled out (clicks are read). Back and Start were chosen because a chord of two buttons that nothing else needs together is the least likely to be bound as a chord, and because the guest input hold removes the chord from the game: `InputSystem::GetState` stores the real state, then calls the hold, and the hold (`HoldGuestInput`) reads that state with `HeldState`, sees both buttons down, turns the hold on in that same poll and the game gets an idle pad from that poll on. The page then holds until the pad is released.
- Residual risk: whichever of the two buttons goes down first is delivered alone for that poll (about 16 ms when pressed together). Start is the pause button in a match, so a slow press can pause the game under the page. Needs an owner run to judge; if it bites, move to a three-button chord.
- Not done: the scene run (no game run was made) and the on-pad test for "no input reached the game". The first box (evidence that the combo triggers nothing in the game) is therefore not met as written.
