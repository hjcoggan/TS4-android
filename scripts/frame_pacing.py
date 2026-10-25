"""Frame pacing of a port run against WWE All Stars' pacing bar (ticket 16).

Two logs give frame times without PresentMon. The presenter's swap log (--present_pacing_log,
the SDK's paced presentation) has one row per guest frame at the moment it was presented on a
paced vblank, and counts the guest frames replaced before they were shown: this is what reaches
the screen, and what the bar is judged on. --ts4_frame_times_csv (src/frame_dumps.h) has the time
each guest swap was processed on the GPU thread, before pacing, for comparison. Each interval
between frames is judged as in WWE's scripts/analyze_hitches.py:
- on a step: within 1 ms of the game's 60 Hz step (16.7 ms), or of its own 30 Hz step (33.3 ms)
  inside a run of at least MIN_30HZ_RUN such frames: Top Spin 4 presents its cutaways at an even
  30 Hz for a second or more (docs/research/oracle-baseline.md), where WWE flags such windows
  with --allow-30hz. A lone 33.3 ms frame in 60 Hz play is a held frame;
- a hitch: longer than 25 ms and not a cutaway step (a game frame held or skipped);
- the 1 % low: the frame time only the slowest 1 % of the 60 Hz frames exceed, against WWE's
  match bar of 16.7 ms.
- a replaced frame: a guest frame the presenter dropped unshown (a skip, in WWE's judder bar).
A run passes with no hitch, no replaced frame, every frame on a step, and that 1 % low.
--from/--to (seconds on the log's own clock, which starts with the game) judge a window, such as
the match, instead of the whole run with its loads.

    python scripts/frame_pacing.py <present_pacing.csv | frame_times.csv> [--from S] [--to S]
"""
import argparse
import csv
import math
import sys
from dataclasses import dataclass, field

STEP_60_MS = 1000 / 60
STEP_30_MS = 1000 / 30
ON_STEP_SLACK_MS = 1.0
HITCH_MS = 25.0
ONE_PERCENT_LOW_BAR_MS = 16.7
MIN_30HZ_RUN = 6  # 0.2 s; the shortest cutaway measured on the Oracle lasted 1 s


@dataclass
class Verdict:
    frames: int
    hitches: list = field(default_factory=list)  # (time ms, interval ms)
    uneven: int = 0
    at_30hz: int = 0
    one_percent_low: float = 0.0
    worst: float = 0.0
    replaced: int = 0

    @property
    def passed(self):
        return (not self.hitches and self.uneven == 0 and self.replaced == 0
                and self.one_percent_low <= ONE_PERCENT_LOW_BAR_MS + 1e-9)


def load_frame_times(path):
    """The shown-frame times in ms, in order."""
    with open(path, newline="", encoding="utf-8") as file:
        return [float(row["ms"]) for row in csv.DictReader(file)]


def load_present_log(path):
    """The presenter's swap log: present times in ms, and (time ms, count) of replaced frames."""
    times, replaced = [], []
    with open(path, newline="", encoding="utf-8") as file:
        for row in csv.DictReader(file):
            at = int(row["present_us"]) / 1000
            times.append(at)
            if int(row["frames_replaced"] or 0):
                replaced.append((at, int(row["frames_replaced"])))
    return times, replaced


def load_times(path):
    """Present times and replaced frames from either log, told apart by the header."""
    with open(path, encoding="utf-8") as file:
        header = file.readline()
    if header.startswith("present_us"):
        return load_present_log(path)
    return load_frame_times(path), []


def judge(times_ms, from_s=None, to_s=None, replaced=()):
    """The verdict on the frames shown in [from_s, to_s] (seconds; the whole run by default)."""
    shown = [t for t in times_ms
             if (from_s is None or t >= from_s * 1000) and (to_s is None or t <= to_s * 1000)]
    intervals = [(now, now - previous) for previous, now in zip(shown, shown[1:])]
    on_30 = [abs(ms - STEP_30_MS) <= ON_STEP_SLACK_MS for _, ms in intervals]
    # A 30 Hz frame is a cutaway step only inside a long enough run of them.
    cutaway = [False] * len(intervals)
    start = 0
    while start < len(intervals):
        end = start
        while end < len(intervals) and on_30[end]:
            end += 1
        if end - start >= MIN_30HZ_RUN:
            cutaway[start:end] = [True] * (end - start)
        start = max(end, start + 1)
    verdict = Verdict(frames=len(shown))
    if shown:
        verdict.replaced = sum(count for at, count in replaced if shown[0] <= at <= shown[-1])
    at_60 = []
    for (now, ms), is_cutaway in zip(intervals, cutaway):
        if is_cutaway:
            verdict.at_30hz += 1
        elif ms > HITCH_MS:
            verdict.hitches.append((now, ms))
        elif abs(ms - STEP_60_MS) > ON_STEP_SLACK_MS:
            verdict.uneven += 1
        if not is_cutaway:
            at_60.append(ms)
        verdict.worst = max(verdict.worst, ms)
    if at_60:
        ordered = sorted(at_60)
        verdict.one_percent_low = ordered[max(math.ceil(0.99 * len(ordered)), 1) - 1]
    return verdict


def main():
    ap = argparse.ArgumentParser(description="Frame pacing of a port run against WWE's bar.")
    ap.add_argument("frame_times", help="present_pacing.csv or frame_times.csv of a run")
    ap.add_argument("--from", dest="from_s", type=float, help="window start, seconds")
    ap.add_argument("--to", dest="to_s", type=float, help="window end, seconds")
    args = ap.parse_args()
    times, replaced = load_times(args.frame_times)
    verdict = judge(times, args.from_s, args.to_s, replaced)
    print(f"{verdict.frames} frames, {verdict.at_30hz} at the game's 30 Hz step; "
          f"1 % low {verdict.one_percent_low:.2f} ms, worst {verdict.worst:.2f} ms; "
          f"uneven {verdict.uneven}, hitches {len(verdict.hitches)}, "
          f"replaced {verdict.replaced}: "
          f"{'PASS' if verdict.passed else 'FAIL'}")
    for at, ms in verdict.hitches[:20]:
        print(f"  hitch at {at / 1000:.3f} s: {ms:.1f} ms")
    sys.exit(0 if verdict.passed else 1)


if __name__ == "__main__":
    main()
