"""Match functions of a reference recomp (WWE All Stars' XDK D3D library) into a TU1 Module.

Each function is reduced to a masked signature: the top 16 bits of every instruction (opcode
and registers), with branch displacements and immediates dropped, since relocated addresses
differ between the two links. A reference function matches when exactly one function of the
same length here has an identical signature ("unique"). The rest get the most similar
function within 25% of their length, by the share of signature words in common, in order
("near:<percent>"), which survives the small changes between XDK versions (WWE All Stars
links XDK 2.0.11775, Top Spin 4 2.0.20209). Copied from WWE All Stars (ADR-0004) and widened
to near matches.

Usage: python scripts/match_functions.py <ref.pe> <ref_base> <ref_init.cpp> <lo> <hi> <module>
       lo/hi: hex bounds of the reference range; module: loader or swing
Writes analysis/function-matches-<module>.tsv: ref, ours, size, status.
"""
import difflib
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULES = {"loader": ("Loader_DLL", 0x83000000), "swing": ("Swing_DLL", 0x88000000)}


def starts(init_path):
    text = Path(init_path).read_text(errors="replace")
    return sorted({int(a, 16) for a in re.findall(r"\{ (0x8[0-9A-F]{7}), sub_[0-9A-F]+ \}", text)})


def signature(image, base, start, end):
    words = []
    for address in range(start, end, 4):
        insn = int.from_bytes(image[address - base:address - base + 4], "big")
        opcode = insn >> 26
        if opcode in (16, 18):  # bc, b: keep opcode, BO/BI and link bits
            words.append((insn & 0xFFFF0003) if opcode == 16 else (insn & 0xFC000003))
        else:
            words.append(insn & 0xFFFF0000)
    return tuple(words)


def spans(addresses):
    return list(zip(addresses, addresses[1:]))


def main():
    ref_image = Path(sys.argv[1]).read_bytes()
    ref_base = int(sys.argv[2], 16)
    ref_starts = starts(sys.argv[3])
    lo, hi = int(sys.argv[4], 16), int(sys.argv[5], 16)
    module = sys.argv[6]
    name, base = MODULES[module]
    image = (ROOT / f"analysis/xex/tu1/{name}.pe").read_bytes()
    ours = starts(ROOT / f"generated/{module}/topspin4_init.cpp")

    by_signature = defaultdict(list)
    by_length = defaultdict(list)
    for a, b in spans(ours):
        if b - a <= 0x4000:
            s = signature(image, base, a, b)
            by_signature[s].append(a)
            by_length[len(s)].append((a, s))

    rows, unique, near = [], 0, 0
    for a, b in spans(ref_starts):
        if not (lo <= a < hi) or b - a > 0x4000:
            continue
        ref = signature(ref_image, ref_base, a, b)
        hits = by_signature.get(ref, [])
        if len(hits) == 1:
            unique += 1
            rows.append(f"{a:08X}\t{hits[0]:08X}\t{b - a:X}\tunique")
            continue
        best, best_ratio = 0, 0.0
        if len(ref) >= 6:
            for n in range(int(len(ref) * 0.75), int(len(ref) * 1.25) + 1):
                for ours_address, s in by_length.get(n, []):
                    matcher = difflib.SequenceMatcher(None, ref, s, autojunk=False)
                    if matcher.real_quick_ratio() <= best_ratio or matcher.quick_ratio() <= best_ratio:
                        continue
                    ratio = matcher.ratio()
                    if ratio > best_ratio:
                        best, best_ratio = ours_address, ratio
        if best and best_ratio >= 0.6:
            near += 1
            rows.append(f"{a:08X}\t{best:08X}\t{b - a:X}\tnear:{round(best_ratio * 100)}")
        else:
            status = "none" if not hits else f"ambiguous:{len(hits)}"
            rows.append(f"{a:08X}\t{0:08X}\t{b - a:X}\t{status}")
    out = ROOT / f"analysis/function-matches-{module}.tsv"
    out.write_text("ref\tours\tsize\tstatus\n" + "\n".join(rows) + "\n")
    print(f"{len(rows)} reference functions, {unique} unique, {near} near -> {out}")


if __name__ == "__main__":
    main()
