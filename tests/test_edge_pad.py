"""scripts/edge_pad.py's reading of an input script (src/script_input.h grammar) as wall-clock
steps at 60 frames a second. The virtual pad itself is not touched here."""

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))

from edge_pad import parse  # noqa: E402


def test_at_is_an_absolute_time_at_sixty_frames_a_second():
    [(kind, seconds, state, _)] = parse(["at 600"])
    assert kind == "at" and seconds == pytest.approx(10.0) and state["buttons"] == []


def test_a_frame_timed_press_holds_then_releases_for_seven_frames():
    press, release = parse(["press START+A 6f"])
    assert press[1] == pytest.approx(6 / 60) and press[2]["buttons"] == ["START", "A"]
    assert release[1] == pytest.approx(7 / 60) and release[2]["buttons"] == []


def test_a_bare_press_lasts_120_ms_and_comments_are_ignored():
    steps = parse(["# intro", "press B  # skip"])
    assert [s[1] for s in steps] == [pytest.approx(0.12), pytest.approx(0.12)]
    assert steps[0][2]["buttons"] == ["B"]


def test_stick_and_trigger_set_their_axes():
    [stick] = parse(["stick L 0 32767 30f A 255"])
    assert (stick[2]["lx"], stick[2]["ly"], stick[2]["buttons"], stick[2]["rt"]) == (0, 32767, ["A"], 255)
    [trigger] = parse(["trigger R 128 10f"])
    assert trigger[2]["rt"] == 128 and trigger[1] == pytest.approx(10 / 60)


def test_unknown_or_broken_lines_are_skipped(capsys):
    assert parse(["jump high", "press"]) == []
    assert "skipped" in capsys.readouterr().out
