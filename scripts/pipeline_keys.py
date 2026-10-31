"""The shipped pipeline key list (data/pipeline-keys.txt). Ported from WWE All Stars (ADR-0004).

Native prebuilds every key on the list before play (ADR-0010). The list holds
plain pipeline keys only: shader container hashes, formats and render-state
register values, as fixed-size hex lines. No shader code, no game data.

  merge   adds the keys of session stores (keys.bin) to the list; running it
          again with the same stores changes nothing
  check   reports keys whose shaders are not in a shader pack
  retag   moves the list to a new XenosRecomp patch whose keys mean the same
          (a patch that only adds what no listed key uses); any other patch
          change needs the list merged again from new stores

The key size is not fixed here: a store names it in its header and the list on
its key-size line, both sizeof(PipelineKey) of the game that wrote them. The
game refuses a list of another size (prebuild_plan.cpp, ReadKeyList), and the
host tests read data/pipeline-keys.txt against sizeof(PipelineKey).

The key layout is src/native/pipelines.h (PipelineKey), the store layout
src/native/pipeline_store.cpp, the pack layout src/native/shader_pack.h.
"""

from __future__ import annotations

import argparse
import struct
import sys
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path

LIST_VERSION = 1
# The game's ReadKeyList requires this word (the format came from WWE All Stars).
LIST_MAGIC = "wwe-pipeline-keys"
STORE_MAGICS = (b"WWEP", b"SVRP")  # SVRP: stores written before the magic was the port's own
STORE_HEADER = struct.Struct("<4sIIIQ")  # magic, version, key size, count, converter id
STORE_HEADER_SIZE = {2: 24, 3: 56}  # 3 adds the device tag (28 bytes) and padding
PACK_MAGIC = b"WWSP"
PACK_HEADER = struct.Struct("<4sIQIIII")
PACK_ENTRY = struct.Struct("<QIIII")
HEADER = """\
# Pipeline keys Native builds before play (ADR-0010). Each line is one
# PipelineKey (src/native/pipelines.h) in hex: shader container hashes,
# formats and render state. No shader code or game data.
# Written by scripts/pipeline_keys.py merge; do not edit by hand.
"""


@dataclass
class Store:
    converter: int
    key_size: int
    keys: list[bytes]


@dataclass
class KeyList:
    converter: int
    key_size: int
    keys: list[bytes]


@dataclass
class MergeReport:
    added: int = 0
    other_converter: int = 0  # stores skipped: shaders of another XenosRecomp patch
    other_key_size: int = 0  # stores skipped: another PipelineKey layout
    dropped: int = 0  # keys of the old list dropped: another converter or key size


def read_store(data: bytes) -> Store:
    """The converter id, key size and keys of a keys.bin (version 2 or 3)."""
    if len(data) < STORE_HEADER.size:
        raise ValueError("not a pipeline store (too short)")
    magic, version, key_size, count, converter = STORE_HEADER.unpack_from(data)
    if magic not in STORE_MAGICS:
        raise ValueError("not a pipeline store")
    if version not in STORE_HEADER_SIZE or key_size == 0:
        raise ValueError(f"pipeline store of another format (version {version}, key size {key_size})")
    start = STORE_HEADER_SIZE[version]
    if len(data) < start + count * key_size:
        raise ValueError("pipeline store is truncated")
    return Store(converter, key_size,
                 [data[start + i * key_size:start + (i + 1) * key_size] for i in range(count)])


def format_list(converter: int, key_size: int, keys: list[bytes]) -> str:
    if any(len(k) != key_size for k in keys):
        raise ValueError(f"a key is not {key_size} bytes")
    lines = [HEADER, f"{LIST_MAGIC} {LIST_VERSION}", f"key-size {key_size}",
             f"converter {converter:016x}"]
    lines += [k.hex() for k in sorted(set(keys))]
    return "\n".join(lines) + "\n"


def parse_list(text: str) -> KeyList:
    lines = [line.strip() for line in text.splitlines()]
    lines = [line for line in lines if line and not line.startswith("#")]
    if len(lines) < 3 or lines[0] != f"{LIST_MAGIC} {LIST_VERSION}" or \
            not lines[1].startswith("key-size ") or not lines[2].startswith("converter "):
        raise ValueError("not a pipeline key list of this version")
    key_size = int(lines[1].split()[1])
    converter = int(lines[2].split()[1], 16)
    keys = []
    for line in lines[3:]:
        if len(line) != 2 * key_size:
            raise ValueError(f"not a pipeline key: {line[:32]}")
        keys.append(bytes.fromhex(line))
    return KeyList(converter, key_size, keys)


def merge(list_text: str | None, stores: list[bytes], converter: int) -> tuple[str, MergeReport]:
    """The list with the stores' keys added. The key size is the stores' (the game that wrote
    them); the old list's keys stay only if they are of the same converter and key size."""
    report = MergeReport()
    read = [read_store(store) for store in stores]
    sizes = {store.key_size for store in read if store.converter == converter}
    old = parse_list(list_text) if list_text is not None else None
    if len(sizes) > 1:
        raise ValueError(f"stores of this converter disagree on the key size: {sorted(sizes)}")
    key_size = sizes.pop() if sizes else old.key_size if old else None
    if key_size is None:
        raise ValueError("no store of this converter and no list to take the key size from")
    keys: set[bytes] = set()
    if old is not None:
        if old.converter == converter and old.key_size == key_size:
            keys.update(old.keys)
        else:
            report.dropped = len(set(old.keys))
    before = len(keys)
    for store in read:
        if store.converter != converter:
            report.other_converter += 1
        elif store.key_size != key_size:
            report.other_key_size += 1
        else:
            keys.update(store.keys)
    report.added = len(keys) - before
    return format_list(converter, key_size, sorted(keys)), report


def retag(list_text: str, from_converter: int, to_converter: int) -> str:
    """The list with its keys unchanged under to_converter; the list must be from_converter's."""
    old = parse_list(list_text)
    if old.converter != from_converter:
        raise ValueError(f"the list is for converter {old.converter:016x}, not {from_converter:016x}")
    return format_list(to_converter, old.key_size, old.keys)


def pack_shaders(data: bytes) -> set[int]:
    magic, _version, _converter, count, *_ = PACK_HEADER.unpack_from(data)
    if magic != PACK_MAGIC:
        raise ValueError("not a shader pack")
    return {PACK_ENTRY.unpack_from(data, PACK_HEADER.size + i * PACK_ENTRY.size)[0]
            for i in range(count)}


def pack_converter(data: bytes) -> int:
    magic, _version, converter, *_ = PACK_HEADER.unpack_from(data)
    if magic != PACK_MAGIC:
        raise ValueError("not a shader pack")
    return converter


def check_against_pack(list_text: str, pack: bytes, top_ups: Sequence[bytes] = ()) -> list[bytes]:
    """Keys on the list whose vertex or pixel shader is neither in the pack nor in one of
    the top-up packs the game merges into it (<cache_root>/shader-topup)."""
    shaders = pack_shaders(pack)
    for top_up in top_ups:
        shaders |= pack_shaders(top_up)
    missing = []
    for k in parse_list(list_text).keys:
        vertex_shader, pixel_shader = struct.unpack_from("<QQ", k)
        if vertex_shader not in shaders or (pixel_shader and pixel_shader not in shaders):
            missing.append(k)
    return missing


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="The shipped pipeline key list.")
    commands = parser.add_subparsers(dest="command", required=True)
    merge_parser = commands.add_parser("merge", help="add session stores' keys to the list")
    merge_parser.add_argument("list", type=Path)
    merge_parser.add_argument("converter_id", help="XenosRecomp patch ID, 16 hex digits")
    merge_parser.add_argument("stores", type=Path, nargs="+", help="keys.bin files")
    check_parser = commands.add_parser("check", help="report keys whose shaders left the pack")
    check_parser.add_argument("list", type=Path)
    check_parser.add_argument("pack", type=Path)
    check_parser.add_argument("--top-ups", type=Path, help="the shader-topup folder beside the pack")
    retag_parser = commands.add_parser("retag", help="move the list to a patch whose keys mean the same")
    retag_parser.add_argument("list", type=Path)
    retag_parser.add_argument("from_converter", help="the list's XenosRecomp patch ID")
    retag_parser.add_argument("to_converter", help="the new XenosRecomp patch ID")
    args = parser.parse_args(argv)

    if args.command == "retag":
        text = retag(args.list.read_text(encoding="utf-8"), int(args.from_converter, 16),
                     int(args.to_converter, 16))
        with args.list.open("w", encoding="utf-8", newline="\n") as file:
            file.write(text)
        print(f"{len(parse_list(text).keys)} keys now under converter {args.to_converter}: {args.list}")
        return 0

    if args.command == "merge":
        converter = int(args.converter_id, 16)
        existing = args.list.read_text(encoding="utf-8") if args.list.exists() else None
        stores = []
        for path in args.stores:
            try:
                data = path.read_bytes()
                read_store(data)
            except (OSError, ValueError) as error:
                print(f"skipped {path}: {error}")
                continue
            stores.append(data)
        try:
            text, report = merge(existing, stores, converter)
        except ValueError as error:
            print(f"pipeline key list not written: {error}", file=sys.stderr)
            return 1
        args.list.parent.mkdir(parents=True, exist_ok=True)
        with args.list.open("w", encoding="utf-8", newline="\n") as file:
            file.write(text)
        print(f"{len(parse_list(text).keys)} keys ({report.added} new, {report.dropped} dropped); "
              f"stores skipped: {report.other_converter} of another converter, "
              f"{report.other_key_size} of another key size: {args.list}")
        return 0

    if not args.list.exists():
        print(f"pipeline key list: none at {args.list}")
        return 0
    text = args.list.read_text(encoding="utf-8")
    pack = args.pack.read_bytes()
    converter = parse_list(text).converter
    if pack_converter(pack) != converter:
        # The game drops the whole list then (prebuild_plan.cpp); merge or retag it.
        print(f"pipeline key list: converter {converter:016x}, the pack's is "
              f"{pack_converter(pack):016x}; the game ignores the list until it is merged or retagged")
        return 0
    top_ups = sorted(args.top_ups.glob("*.shaderpack")) if args.top_ups and args.top_ups.is_dir() else []
    missing = check_against_pack(text, pack, [path.read_bytes() for path in top_ups])
    for k in missing:
        vertex_shader, pixel_shader = struct.unpack_from("<QQ", k)
        print(f"pipeline key list: shaders {vertex_shader:016x}/{pixel_shader:016x} not in the pack")
    print(f"pipeline key list: {len(missing)} keys whose shaders are not in {args.pack}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
