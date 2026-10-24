"""Carry topspin4-decomp's names from the base build to the TU1 images (ticket 04).

Usage: python scripts/tu_symbol_map.py [--decomp DIR] [--out FILE] [--report FILE]

topspin4-decomp names Swing's functions in the base (disc) image. TU1 relinks Swing (ADR-0002),
so names move by function similarity, not by address: scripts/tu_addrmap.py matches .pdata
functions by their code with branch and address immediates masked. Inputs are the decomp's
config/54540859/symbols.txt (named functions; its fn_XXXXXXXX auto-names are skipped) and
config/symbols.csv (reviewed names, which win). The output has the decomp's own
symbols.csv columns, so its Ghidra script ApplySymbolMap.java reads it: names and addresses
only, no game data. The report lists the match rates and the names left unmatched.
"""
from __future__ import annotations

import argparse
import collections
import csv
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import tu_addrmap  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
SYMBOL_LINE = re.compile(r"^(\S+) = \.text:0x([0-9A-Fa-f]{8}); // type:function")
AUTO_NAME = re.compile(r"^fn_[0-9A-Fa-f]{8}$")
HEADER = ["module", "address", "name", "confidence", "evidence"]
# A function* match (same code shape, a struct offset or constant differs) is still the same
# function, but a hook placed by its name deserves a look.
# data-ref: matched code forms the address with lis/addi (a callback or handler passed by address).
# registration: a registration function pairs the handler with the same command string in both.
CONFIDENCE = {"function": "high", "function*": "medium", "local": "medium", "via-table": "medium",
              "data-ref": "medium", "registration": "high"}


def decomp_names(decomp: Path) -> dict[int, tuple[str, str, str]]:
    """Base address -> (name, confidence, evidence), reviewed rows over listed names."""
    names = {}
    for line in (decomp / "config/54540859/symbols.txt").read_text(encoding="utf-8").splitlines():
        m = SYMBOL_LINE.match(line)
        if m and not AUTO_NAME.match(m.group(1)):
            names[int(m.group(2), 16)] = (m.group(1), "medium", "symbols.txt")
    with open(decomp / "config/symbols.csv", newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            if row["module"] == "Swing_DLL.xex":
                names[int(row["address"], 16)] = (row["name"], row["confidence"], row["evidence"])
    return names


def c_string(image: tu_addrmap.Image, address: int) -> str | None:
    """The printable C string at address, if one starts there."""
    if not image.base <= address < image.end or image.text()[0] <= address < image.text()[1]:
        return None
    o = address - image.base
    end = image.data.find(b"\0", o, o + 256)
    text = image.data[o:end] if end > o else b""
    return text.decode() if len(text) >= 3 and all(0x20 <= c < 0x7F for c in text) else None


def registrations(image: tu_addrmap.Image, start: int) -> dict[str, list[int]]:
    """Command name -> the code addresses a registration function forms after that name's string
    (the handler, and any shared wrapper it registers with)."""
    found, current = collections.defaultdict(list), None
    lo, hi = image.text()
    for ref in tu_addrmap.address_refs(image, start, image.functions[start]):
        if ref is None:
            continue
        name = c_string(image, ref)
        if name is not None:
            current = name
        elif current is not None and lo <= ref < hi:
            found[current].append(ref)
    return found


def map_by_registration(swing: tu_addrmap.AddressMap, names: dict[int, tuple[str, str, str]]) -> dict[int, tuple[int, str]]:
    """Base handler address -> (TU1 address, command name), from registration functions matched
    in both images: the command's name string pairs the handlers across the relink."""
    mapped = {}
    for base_address, (name, _, _) in names.items():
        if not name.startswith("Register"):
            continue
        tu1 = swing.map(base_address).new
        if tu1 is None or base_address not in swing.base.functions or tu1 not in swing.tu2.functions:
            continue
        base_regs, tu1_regs = registrations(swing.base, base_address), registrations(swing.tu2, tu1)
        for command, handlers in base_regs.items():
            if len(tu1_regs.get(command, [])) == len(handlers):
                for b, t in zip(handlers, tu1_regs[command]):
                    mapped.setdefault(b, (t, command))
    return mapped


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Map topspin4-decomp's Swing names to TU1.")
    parser.add_argument("--decomp", type=Path, default=ROOT / ".tools/topspin4-decomp")
    parser.add_argument("--out", type=Path, default=ROOT / "symbols/tu1-symbols.csv")
    parser.add_argument("--report", type=Path, help="write the Markdown report here")
    args = parser.parse_args(argv)

    maps = {module: tu_addrmap.address_map(module) for module in tu_addrmap.MODULES}
    rates = {module: (len(m.functions.map), len(m.base.functions), len(m.functions.changed))
             for module, m in maps.items()}
    swing = maps["swing"]

    names = decomp_names(args.decomp)
    by_registration = map_by_registration(swing, names)
    rows, unmatched, methods = [], [], collections.Counter()
    script_rows, script_confirmed = [], []
    for base_address, (name, confidence, evidence) in sorted(names.items()):
        mapping = swing.map(base_address)
        registered = by_registration.get(base_address)
        if mapping.new is None and registered is not None:
            # Tiny handlers (li r3,N; blr) have copies everywhere; the command string pins them.
            mapping = tu_addrmap.Mapping(base_address, registered[0], "registration",
                                         f'registered as "{registered[1]}"')
        methods[mapping.method] += 1
        if mapping.new is None:
            unmatched.append((base_address, name, mapping.method, mapping.note))
            continue
        level = confidence if CONFIDENCE[mapping.method] == "high" else "medium"
        note = f"; {mapping.note}" if mapping.note else ""
        row = ["Swing_DLL.xex", f"{mapping.new:08x}", name, level,
               f"tu1 {mapping.method} from base {base_address:08x}{note}; {evidence}"]
        rows.append(row)
        if name.startswith("Script_"):
            script_rows.append(row)
            # Confirmed when the registration pairs the same command string with this address.
            if registered is not None and registered[0] == mapping.new:
                script_confirmed.append(row)

    # The TU1 link folded some functions with identical code into one, so several names can land
    # on one address: one row keeps the first name (a high-confidence one if any) and names the
    # rest in its evidence, since a hook there changes every one of them.
    by_address = collections.defaultdict(list)
    for row in rows:
        by_address[row[1]].append(row)
    folded, out_rows = [], []
    for address, group in sorted(by_address.items()):
        group.sort(key=lambda row: (row[3] != "high", row[2]))
        row = list(group[0])
        if len(group) > 1:
            others = [other[2] for other in group[1:]]
            row[4] += "; folded in TU1 with " + " ".join(others)
            folded.append((address, [group[0][2]] + others))
        out_rows.append(row)

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with open(args.out, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(HEADER)
        writer.writerows(out_rows)

    summary = [f"{module}: {matched} of {total} .pdata functions matched ({matched / total:.1%}), "
               f"{changed} by position with changed code" for module, (matched, total, changed) in rates.items()]
    summary.append(f"names: {len(rows)} of {len(names)} mapped ({len(rows) / len(names):.1%}); "
                   + ", ".join(f"{n} {m}" for m, n in methods.most_common()))
    summary.append(f"script commands: {len(script_rows)} mapped, {len(script_confirmed)} paired with "
                   "their command string by a registration function in both images")
    summary.append(f"folded: {sum(len(n) for _, n in folded)} names share {len(folded)} TU1 addresses: "
                   + "; ".join(f"{a.upper()} " + ", ".join(n) for a, n in folded))
    print("\n".join(summary))
    for base_address, name, method, note in unmatched:
        print(f"  unmatched {name} (base {base_address:08X}): {method} {note}")

    if args.report:
        lines = ["| Name | Base | Why not mapped |", "|---|---|---|"]
        lines += [f"| `{name}` | `{a:08X}` | {method} {note} |" for a, name, method, note in unmatched]
        unconfirmed = [row for row in script_rows if row not in script_confirmed]
        lines += ["", "Script commands mapped without a registration pairing them by command string:", ""]
        lines += [f"- `{row[2]}` at `{row[1].upper()}` ({row[4]})" for row in unconfirmed] or ["- none"]
        args.report.write_text("\n".join(["- " + s for s in summary] + [""] + lines) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
