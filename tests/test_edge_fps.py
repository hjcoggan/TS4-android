"""scripts/edge_fps.py's reading of the guest frame counter from Xenia Edge's log, and its
per-second frame rate from wall-clock samples of that counter."""

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))

from edge_fps import frame_of, per_second  # noqa: E402


def test_the_frame_counter_is_read_from_a_log_line_prefix():
    assert frame_of("i> f:0038798 000059D8 Cheap-skate exit!") == 38798
    assert frame_of("F> f:0000000 01000028 DiscImageDevice::ResolvePath(\\Loader_DLL.xex)") == 0


def test_lines_without_a_frame_counter_have_none():
    assert frame_of("   F 830006BC 83517490 25B ( 603)    VdSwap") is None
    assert frame_of("Module Hash: CD4DFB48F55DBDB5") is None
    assert frame_of("") is None


def test_each_second_is_the_frames_between_the_first_samples_at_or_after_its_edges():
    # 60 fps for second 0 (polled every 0.1 s), then 30 fps for second 1.
    samples = [(t / 10, t * 6) for t in range(11)] + [(1.5, 75), (2.0, 90)]
    assert per_second(samples) == [(0, pytest.approx(60)), (1, pytest.approx(30))]


def test_a_rate_is_over_the_samples_own_times_not_the_whole_second():
    # The poller saw the log late at 1.25 s: 75 frames over 1.25 s is still 60 fps.
    assert per_second([(0.0, 0), (1.25, 75), (2.0, 120)]) == [(0, pytest.approx(60)), (1, pytest.approx(60))]


def test_a_second_with_no_sample_in_it_is_left_out():
    # A screenshot stalled the poller from 0.5 s to 2.5 s.
    rows = per_second([(0.0, 0), (0.5, 30), (2.5, 150), (3.0, 180)])
    assert [s for s, _ in rows] == [0, 2]
    assert rows[1][1] == pytest.approx(60)
