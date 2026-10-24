"""List or extract the files in an Xbox 360 STFS package (CON/LIVE/PIRS), e.g. a title update.

Usage: python scripts/stfs_extract.py <package> [out_dir]
Without out_dir it only lists the files. Only read-only packages (one hash table
per level, as Microsoft-signed LIVE/PIRS content is) are supported.
Layout: https://free60.org/System-Software/Formats/STFS/
"""
import sys
from pathlib import Path

BLOCK = 0x1000
LEVELS = (0xAA, 0x70E4, 0x4AF768)  # data blocks covered by one hash table at levels 0-2

data = Path(sys.argv[1]).read_bytes()
if data[:4] not in (b"CON ", b"LIVE", b"PIRS"):
    sys.exit("not an STFS package")
be = lambda o, n=4: int.from_bytes(data[o:o + n], "big")
le3 = lambda o: int.from_bytes(data[o:o + 3], "little")

header_size = be(0x340)
flags = data[0x37B]
if not flags & 1:
    sys.exit("writable (two hash tables per level) packages are not supported")
table_blocks = int.from_bytes(data[0x37C:0x37E], "little")
table_start = le3(0x37E)
base = (header_size + 0xFFF) & ~0xFFF


def offset(index):
    """File offset of data block `index`, skipping the hash tables interleaved with the data."""
    block = index
    for level, covered in enumerate(LEVELS):
        if level and index < LEVELS[level - 1]:
            break
        block += index // covered + 1
    return base + block * BLOCK


def next_block(index):
    """The next data block in a chain, from the level-0 hash table entry for `index`."""
    table = offset(index - index % 0xAA) - BLOCK
    return be(table + (index % 0xAA) * 0x18 + 0x15, 3)


def chain(start, count, consecutive):
    index = start
    for _ in range(count):
        yield index
        index = index + 1 if consecutive else next_block(index)


entries = []
for block in chain(table_start, table_blocks, False):
    for i in range(BLOCK // 0x40):
        o = offset(block) + i * 0x40
        name_len = data[o + 0x28] & 0x3F
        if not name_len:
            continue
        entries.append({
            "name": data[o:o + name_len].decode("ascii"),
            "dir": bool(data[o + 0x28] & 0x80),
            "consecutive": bool(data[o + 0x28] & 0x40),
            "blocks": le3(o + 0x29),
            "start": le3(o + 0x2F),
            "parent": int.from_bytes(data[o + 0x32:o + 0x34], "big", signed=True),
            "size": be(o + 0x34),
        })


def path(entry):
    parts = [entry["name"]]
    while entry["parent"] != -1:
        entry = entries[entry["parent"]]
        parts.append(entry["name"])
    return "/".join(reversed(parts))


out = Path(sys.argv[2]) if len(sys.argv) > 2 else None
for entry in entries:
    if entry["dir"]:
        continue
    print(f"{entry['size']:>10}  {path(entry)}")
    if out:
        target = out / path(entry)
        target.parent.mkdir(parents=True, exist_ok=True)
        content = b"".join(data[offset(b):offset(b) + BLOCK]
                           for b in chain(entry["start"], entry["blocks"], entry["consecutive"]))
        target.write_bytes(content[:entry["size"]])
