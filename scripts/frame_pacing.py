"""Frame pacing of a port run against WWE All Stars' pacing bar (ticket 16).

The port writes the host time of every frame it shows with --ts4_frame_times_csv (src/frame_dumps.h),
measured on the GPU thread as each guest swap is processed, which needs no PresentMon. Each
interval between frames is judged as in WWE's scripts/analyze_hitches.py:
- on a step: within 1 ms of the game's 60 Hz step (16.7 ms) or of its own 30 Hz step (33.3 ms),
  which Top Spin 4 presents in its cutaways (docs/research/oracle-baseline.md);
- a hitch: longer than 25 ms and not on the 30 Hz step (a game frame held or skipped);
- the 1 % low: the frame time only the slowest 1 % of the 60 Hz frames exceed, against WWE's
  match bar of 16.7 ms.
A run passes with no hitch, every frame on a step, and that 1 % low. The times are when frames
reach the presenter, not when they reach the screen, so display-side judder is not seen.

    python scripts/frame_pacing.py <frame_times.csv>
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


@dataclass
class Verdict:
    frames: int
    hitches: list = field(default_factory=list)  # (time ms, interval ms)
    uneven: int = 0
    at_30hz: int = 0
    one_percent_low: float = 0.0
    worst: float = 0.0

    @property
    def passed(self):
        return (not self.hitches and self.uneven == 0
                and self.one_percent_low <= ONE_PERCENT_LOW_BAR_MS + 1e-9)


def load_frame_times(path):
    """The shown-frame times in ms, in order."""
    with open(path, newline="", encoding="utf-8") as file:
        return [float(row["ms"]) for row in csv.DictReader(file)]


def judge(times_ms):
    verdict = Verdict(frames=len(times_ms))
    at_60 = []
    for previous, now in zip(times_ms, times_ms[1:]):
        ms = now - previous
        on_60 = abs(ms - STEP_60_MS) <= ON_STEP_SLACK_MS
        on_30 = abs(ms - STEP_30_MS) <= ON_STEP_SLACK_MS
        if on_30:
            verdict.at_30hz += 1
        elif ms > HITCH_MS:
            verdict.hitches.append((now, ms))
        elif not on_60:
            verdict.uneven += 1
        if not on_30:
            at_60.append(ms)
        verdict.worst = max(verdict.worst, ms)
    if at_60:
        ordered = sorted(at_60)
        verdict.one_percent_low = ordered[max(math.ceil(0.99 * len(ordered)), 1) - 1]
    return verdict


def main():
    ap = argparse.ArgumentParser(description="Frame pacing of a port run against WWE's bar.")
    ap.add_argument("frame_times")
    args = ap.parse_args()
    verdict = judge(load_frame_times(args.frame_times))
    print(f"{verdict.frames} frames, {verdict.at_30hz} at the game's 30 Hz step; "
          f"1 % low {verdict.one_percent_low:.2f} ms, worst {verdict.worst:.2f} ms; "
          f"uneven {verdict.uneven}, hitches {len(verdict.hitches)}: "
          f"{'PASS' if verdict.passed else 'FAIL'}")
    for at, ms in verdict.hitches[:20]:
        print(f"  hitch at {at / 1000:.3f} s: {ms:.1f} ms")
    sys.exit(0 if verdict.passed else 1)


if __name__ == "__main__":
    main()
