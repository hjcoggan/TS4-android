"""Find guest values that advance by a steady step per frame, from --ts4_memdump_frames dumps.

Each dump (src/frame_dumps.cpp) is mem-NNNNNN.bin, records of {u32 guest address, u32 length,
bytes} with a big-endian header, and mem-NNNNNN.txt with the guest swap count before and after
it was written. The game runs on while a dump is written, so a change is divided by the swaps
between the dumps' midpoints. Every aligned 4-byte word is read as a big-endian float and as a
big-endian integer; a word is a clock when it changed by the same step per swap between each
pair of consecutive dumps (within --tolerance), and the steps are grouped and printed with
their addresses: a cutaway's timeline, animation and camera clocks advance this way, and one
that counts frames steps by 1 (integer) or a constant like 1/30 (float).

    topspin4.exe ... --ts4_memdump_frames=3400,3410,3420 --ts4_dump_dir=<dir>
    python scripts/memdiff.py <dir>/mem-003400 <dir>/mem-003410 <dir>/mem-003420 [--top 40]
"""
import argparse
from collections import defaultdict
from pathlib import Path

import numpy as np


def load(stem):
    stem = Path(stem)
    side = dict(line.split() for line in stem.with_suffix(".txt").read_text().splitlines())
    swaps = (int(side["swaps_before"]) + int(side["swaps_after"])) / 2
    data = stem.with_suffix(".bin").read_bytes()
    regions, at = {}, 0
    while at + 8 <= len(data):
        address = int.from_bytes(data[at:at + 4], "big")
        length = int.from_bytes(data[at + 4:at + 8], "big")
        regions[address] = data[at + 8:at + 8 + length]
        at += 8 + length
    return swaps, regions


def steady_words(dumps, tolerance):
    """{(kind, rounded step): [guest address]} of words that step evenly per swap."""
    found = defaultdict(list)
    first = dumps[0][1]
    for address, block in first.items():
        blocks = [d[1].get(address) for d in dumps]
        if any(b is None or len(b) != len(block) for b in blocks):
            continue
        n = len(block) // 4 * 4
        if n == 0:
            continue
        swaps = [d[0] for d in dumps]
        gaps = np.diff(swaps)
        if np.any(gaps <= 0):
            continue
        ints = np.stack([np.frombuffer(b[:n], ">i4").astype(np.int64) for b in blocks])
        floats = np.stack([np.frombuffer(b[:n], ">f4").astype(np.float64) for b in blocks])
        # Integers: the same whole-number change per swap in every interval.
        int_steps = np.diff(ints, axis=0) / gaps[:, None]
        int_ok = np.all(np.abs(int_steps - int_steps[0]) <= tolerance * np.abs(int_steps[0]),
                        axis=0)
        int_ok &= (int_steps[0] != 0) & (np.abs(int_steps[0]) <= 4)
        for i in np.nonzero(int_ok)[0]:
            found[("int", round(float(int_steps[0][i]), 3))].append(address + 4 * int(i))
        # Floats: finite, a small non-zero step, the same in every interval.
        with np.errstate(invalid="ignore", over="ignore"):
            float_steps = np.diff(floats, axis=0) / gaps[:, None]
            finite = np.all(np.isfinite(floats), axis=0) & np.all(np.abs(floats) < 1e7, axis=0)
            step = float_steps[0]
            float_ok = finite & (np.abs(step) > 1e-5) & (np.abs(step) < 10)
            float_ok &= np.all(np.abs(float_steps - step) <= tolerance * np.abs(step) + 1e-7,
                               axis=0)
        float_ok &= ~int_ok
        for i in np.nonzero(float_ok)[0]:
            found[("float", round(float(step[i]), 5))].append(address + 4 * int(i))
    return found


def main():
    ap = argparse.ArgumentParser(description="Find guest values that advance by a steady step per frame.")
    ap.add_argument("dumps", nargs="+", help="dump stems (mem-NNNNNN, without .bin/.txt), in order")
    ap.add_argument("--tolerance", type=float, default=0.02, help="relative step agreement")
    ap.add_argument("--top", type=int, default=40, help="step groups to print")
    args = ap.parse_args()
    if len(args.dumps) < 3:
        ap.error("give at least three dumps, so a steady step can be told from a single change")
    dumps = [load(stem) for stem in args.dumps]
    print("swaps at dumps:", [round(d[0], 1) for d in dumps])
    found = steady_words(dumps, args.tolerance)
    groups = sorted(found.items(), key=lambda item: -len(item[1]))[:args.top]
    for (kind, step), addresses in groups:
        shown = " ".join(f"{a:08x}" for a in addresses[:8])
        more = f" (+{len(addresses) - 8})" if len(addresses) > 8 else ""
        print(f"{kind:5} step {step:>10} per swap: {len(addresses):5} words  {shown}{more}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
