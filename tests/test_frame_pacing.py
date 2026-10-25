"""scripts/frame_pacing.py: the port's shown-frame times (--ts4_frame_times_csv) judged against
WWE All Stars' pacing bar (ticket 16), with Top Spin 4's own 30 fps cutaways allowed."""

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))

from frame_pacing import judge, load_frame_times, load_present_log  # noqa: E402


def times(intervals_ms, start=1000.0):
    out, at = [start], start
    for ms in intervals_ms:
        at += ms
        out.append(at)
    return out


def test_a_steady_60_passes_with_no_hitch():
    verdict = judge(times([1000 / 60] * 600))
    assert verdict.hitches == [] and verdict.uneven == 0
    assert verdict.one_percent_low == pytest.approx(1000 / 60)
    assert verdict.passed


def test_a_long_frame_is_a_hitch():
    verdict = judge(times([1000 / 60] * 100 + [40.0] + [1000 / 60] * 100))
    assert [round(ms, 1) for _, ms in verdict.hitches] == [40.0]
    assert not verdict.passed


def test_an_even_30_hz_cutaway_is_the_game_s_own_step_not_a_hitch():
    verdict = judge(times([1000 / 60] * 100 + [1000 / 30] * 60 + [1000 / 60] * 100))
    assert verdict.hitches == [] and verdict.uneven == 0
    assert verdict.at_30hz == 60
    # The 1 % low is over the 60 Hz frames, so the cutaway does not count against it.
    assert verdict.one_percent_low == pytest.approx(1000 / 60)
    assert verdict.passed


def test_a_frame_between_the_two_steps_is_a_hitch():
    verdict = judge(times([1000 / 60] * 100 + [28.0] + [1000 / 60] * 100))
    assert len(verdict.hitches) == 1
    assert not verdict.passed


def test_a_frame_off_step_by_more_than_a_millisecond_is_uneven():
    verdict = judge(times([1000 / 60] * 100 + [19.0, 14.3] + [1000 / 60] * 100))
    assert verdict.hitches == []
    assert verdict.uneven == 2
    assert not verdict.passed


def test_frame_times_load_from_the_csv(tmp_path):
    path = tmp_path / "frame_times.csv"
    path.write_text("frame,ms\n1,1000.000\n2,1016.700\n3,1033.300\n", encoding="utf-8")
    assert load_frame_times(path) == pytest.approx([1000.0, 1016.7, 1033.3])


def test_a_lone_33_ms_frame_in_60_hz_play_is_a_dropped_frame_not_a_cutaway():
    verdict = judge(times([1000 / 60] * 100 + [1000 / 30] + [1000 / 60] * 100))
    assert len(verdict.hitches) == 1
    assert verdict.at_30hz == 0
    assert not verdict.passed


def test_a_run_flipping_between_60_and_30_is_hitching_not_a_cutaway():
    verdict = judge(times([1000 / 60, 1000 / 30] * 50))
    assert len(verdict.hitches) == 50
    assert not verdict.passed


def test_a_window_judges_only_the_frames_inside_it():
    # A load stall at the start, then a clean match from 2 s on.
    run = times([300.0] + [1000 / 60] * 600, start=0.0)
    assert not judge(run).passed
    assert judge(run, from_s=2.0).passed


SWAP_LOG_HEADER = ("present_us,interval_us,repeated_vblanks,arrival_after_vblank_us,latency_us,"
                   "guest_vblank,frames_replaced,external_sequence,"
                   "present_id,wait_us,unshown,shown_id,shown_us,shown_after_present_us\n")


def test_the_presenter_s_swap_log_loads_as_present_times_and_replaced_frames(tmp_path):
    path = tmp_path / "present_pacing.csv"
    path.write_text(SWAP_LOG_HEADER
                    + "1000,0,0,500,4000,60,0,0,1,0,0,,,\n"
                    + "17667,16667,0,480,4100,61,2,0,2,0,0,1,1500,500\n", encoding="utf-8")
    times, replaced = load_present_log(path)
    assert times == pytest.approx([1.0, 17.667])
    assert replaced == [(17.667, 2)]


def test_frames_the_presenter_replaced_unshown_fail_the_bar():
    verdict = judge(times([1000 / 60] * 200), replaced=[(1500.0, 1)])
    assert verdict.replaced == 1
    assert not verdict.passed
    assert judge(times([1000 / 60] * 200), replaced=[(99999.0, 1)]).passed  # outside the window
