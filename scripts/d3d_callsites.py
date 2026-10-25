"""Check the D3D hooks' argument registers against the game's own call sites.

Each renderer row of symbols/d3d-functions.csv names a hook body in src/d3d/hooks.h
(TS4_D3D_HOOK_<name>), which reads arguments as ctx.rN before the original runs. This finds
every direct call of the row's address in the Module's generated code (generated/<module>) and
the last instruction before it that sets each of those registers. A register is flagged when no
caller sets it nearby, or when every caller sets it to the same literal: both mean the hook
probably reads the wrong register. Top Spin 4's XDK is not WWE's, and LTCG specialises some
functions per Module (Resolve's destination is r6, not WWE's r8; Swing's DrawVertices takes no
start vertex), so run this after adding or changing a renderer row.

    python scripts/d3d_callsites.py [--verbose] [name ...]

Exit code: 0 no flags, 1 flags, 2 the generated code or the table is missing.
"""
import argparse
import csv
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULES = {"Loader_DLL.xex": "loader", "Swing_DLL.xex": "swing"}
# Look this far back from a call for the instructions that set its arguments.
WINDOW = 40
SET_RE = re.compile(r"^\s*//\s*(\w+)\.?\s+(r\d+|f\d+),\s*(.*)$")
# Arguments a Module's callers legitimately always pass as the same literal.
CONSTANT_OK = {
    ("Resolve", "r8"): "the mip level: 0 in Loader, varied only by Swing's mip chains",
}
CALL_RE = re.compile(r"\bsub_([0-9A-F]{8})\(ctx, base\);")
FUNC_RE = re.compile(r"^(?:DEFINE_REX_FUNC|PPC_FUNC_IMPL)\(sub_([0-9A-F]{8})\)")
# Instructions whose first operand is a register they write.
WRITES = {"mr", "li", "lis", "addi", "addis", "ori", "oris", "lwz", "lhz", "lbz", "ld", "lwzx",
          "rlwinm", "clrlwi", "add", "subf", "mullw", "srwi", "slwi", "extsw", "lfs", "lfd",
          "fmr", "and", "or", "xor", "neg", "lwzu", "andi", "mulli", "srawi", "subfic"}


def hook_registers(hooks_text):
    """name -> argument registers its body reads from ctx before the original (r4 and up; r3
    is the device or object, r1 the stack)."""
    out = {}
    for m in re.finditer(r"#define TS4_D3D_HOOK_(\w+)\(name, address\)(.*?)(?:\n\n|\Z)",
                         hooks_text, re.S):
        body = m.group(2).split("TS4_D3D_CALL", 1)[0]
        regs = sorted(set(re.findall(r"ctx\.(r(?:[4-9]|10)|f1)\.", body)),
                      key=lambda r: (r[0], int(r[1:])))
        out[m.group(1)] = regs
    return out


def call_sites(module_dir, address):
    """(caller function, {register: setting instruction}) for each direct call of address."""
    sites = []
    for path in sorted(module_dir.glob("*_recomp.*.cpp")):
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        function = None
        for i, line in enumerate(lines):
            f = FUNC_RE.match(line)
            if f:
                function = f.group(1)
                continue
            c = CALL_RE.search(line)
            if not c or c.group(1) != address:
                continue
            sets = {}
            # The first "// bl" going back is this call's own; stop at the next one (the previous
            # call: what it leaves in r3-r10 is not ours) or the function start.
            own_call_seen = False
            for back in lines[max(0, i - WINDOW):i][::-1]:
                if FUNC_RE.match(back):
                    break
                if re.match(r"^\s*//\s*bl\s", back):
                    if own_call_seen:
                        break
                    own_call_seen = True
                    continue
                s = SET_RE.match(back)
                if s and s.group(1) in WRITES and s.group(2) not in sets:
                    sets[s.group(2)] = f"{s.group(1)} {s.group(2)},{s.group(3)}"
            sites.append((function, sets))
    return sites


def main(argv):
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n")[0])
    parser.add_argument("names", nargs="*", help="only these rows (default: every renderer row)")
    parser.add_argument("--verbose", action="store_true", help="print every call site")
    args = parser.parse_args(argv)
    table = ROOT / "symbols/d3d-functions.csv"
    hooks = hook_registers((ROOT / "src/d3d/hooks.h").read_text(encoding="utf-8"))
    flags = 0
    with open(table, newline="", encoding="utf-8") as f:
        rows = [r for r in csv.DictReader(f) if r["role"] == "renderer"]
    for row in rows:
        name, module = row["name"], MODULES.get(row["module"])
        if args.names and name not in args.names:
            continue
        if module is None:
            print(f"{table}: unknown module {row['module']}", file=sys.stderr)
            return 2
        module_dir = ROOT / "generated" / module
        if not module_dir.exists():
            print(f"no generated code at {module_dir}", file=sys.stderr)
            return 2
        regs = hooks.get(name, [])
        sites = call_sites(module_dir, row["address"].upper())
        print(f"{row['module']} {row['address'].upper()} {name}: reads {', '.join(regs) or 'nothing'}"
              f"; {len(sites)} direct call site(s)")
        if not sites or not regs:
            continue
        values = defaultdict(list)
        for function, sets in sites:
            for reg in regs:
                values[reg].append(sets.get(reg))
            if args.verbose:
                print(f"  from {function}: " + "; ".join(f"{r}={sets.get(r, '?')}" for r in regs))
        # Arguments are positional: a caller that sets the registers below one the hook reads,
        # and none from it upwards, passes fewer arguments than the hook takes (Swing's
        # DrawVertices: r4 and r5 only, while WWE's took a start vertex in r5 and the count in
        # r6). Integer registers only; f1 is separate.
        ints = [r for r in regs if r.startswith("r")]
        for i, reg in enumerate(ints):
            if i == 0:
                continue
            beyond = 0
            for function, sets in sites:
                if (any(r in sets for r in ints[:i]) and
                        not any(r in sets for r in ints[i:])):
                    beyond += 1
            if beyond * 2 > len(sites):
                print(f"  FLAG {reg}: {beyond} of {len(sites)} callers set {', '.join(ints[:i])} "
                      f"and nothing from {reg} up (the function takes fewer arguments?)")
                flags += 1
        for reg in regs:
            known = [v for v in values[reg] if v is not None]
            if not known:
                # Wrappers pass their own arguments on unchanged: not enough to decide.
                print(f"  note {reg}: no caller sets it within {WINDOW} lines (pass-through?)")
            elif len(known) > 1 and len(set(known)) == 1 and known[0].startswith("li "):
                if (name, reg) in CONSTANT_OK:
                    print(f"  ok {reg}: always {known[0]} ({CONSTANT_OK[(name, reg)]})")
                else:
                    print(f"  FLAG {reg}: every caller that sets it passes {known[0]}")
                    flags += 1
    print(f"{flags} flag(s)")
    return 1 if flags else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
