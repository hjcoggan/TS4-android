"""Write analysis/d3d-sweep.csv: a counted hook on every function of a Module's D3D copy that
symbols/d3d-functions.csv does not already hook, for a sweep build (-DTOPSPIN4_D3D_SWEEP=ON).
With --ts4_d3d_census the census then shows every D3D function's calls, which finds the entry
points the table is missing. Adapted from WWE All Stars' gen_d3d_sweep.py (ADR-0004).

Usage: python scripts/gen_d3d_sweep.py <module> <lo> <hi>   (module: loader or swing)
       e.g. swing 88286000 882AF000
"""
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NAMES = {"loader": "Loader_DLL.xex", "swing": "Swing_DLL.xex"}

module, lo, hi = sys.argv[1], int(sys.argv[2], 16), int(sys.argv[3], 16)
init = (ROOT / f"generated/{module}/topspin4_init.cpp").read_text(errors="replace")
starts = sorted({int(a, 16) for a in re.findall(r"\{ (0x8[0-9A-F]{7}), sub_[0-9A-F]+ \}", init)})
with open(ROOT / "symbols/d3d-functions.csv", newline="") as f:
    hooked = {int(r["address"], 16) for r in csv.DictReader(f) if r["module"] == NAMES[module]}
rows = [a for a in starts if lo <= a < hi and a not in hooked]
out = ROOT / "analysis/d3d-sweep.csv"
with open(out, "w", newline="") as f:
    f.write("module,address,name,role,evidence\n")
    for a in rows:
        f.write(f"{NAMES[module]},{a:08x},Sweep_{a:08X},census,sweep\n")
print(f"{len(rows)} functions -> {out}")
