"""Guest frame rate on the Oracle, from Xenia Edge's log.

`scripts/edge.ps1 -FrameLog` makes Edge log every VdSwap and records, each time it polls the log,
the wall seconds, the VdSwap calls so far and the newest log line in frames.tsv. This turns the
swap count into fps for each second. Edge's log lines carry no time, but each starts with Edge's
frame counter (`i> f:0038798`); its rise over the run is printed beside the swaps as a check.

    python scripts/edge_fps.py <frames.tsv> [--out fps.csv]
"""
import argparse
import re
import statistics
import sys
from pathlib import Path

FRAME_PREFIX = re.compile(r"^.> f:(\d+) ")
DROPPED = 55  # fps below this counts as a dropped second
HALVED = (25, 35)  # fps in this band reads as the game presenting every other vblank


def frame_of(line):
    """Edge's frame counter at the start of one of its log lines, or None."""
    m = FRAME_PREFIX.match(line)
    return int(m.group(1)) if m else None


def per_second(samples):
    """[(second, fps)] from time-ordered (wall seconds, frame count) samples.

    Second s runs from the first sample at or after s to the first at or after s + 1, so a late
    poll stretches the window rather than skewing the rate. A second with no sample in it is
    left out."""
    rows = []
    if not samples:
        return rows
    i = 0
    for s in range(int(samples[0][0]), int(samples[-1][0])):
        while samples[i][0] < s:
            i += 1
        j = i
        while j < len(samples) and samples[j][0] < s + 1:
            j += 1
        if j == len(samples):
            break
        (ta, fa), (tb, fb) = samples[i], samples[j]
        if ta < s + 1:
            rows.append((s, (fb - fa) / (tb - ta)))
    return rows


def read_samples(path):
    """(wall seconds, swaps) and (wall seconds, Edge frame) samples from frames.tsv lines of
    `seconds<TAB>swaps<TAB>log line`."""
    swaps, frames = [], []
    for row in Path(path).read_text(encoding="utf-8", errors="replace").splitlines():
        seconds, count, line = row.split("\t", 2)
        swaps.append((float(seconds), int(count)))
        frame = frame_of(line)
        if frame is not None:
            frames.append((float(seconds), frame))
    return swaps, frames


def main():
    ap = argparse.ArgumentParser(description="Guest frame rate on the Oracle, from Edge's log.")
    ap.add_argument("frames")
    ap.add_argument("--out")
    args = ap.parse_args()
    swaps, frames = read_samples(args.frames)
    rows = per_second(swaps)
    if not rows:
        sys.exit(f"{args.frames}: no frame samples")
    out = Path(args.out or Path(args.frames).with_name("fps.csv"))
    out.write_text("second,fps\n" + "".join(f"{s},{fps:.1f}\n" for s, fps in rows), encoding="utf-8")
    rates = [fps for _, fps in rows]
    print(f"{len(rows)} s: median {statistics.median(rates):.1f}, min {min(rates):.1f}, "
          f"max {max(rates):.1f}; below {DROPPED} fps: {sum(r < DROPPED for r in rates)} s, "
          f"{HALVED[0]}-{HALVED[1]} fps: {sum(HALVED[0] <= r <= HALVED[1] for r in rates)} s; "
          f"{swaps[-1][1] - swaps[0][1]} swaps against an Edge frame counter rise of "
          f"{frames[-1][1] - frames[0][1]} -> {out}")


if __name__ == "__main__":
    main()
