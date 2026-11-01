"""Rewrite Top Spin 4's Lua 5.0 bytecode so standard tools (unluac) can read it.

The game's Lua (Swing_DLL, loader 888115e8) adds one constant type to Lua 5.0's nil (0),
number (3) and string (4): 9, a wide string (88811490: u32 character count including the
terminator, then that many UTF-16 characters in the chunk's byte order). This rewrites each one
as a type 4 string holding the UTF-8 text with a "W:" prefix, so a decompiled script still
shows which strings were wide. Everything else is copied as it is.

    python scripts/lua_wide_strings.py <in.luac> <out.luac>
    python scripts/lua_wide_strings.py --dir <in dir> <out dir>    every *.luac below <in dir>
"""
import struct
import sys
from pathlib import Path

WIDE = 9


class Chunk:
    def __init__(self, data):
        self.data, self.pos, self.out = data, 0, bytearray()

    def take(self, n):
        piece = self.data[self.pos:self.pos + n]
        if len(piece) < n:
            raise ValueError(f"cut short at {self.pos:#x}")
        self.pos += n
        return piece

    def copy(self, n):
        piece = self.take(n)
        self.out += piece
        return piece

    def header(self):
        head = self.copy(5)
        if head[:4] != b"\x1bLua" or head[4] != 0x50:
            raise ValueError("not Lua 5.0 bytecode")
        little = self.copy(1)[0] == 1
        sizes = self.copy(7)  # int, size_t, Instruction, SIZE_OP, SIZE_A, SIZE_B, SIZE_C
        if sizes[0] != 4 or sizes[1] != 4 or sizes[2] != 4:
            raise ValueError("expected 4-byte int, size_t and Instruction")
        self.number_size = self.copy(1)[0]
        self.copy(self.number_size)  # the check number
        self.endian = "<" if little else ">"

    def int(self):
        return struct.unpack(self.endian + "i", self.copy(4))[0]

    def string(self):
        size = struct.unpack(self.endian + "I", self.copy(4))[0]
        self.copy(size)

    def function(self):
        self.string()  # source
        self.int()  # line defined
        self.copy(4)  # upvalue count, parameter count, vararg flag, max stack size
        self.copy(4 * self.int())  # line info
        for _ in range(self.int()):  # locals
            self.string()
            self.copy(8)
        for _ in range(self.int()):  # upvalue names
            self.string()
        for _ in range(self.int()):  # constants
            kind = self.take(1)[0]
            if kind == WIDE:
                count = struct.unpack(self.endian + "I", self.take(4))[0]
                raw = self.take(count * 2)
                codec = "utf-16-le" if self.endian == "<" else "utf-16-be"
                text = raw.decode(codec, errors="replace").rstrip("\0")
                encoded = ("W:" + text).encode("utf-8") + b"\0"
                self.out.append(4)
                self.out += struct.pack(self.endian + "I", len(encoded)) + encoded
                continue
            self.out.append(kind)
            if kind == 3:
                self.copy(self.number_size)
            elif kind == 4:
                self.string()
            elif kind != 0:
                raise ValueError(f"constant type {kind} at {self.pos:#x}")
        for _ in range(self.int()):  # nested functions
            self.function()
        self.copy(4 * self.int())  # code


def convert(data):
    chunk = Chunk(data)
    chunk.header()
    chunk.function()
    return bytes(chunk.out)


def main(argv):
    if argv[:1] == ["--dir"]:
        source, target = Path(argv[1]), Path(argv[2])
        failed = 0
        for path in sorted(source.rglob("*.luac")):
            out = target / path.relative_to(source)
            out.parent.mkdir(parents=True, exist_ok=True)
            try:
                out.write_bytes(convert(path.read_bytes()))
            except ValueError as error:
                print(f"{path}: {error}", file=sys.stderr)
                failed += 1
        return 1 if failed else 0
    Path(argv[1]).write_bytes(convert(Path(argv[0]).read_bytes()))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
