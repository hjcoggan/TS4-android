"""scripts/scene_runner.py: scene files, the rules a run is held to, the run-to-run verdict, and
the lock that keeps game runs from overlapping. The game itself is not started here."""

import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from scene_runner import (DEFAULTS, GameRunLock, RunLockBusy, SceneError, load_scene, repeatability,  # noqa: E402
                          run_failures)

GOOD = """
name = "boot-main-menu"
from = "boot"
input = '''
at 1100
press START
'''
[[capture]]
frame = 1300
[[capture]]
frame = 350
"""


def test_a_valid_scene_has_its_frames_in_order_and_the_default_checks():
    scene = load_scene(GOOD)
    assert scene.name == "boot-main-menu"
    assert scene.frames == [350, 1300]
    assert "press START" in scene.input
    assert scene.check == DEFAULTS


def test_check_overrides_one_value_and_keeps_the_rest():
    scene = load_scene(GOOD + "\n[check]\ntile_hot_max = 80\n")
    assert scene.check["tile_hot_max"] == 80
    assert scene.check["runs"] == DEFAULTS["runs"]


@pytest.mark.parametrize("text, says", [
    ('from = "boot"\ninput = ""\n[[capture]]\nframe = 1\n', "name"),
    ('name = "x"\nfrom = "title"\ninput = ""\n[[capture]]\nframe = 1\n', "from"),
    ('name = "x"\nfrom = "boot"\ninput = ""\n', "capture"),
    ('name = "x"\nfrom = "boot"\ninput = ""\n[[capture]]\nframe = -3\n', "frame"),
    ('name = "x"\nfrom = "boot"\ninput = ""\n[[capture]]\nframe = 1\n[check]\nfoo = 1\n', "foo"),
    ('name = "x"\nfrom = "boot"\ninput = ""\n[[capture]]\nframe = 1\n[check]\nruns = 1\n', "runs"),
    ("name = ", "TOML"),
])
def test_bad_scenes_say_what_is_wrong(text, says):
    with pytest.raises(SceneError, match=says):
        load_scene(text)


def report(*frames, guest_swaps=None, max_luma=200):
    last = max(frames)
    return {"exit_after_frame": last,
            "guest_swaps": last + 1 if guest_swaps is None else guest_swaps,
            "dumps": [{"frame": f, "file": f"frame-{f:06}.png", "width": 1280, "height": 720,
                       "max_luma": max_luma} for f in frames]}


def test_a_run_that_exited_with_every_capture_passes():
    assert run_failures(report(350, 1300), [350, 1300]) == []


def test_a_run_without_a_report_did_not_exit_on_its_own():
    assert run_failures(None, [350]) == ["no session report (the run did not exit on its own)"]


def test_a_missing_capture_fails_the_run_and_names_the_frame():
    assert run_failures(report(1300), [350, 1300]) == ["no capture of frame 350"]


def test_a_black_capture_fails_the_run():
    assert run_failures(report(1300, max_luma=10), [1300]) == ["capture of frame 1300 is black"]


def test_the_dump_clock_must_match_the_pads():
    # Frame 1300 is the 1301st swap; the CPU may have issued a frame or two more meanwhile.
    assert run_failures(report(1300, guest_swaps=1303), [1300]) == []
    assert run_failures(report(1300, guest_swaps=1290), [1300]) == [
        "frame clocks disagree: frame 1300 shown after only 1290 guest swaps"]
    assert run_failures(report(1300, guest_swaps=1310), [1300]) == [
        "frame clocks disagree: frame 1300 shown with 1310 guest swaps issued"]


def absdiff(reference, test):
    """A stand-in for FLIP: per-pixel mean absolute difference, 0..1."""
    return np.abs(reference - test).mean(axis=2)


GREY = np.full((64, 96, 3), 0.2, np.float32)


def test_identical_runs_repeat():
    verdicts = repeatability([GREY, GREY.copy(), GREY.copy()], DEFAULTS, absdiff)
    assert list(verdicts) == ["run-2", "run-3"]
    assert all(v["passed"] and v["worst_tile_hot"] == 0 for v in verdicts.values())


def test_a_changed_block_fails_and_names_its_tile():
    changed = GREY.copy()
    changed[32:64, 32:64] = 1.0  # one whole 32x32 tile: row 1, column 1
    verdict = repeatability([GREY, changed], DEFAULTS, absdiff)["run-2"]
    assert not verdict["passed"]
    assert verdict["worst_tile_hot"] == 32 * 32
    assert verdict["failing_tiles"] == [[1, 1]]


def test_hot_pixels_up_to_the_tile_limit_still_repeat():
    changed = GREY.copy()
    changed[0, :50] = 1.0  # 50 hot pixels in tiles (0,0) and (0,1): 32 and 18
    verdict = repeatability([GREY, changed], DEFAULTS, absdiff)["run-2"]
    assert verdict["passed"]
    assert verdict["worst_tile_hot"] == 32


def test_a_second_game_run_is_refused_while_one_holds_the_lock(tmp_path):
    lock = tmp_path / "game-run.lock"
    with GameRunLock(lock):
        with pytest.raises(RunLockBusy):
            with GameRunLock(lock):
                pass
    with GameRunLock(lock):
        pass


def test_the_lock_holds_against_the_smoke_runs_exclusive_open(tmp_path):
    # scripts/smoke.ps1 takes the same file with FileShare.None.
    lock = tmp_path / "game-run.lock"
    holder = subprocess.Popen(
        ["pwsh", "-NoProfile", "-Command",
         f"$f = [IO.File]::Open('{lock}', 'OpenOrCreate', 'ReadWrite', 'None'); "
         "[Console]::Out.WriteLine('held'); [Console]::Out.Flush(); [Console]::In.ReadLine()"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    try:
        assert holder.stdout.readline().strip() == "held"
        with pytest.raises(RunLockBusy):
            with GameRunLock(lock):
                pass
    finally:
        holder.communicate("\n", timeout=30)
    with GameRunLock(lock):
        pass
