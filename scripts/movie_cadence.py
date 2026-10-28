"""Movie cadence of a run: does each picture of a 30 fps movie last exactly two 60 Hz swaps?

Reads the run's --ts4_frame_hash_csv (src/frame_dumps.h: a sampled hash of every shown frame)
and finds the movie as the longest stretch where the picture changes at least every 4 swaps.
Each change should come two swaps after the last; a change one or three swaps after shows a
picture for 16 or 50 ms, the judder docs/research/movie-cadence.md describes. A run passes with
at most 2 % of changes off that step.

    topspin4.exe ... --ts4_frame_hash_csv=<run>/frame_hash.csv --ts4_exit_after_frame=720
    python scripts/movie_cadence.py <run>/frame_hash.csv
"""
import csv
import sys
from collections import Counter

MOVIE_MAX_GAP = 4
OFF_STEP_LIMIT = 0.02


def load(path):
    with open(path, newline="", encoding="utf-8") as file:
        return [(int(r["frame"]), float(r["ms"]), r["hash"]) for r in csv.DictReader(file)]


def movie_changes(frames):
    """The picture changes of the longest stretch with no gap above MOVIE_MAX_GAP swaps."""
    changes = [frames[i] for i in range(1, len(frames)) if frames[i][2] != frames[i - 1][2]]
    best, current = [], []
    for change in changes:
        if current and change[0] - current[-1][0] > MOVIE_MAX_GAP:
            best, current = max(best, current, key=len), []
        current.append(change)
    return max(best, current, key=len)


def judge(frames):
    changes = movie_changes(frames)
    gaps = [b[0] - a[0] for a, b in zip(changes, changes[1:])]
    off = sum(1 for gap in gaps if gap != 2)
    return changes, gaps, off, off <= len(gaps) * OFF_STEP_LIMIT


def main():
    frames = load(sys.argv[1])
    changes, gaps, off, passed = judge(frames)
    if not changes:
        print("no movie found")
        return 1
    print(f"movie: swaps {changes[0][0]}-{changes[-1][0]} "
          f"({changes[0][1] / 1000:.2f}-{changes[-1][1] / 1000:.2f} s), {len(changes)} changes")
    print("gaps:", "".join(str(min(gap, 9)) for gap in gaps))
    print("gap counts:", dict(sorted(Counter(gaps).items())))
    print(f"{off} of {len(gaps)} changes off the 2-swap step: {'PASS' if passed else 'FAIL'}")
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
