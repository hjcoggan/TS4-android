"""scripts/scene_runner.py: scene files, the rules a run is held to, the run-to-run verdict, and
the lock that keeps game runs from overlapping. The game itself is not started here."""

import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from scene_runner import (DEFAULTS, GameRunLock, RunLockBusy, SceneError, apply_ramp, envelope_error, fit_ramp,  # noqa: E402
                          edges_may_differ, enhancement_failures, judge_enhanced, load_scene,
                          log_failures,
                          native_failures, parse_enhancements, plan_runs, repeatability,
                          run_failures, shrink_to)

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
    ('name = "x"\nfrom = "boot"\nlog = "one"\ninput = ""\n[[capture]]\nframe = 1\n', "log"),
    ("name = ", "TOML"),
])
def test_bad_scenes_say_what_is_wrong(text, says):
    with pytest.raises(SceneError, match=says):
        load_scene(text)


def test_a_scene_names_the_log_lines_every_run_must_show():
    scene = load_scene('log = ["closed with no sign-in change"]\n' + GOOD)
    assert scene.log == ["closed with no sign-in change"]
    assert load_scene(GOOD).log == []


def test_a_run_fails_on_each_expected_log_text_it_lacks():
    log = "[info] XamShowMessageBoxUI: rewritten by the host\n"
    assert log_failures(log, ["rewritten by the host"]) == []
    assert log_failures(log, ["rewritten by the host", "XamShowSigninUI"]) == [
        "log has no line with 'XamShowSigninUI'"]
    assert log_failures(None, ["rewritten"]) == ["log has no line with 'rewritten'"]


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


def report_with(renderer):
    return {"exit_after_frame": 10, "guest_swaps": 12, "dumps": [], "renderer": renderer}


NATIVE_CLEAN = {"native_requested": True, "native_active": True, "fallback": "none", "frames": 10,
                "debug_cvars": [], "enhancements": [{"name": "ts4_sharpening", "on": False}],
                "draw_skips": {}, "gpu_failure": ""}


def test_a_clean_native_run_passes():
    assert native_failures(report_with(NATIVE_CLEAN)) == []


def test_a_renderer_fallback_fails_a_native_run():
    renderer = dict(NATIVE_CLEAN, native_active=False, fallback="missing_shader_pack")
    assert native_failures(report_with(renderer)) == [
        "Native was not active (renderer fallback: missing_shader_pack)"]


def test_every_draw_skip_debug_cvar_and_enhancement_fails_a_native_run():
    renderer = dict(NATIVE_CLEAN,
                    draw_skips={"no_texture": {"total": 3, "first_frame": 7, "first_key": "00ab"}},
                    debug_cvars=["ts4_native_debug_no_blend"],
                    enhancements=[{"name": "ts4_sharpening", "on": True}])
    assert native_failures(report_with(renderer)) == [
        "draw skip no_texture: 3 from frame 7 (key 00ab)",
        "debug cvar ts4_native_debug_no_blend is set",
        "Enhancement ts4_sharpening is on"]


def test_an_enhancement_on_fails_a_classic_run_too():
    # Anisotropic filtering and sharpening work on Classic as well.
    renderer = dict(NATIVE_CLEAN, native_requested=False, native_active=False,
                    enhancements=[{"name": "ts4_anisotropic_filtering", "on": True}])
    assert enhancement_failures(report_with(renderer)) == [
        "Enhancement ts4_anisotropic_filtering is on"]


def test_an_enhancement_asked_for_must_be_on_and_others_still_fail():
    renderer = dict(NATIVE_CLEAN, enhancements=[
        {"name": "ts4_resolution_scale", "on": True}, {"name": "ts4_msaa", "on": False},
        {"name": "ts4_sharpening", "on": True}])
    asked = {"ts4_resolution_scale": "2", "ts4_msaa": "4"}
    assert enhancement_failures(report_with(renderer), asked) == [
        "Enhancement ts4_msaa was asked for (4) but is not on",
        "Enhancement ts4_sharpening is on"]


def test_enhancements_parse_from_key_value_pairs():
    assert parse_enhancements(["ts4_resolution_scale=2", "ts4_msaa=8"]) == {
        "ts4_resolution_scale": "2", "ts4_msaa": "8"}
    with pytest.raises(SceneError):
        parse_enhancements(["ts4_msaa"])


def test_a_scaled_frame_is_box_shrunk_to_the_reference_size():
    big = np.zeros((4, 6, 3), np.uint8)
    big[:2, :2] = 200  # one 2x2 block
    small = shrink_to(big, (2, 3))
    assert small.shape == (2, 3, 3)
    assert (small[0, 0] == 200).all() and (small[1, 1] == 0).all()
    assert shrink_to(small, (2, 3)) is small


def test_an_enhanced_frame_may_differ_along_edges_but_not_over_an_area():
    check = dict(DEFAULTS, tile=8, tile_hot_max=4)
    reference = np.zeros((32, 32, 3), np.float32)
    reference[:, 16:] = 1.0  # one vertical edge
    edge_only = np.zeros((32, 32), np.float32)
    edge_only[:, 15:17] = 1.0  # every pixel along the edge differs
    assert judge_enhanced(edge_only, reference, check)["passed"]
    area = np.zeros((32, 32), np.float32)
    area[2:10, 2:10] = 1.0  # a block away from any edge: a missing glow
    verdict = judge_enhanced(area, reference, check)
    assert not verdict["passed"]
    assert verdict["failing_tiles"]


def test_only_native_only_enhancements_may_differ_along_edges():
    # Anisotropic filtering runs on both renderers, and sharpening acts in the presenter after the
    # dumps: neither changes the comparison, so the strict judge applies.
    assert not edges_may_differ({})
    assert not edges_may_differ({"ts4_anisotropic_filtering": "16"})
    assert not edges_may_differ({"ts4_anisotropic_filtering": "16", "ts4_sharpening": "high"})
    # A Resolution scale or MSAA moves edges, and so does any enhancement not known to be shared.
    assert edges_may_differ({"ts4_resolution_scale": "2"})
    assert edges_may_differ({"ts4_anisotropic_filtering": "16", "ts4_msaa": "4"})
    assert edges_may_differ({"ts4_native_color_mode": "natural"})


def test_a_report_without_a_renderer_object_fails_a_native_run():
    assert native_failures({"exit_after_frame": 1, "guest_swaps": 2, "dumps": []}) == [
        "the session report has no renderer object"]


def test_native_is_judged_against_the_closest_classic_run_per_pixel():
    a = np.zeros((2, 2, 3), np.float32)
    b = np.ones((2, 2, 3), np.float32)
    test = a.copy()
    test[0, 0] = 1.0  # matches b there, a elsewhere

    def error(reference, image):
        return np.abs(reference - image).mean(axis=2)

    assert envelope_error([a, b], test, error).max() == 0.0


def test_the_presenter_ramp_is_read_off_pairs_and_fills_gaps():
    own = np.array([[[0, 0, 0], [128, 128, 128], [255, 255, 255]]], np.uint8)
    shown = np.array([[[0, 0, 0], [115, 115, 115], [255, 255, 255]]], np.uint8)
    ramp = fit_ramp([(own, shown)])
    assert ramp[0][128] == 115 and ramp[2][255] == 255
    assert 0 < ramp[1][64] < 115  # interpolated between the values seen
    assert (apply_ramp(own, ramp) == shown).all()


def test_a_full_scene_plays_every_run_to_its_last_capture():
    plan = plan_runs([350, 1300], classic_runs=2, native_runs=1, dual_runs=1)
    assert plan == [("run-1", [350, 1300]), ("run-2", [350, 1300]),
                    ("native-1", [350, 1300]), ("dual-1", [350, 1300])]


def test_a_dual_scene_without_classic_runs_reads_the_ramp_off_the_first_capture_alone():
    plan = plan_runs([350, 1300], classic_runs=0, native_runs=1, dual_runs=1)
    assert plan == [("native-1", [350]), ("dual-1", [350, 1300])]


@pytest.mark.parametrize("args", [["--runs", "0"], ["--runs", "0", "--native", "1"]])
def test_no_classic_runs_needs_the_dual_runs_to_decide(tmp_path, args):
    from scene_runner import main
    scene = tmp_path / "scene.toml"
    scene.write_text(GOOD)
    # An existing --exe, so only the refusal can stop it before a run.
    assert main([str(scene), "--exe", str(scene), "--out", str(tmp_path / "out"), *args]) == 2


def test_a_presenter_image_that_is_no_ramp_of_natives_is_refused():
    own = np.array([[[10, 10, 10], [10, 10, 10]]], np.uint8)
    shown = np.array([[[0, 0, 0], [200, 200, 200]]], np.uint8)
    with pytest.raises(SceneError):
        fit_ramp([(own, shown)])
