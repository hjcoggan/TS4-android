"""List likely function entries in one Module that codegen has not discovered.

Three kinds of evidence, each named in the output:
- pdata: a function start in the image's .pdata exception table (written by the compiler).
- code: a code address built by "lis rX,hi" then "addi rY,rX,lo" or "ori rY,rX,lo" a few
  instructions later (a callback or thread start passed by address), where the preceding
  instruction ends a block and the target lies in another function than the code building
  it (an address built inside its own function is a local label, such as a jump table).
- table: a code address stored in data next to a known function start (a vtable or
  callback-table neighbour), where the preceding instruction ends a block (blr, bctr,
  unconditional b, or padding). Jump-table case labels are skipped: their neighbours are
  case labels as well, not known function starts.

Usage: python scripts/find_table_targets.py <boot|loader|swing> [--toml]
Inputs: analysis/xex/tu1/<module>.pe (the TU1 memory image, from XexTool) and the function
addresses in generated/<module>/topspin4_init.cpp. Adapted from WWE All Stars (ADR-0004).
"""
import bisect
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULES = {"boot": ("default", 0x82000000), "loader": ("Loader_DLL", 0x83000000),
           "swing": ("Swing_DLL", 0x88000000)}

module = sys.argv[1]
image_name, BASE = MODULES[module]
image = (ROOT / f"analysis/xex/tu1/{image_name}.pe").read_bytes()
init = (ROOT / f"generated/{module}/topspin4_init.cpp").read_text(errors="replace")
known = {int(a, 16) for a in re.findall(r"\{ (0x8[0-9A-F]{7}), \w+ \}", init)}
starts = sorted(known)


def function_of(address):
    """The nearest known function start at or below address."""
    i = bisect.bisect_right(starts, address)
    return starts[i - 1] if i else None


def sections():
    pe = struct.unpack_from("<I", image, 0x3C)[0]
    count = struct.unpack_from("<H", image, pe + 6)[0]
    table = pe + 24 + struct.unpack_from("<H", image, pe + 20)[0]
    for i in range(count):
        name, size, rva = struct.unpack_from("<8sII", image, table + 40 * i)
        flags = struct.unpack_from("<I", image, table + 40 * i + 36)[0]
        yield name.rstrip(b"\0").decode(), BASE + rva, size, flags


code = [(lo, lo + size) for _, lo, size, flags in sections() if flags & 0x20000000]
pdata = [(lo, size) for name, lo, size, _ in sections() if name == ".pdata"]


def in_code(address):
    return any(lo <= address < hi for lo, hi in code)


def word(address):
    return struct.unpack_from(">I", image, address - BASE)[0]


def ends_block(insn):
    if insn in (0x4E800020, 0x4E800420, 0x00000000, 0x60000000):  # blr, bctr, padding, nop
        return True
    return (insn >> 26) == 18 and (insn & 1) == 0  # b / ba without link


# Guest SEH filter and handler blocks run on their function's frame: within their first
# instructions they read it through r31 (loads, stores, addi with rA = r31) or start with the
# "mr r8,r8" scope marker. Scope tables point at them, but codegen covers them inside their
# function, so they are not entries.
FRAME_OPS = {14, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51,
             52, 53, 54, 55, 58, 62}


def seh_block(address):
    for insn in (word(address + 4 * i) for i in range(3)):
        if insn == 0x7D084378 or (insn >> 26 in FRAME_OPS and (insn >> 16) & 31 == 31):
            return True
    return False


candidates = {}
for lo, size in pdata:
    for entry in range(lo, lo + size - 7, 8):
        start = word(entry)
        if start and in_code(start) and start not in known:
            candidates.setdefault(start, []).append(f"pdata {entry:08X}")

# lis rX,hi ... addi/ori rY,rX,lo within this many instructions.
WINDOW = 8
for lo, hi in code:
    pending = {}  # register -> (high half, address of the lis)
    for address in range(lo, hi, 4):
        insn = word(address)
        op, rd, ra, imm = insn >> 26, (insn >> 21) & 31, (insn >> 16) & 31, insn & 0xFFFF
        if op == 15 and ra == 0:  # lis
            pending[rd] = (imm << 16, address)
            continue
        # addi rD,rA,SIMM reads rA; ori rA,rS,UIMM reads rS (the field addi calls rD).
        source, dest = (ra, rd) if op != 24 else (rd, ra)
        if op in (14, 24) and source in pending and address - pending[source][1] <= 4 * WINDOW:
            high, _ = pending[source]
            target = (high + (imm - 0x10000 if op == 14 and imm & 0x8000 else imm)) & 0xFFFFFFFF
            if (in_code(target) and not target & 3 and target not in known
                    and ends_block(word(target - 4)) and not seh_block(target)
                    and function_of(target) != function_of(address)):
                candidates.setdefault(target, []).append(f"code {address:08X}")
        if dest in pending and op not in (36, 37, 38, 44, 45, 62):  # not a store: dest is overwritten
            del pending[dest]

for offset in range(0, len(image) - 4, 4):
    address = BASE + offset
    if in_code(address) or any(lo <= address < lo + size for lo, size in pdata):
        continue
    target = struct.unpack_from(">I", image, offset)[0]
    if not in_code(target) or target & 3 or target in known:
        continue
    neighbours = (address - 4, address + 4)
    if not any(BASE <= n < BASE + len(image) - 4 and word(n) in known for n in neighbours):
        continue
    if not ends_block(word(target - 4)) or seh_block(target):
        continue
    candidates.setdefault(target, []).append(f"table {address:08X}")

if "--toml" in sys.argv:
    for target in sorted(candidates):
        print(f"0x{target:08X} = {{}}  # {', '.join(candidates[target][:3])}")
else:
    for target in sorted(candidates):
        print(f"{target:08X}  {' '.join(candidates[target][:4])}")
    print(f"{len(candidates)} candidates", file=sys.stderr)
