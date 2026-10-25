"""Play an input script (src/script_input.h grammar) on a virtual Xbox 360 pad, for the Oracle.

Xenia Edge ignores posted and injected keys, but sees a ViGEm virtual pad as a real XInput
controller (ViGEmBus driver, `pip install vgamepad`). Edge exposes no frame count, so frame times
are wall-clock at 60 frames a second from when this starts: `at 600` is 10 s in, `press A 6f`
holds for 0.1 s. Edge boots slower than the port, so an Edge script needs its own `at` times.

    python scripts/edge_pad.py <script.txt> [--hold-after SECONDS]

Verbs: press, hold, stick, trigger, wait, at (as in src/script_input.h). Unknown verbs are
reported and skipped. The pad stays connected for --hold-after seconds after the last step
(default 0) and is unplugged on exit.
"""
import argparse
import sys
import time
from pathlib import Path

FRAME = 1 / 60
RELEASE_FRAMES = 7  # the neutral gap after a press, as the port's scripted pad

BUTTONS = {
    "A": "XUSB_GAMEPAD_A", "B": "XUSB_GAMEPAD_B", "X": "XUSB_GAMEPAD_X", "Y": "XUSB_GAMEPAD_Y",
    "START": "XUSB_GAMEPAD_START", "BACK": "XUSB_GAMEPAD_BACK",
    "LB": "XUSB_GAMEPAD_LEFT_SHOULDER", "RB": "XUSB_GAMEPAD_RIGHT_SHOULDER",
    "LS": "XUSB_GAMEPAD_LEFT_THUMB", "RS": "XUSB_GAMEPAD_RIGHT_THUMB",
    "UP": "XUSB_GAMEPAD_DPAD_UP", "DOWN": "XUSB_GAMEPAD_DPAD_DOWN",
    "LEFT": "XUSB_GAMEPAD_DPAD_LEFT", "RIGHT": "XUSB_GAMEPAD_DPAD_RIGHT",
}


def duration(token, default_seconds):
    """'120' is milliseconds, '6f' is frames; None gives the default."""
    if token is None:
        return default_seconds
    if token.endswith("f"):
        return int(token[:-1]) * FRAME
    return int(token) / 1000


def parse(lines):
    """Steps as (kind, seconds or absolute time, state dict, source line)."""
    steps = []
    for raw in lines:
        line = raw.split("#", 1)[0].strip()
        if not line:
            continue
        verb, *args = line.split()
        verb = verb.lower()
        state = {"buttons": [], "lx": 0, "ly": 0, "rx": 0, "ry": 0, "lt": 0, "rt": 0}
        try:
            if verb in ("press", "hold"):
                state["buttons"] = args[0].upper().split("+")
                seconds = duration(args[1] if len(args) > 1 else None, 0.120)
                steps.append(("hold", seconds, state, line))
                steps.append(("hold", RELEASE_FRAMES * FRAME if (len(args) > 1 and args[1].endswith("f"))
                              else 0.120, dict(state, buttons=[]), "release"))
            elif verb == "stick":
                side, x, y, t = args[0].upper(), int(args[1]), int(args[2]), args[3]
                state["lx" if side == "L" else "rx"], state["ly" if side == "L" else "ry"] = x, y
                if len(args) > 4 and args[4] != "-":
                    state["buttons"] = args[4].upper().split("+")
                if len(args) > 5:
                    state["rt"] = int(args[5])
                steps.append(("hold", duration(t, 0.120), state, line))
            elif verb == "trigger":
                state["lt" if args[0].upper() == "L" else "rt"] = int(args[1])
                steps.append(("hold", duration(args[2], 0.120), state, line))
            elif verb == "wait":
                steps.append(("hold", duration(args[0], 0.120), state, line))
            elif verb == "at":
                steps.append(("at", int(args[0]) * FRAME, state, line))
            else:
                print(f"edge_pad: unknown verb skipped: {line}", flush=True)
        except (IndexError, ValueError):
            print(f"edge_pad: bad line skipped: {line}", flush=True)
    return steps


def apply(pad, vg, state):
    pad.reset()
    for name in state["buttons"]:
        if name in BUTTONS:
            pad.press_button(getattr(vg.XUSB_BUTTON, BUTTONS[name]))
        elif name:
            print(f"edge_pad: unknown button {name}", flush=True)
    pad.left_joystick(state["lx"], state["ly"])
    pad.right_joystick(state["rx"], state["ry"])
    pad.left_trigger(state["lt"])
    pad.right_trigger(state["rt"])
    pad.update()


def main(argv):
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n")[0])
    parser.add_argument("script", type=Path)
    parser.add_argument("--hold-after", type=float, default=0.0)
    args = parser.parse_args(argv)
    steps = parse(args.script.read_text(encoding="utf-8").splitlines())
    import vgamepad as vg
    pad = vg.VX360Gamepad()
    start = time.monotonic()
    try:
        for kind, seconds, state, line in steps:
            if kind == "at":
                apply(pad, vg, state)
                delay = start + seconds - time.monotonic()
                if delay > 0:
                    time.sleep(delay)
                continue
            print(f"edge_pad: {time.monotonic() - start:6.2f}s {line}", flush=True)
            apply(pad, vg, state)
            time.sleep(seconds)
        apply(pad, vg, {"buttons": [], "lx": 0, "ly": 0, "rx": 0, "ry": 0, "lt": 0, "rt": 0})
        time.sleep(args.hold_after)
    finally:
        del pad
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
