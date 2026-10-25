"""A Native shader by hash: its container, its converted HLSL and the constants it reads.

The Native renderer names shaders by the XXH3 hash of their container (traces print
"vs <hash> ps <hash>"); --ts4_shader_dump_dir names the container files by FNV-1a, and
scripts/convert-shaders.ps1 names the HLSL after the container file. This maps one to the other
and lists every float constant the HLSL reads, with its register, which is the key for lining up
Native's constants (--ts4_native_trace_ps) against Classic's packed ones in RenderDoc: Classic
packs only the registers a shader uses, in ascending order.

    python scripts/shader_lookup.py <hash> [--dumps cache/guest-shaders] [--hlsl cache/shader-native/hlsl]
"""
import argparse
import re
import sys
from pathlib import Path

import xxhash

ROOT = Path(__file__).resolve().parents[1]
CONSTANT_RE = re.compile(
    r"#define (\w+)(\(INDEX\))? .*?g_PushConstants\.(Vertex|Pixel)ShaderConstants \+ \(?(\d+)")


def main(argv):
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n")[0])
    parser.add_argument("hash", help="the shader's hash as a trace prints it (hex)")
    parser.add_argument("--dumps", type=Path, default=ROOT / "cache/guest-shaders")
    parser.add_argument("--hlsl", type=Path, default=ROOT / "cache/shader-native/hlsl")
    args = parser.parse_args(argv)
    wanted = int(args.hash, 16)
    for container in sorted(args.dumps.glob("*.bin")):
        if xxhash.xxh3_64_intdigest(container.read_bytes()) != wanted:
            continue
        hlsl = args.hlsl / f"{container.stem}.hlsl"
        print(f"container {container}")
        print(f"hlsl      {hlsl}" + ("" if hlsl.exists() else "  (missing: convert the dumps)"))
        if hlsl.exists():
            rows = []
            for m in CONSTANT_RE.finditer(hlsl.read_text(encoding="utf-8", errors="replace")):
                value = int(m.group(4))
                # A single constant's offset is in bytes; an array's base is a register index.
                register = value if m.group(2) else value // 16
                rows.append((register, m.group(1) + ("[]" if m.group(2) else "")))
            for register, name in sorted(rows):
                print(f"  c{register:<4} {name}")
        return 0
    print(f"no container in {args.dumps} hashes to {args.hash}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
