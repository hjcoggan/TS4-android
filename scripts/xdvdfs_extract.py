#!/usr/bin/env python3
"""List or extract the XDVDFS filesystem of an Xbox 360 disc image.

A small, dependency-free port of the reference launcher's XdvdfsImage.cs
(launcher/src/Launcher.Core/XdvdfsImage.cs), which itself mirrors
scripts/write_manifests.py. Only the game partition is read, so security
sectors, the video partition and stripped dumps never matter.

Usage:
    python3 -I xdvdfs_extract.py list    <iso>
    python3 -I xdvdfs_extract.py extract <iso> <dest> [<manifest.tsv>]
    python3 -I xdvdfs_extract.py check   <dir> <manifest.tsv>

`extract` writes every file below <dest>, skipping a file already there at its
manifest size and hash (resume), hashing every file as it is written and
refusing to continue on a mismatch. `check` re-hashes a prepared tree against
the manifest and reports missing, extra, wrong-size and wrong-hash files.

The manifest format is the launcher's (launcher/disc-manifest.tsv):
`#` comments, one `manifest_version<TAB>id` line, then
`sha256<TAB>size<TAB>path` per file.

Exit codes: 0 ok, 2 bad usage/manifest, 3 image or file mismatch, 4 I/O.
"""

import hashlib
import os
import sys
import time

SECTOR = 2048
VOLUME_DESCRIPTOR = 0x10000
MAGIC = b"MICROSOFT*XBOX*MEDIA"
# Where the game partition starts: plain XDVDFS, then the XGD2 (Top Spin 4),
# XGD3 and XGD1 layouts.
PARTITION_OFFSETS = (0, 0xFD90000, 0x2080000, 0x18300000)
CHUNK = 1 << 20


class BadImage(Exception):
    pass


class Xdvdfs:
    def __init__(self, path):
        self.path = path
        self.size = os.path.getsize(path)
        self.partition = None
        with open(path, "rb") as f:
            header = bytearray(SECTOR)
            for partition in PARTITION_OFFSETS:
                if partition + VOLUME_DESCRIPTOR + SECTOR > self.size:
                    continue
                f.seek(partition + VOLUME_DESCRIPTOR)
                if f.readinto(header) != SECTOR:
                    continue
                if header[:len(MAGIC)] == MAGIC and header[SECTOR - len(MAGIC):] == MAGIC:
                    self.partition = partition
                    self.root_sector = int.from_bytes(header[20:24], "little")
                    self.root_size = int.from_bytes(header[24:28], "little")
                    break
        if self.partition is None:
            raise BadImage("no XDVDFS volume descriptor at any known partition offset")

    def _read(self, offset, count):
        if offset < 0 or count < 0 or offset + count > self.size:
            raise BadImage("read outside the disc image")
        with open(self.path, "rb") as f:
            f.seek(offset)
            data = f.read(count)
        if len(data) != count:
            raise BadImage("the disc image is truncated")
        return data

    def files(self):
        """Every file: dicts of path ('/' separators), offset and size, sorted by path."""
        found = []
        self._walk(self.root_sector, self.root_size, "", 0, set(), found)
        names = set()
        for entry in found:
            key = entry["path"].lower()
            if key in names:
                raise BadImage("duplicate file names in the disc image")
            names.add(key)
        found.sort(key=lambda e: e["path"].lower())
        return found

    def _walk(self, sector, length, parent, depth, seen, out):
        if depth > 64 or sector in seen:
            raise BadImage("cyclic or excessively deep directory structure")
        if length == 0:
            return
        seen.add(sector)
        if length > 16 * 1024 * 1024:
            raise BadImage("unreasonably large directory table")
        data = self._read(self.partition + sector * SECTOR, length)
        pending = [0]
        visited = set()
        while pending:
            offset = pending.pop()
            if offset in visited or offset + 14 > length:
                raise BadImage("invalid directory entry pointer")
            visited.add(offset)
            left = int.from_bytes(data[offset:offset + 2], "little")
            right = int.from_bytes(data[offset + 2:offset + 4], "little")
            entry_sector = int.from_bytes(data[offset + 4:offset + 8], "little")
            entry_length = int.from_bytes(data[offset + 8:offset + 12], "little")
            attributes = data[offset + 12]
            name_length = data[offset + 13]
            if name_length == 0 or offset + 14 + name_length > length:
                raise BadImage("invalid entry name length")
            name = data[offset + 14:offset + 14 + name_length].decode("ascii", "replace")
            if name in (".", "..") or any(c in name for c in '/\\:<>"|?*\0'):
                raise BadImage("unsafe entry name in the disc image: %r" % name)
            relative = parent + "/" + name if parent else name
            absolute = self.partition + entry_sector * SECTOR
            if absolute + entry_length > self.size:
                raise BadImage("entry outside the disc image: " + relative)
            if attributes & 0x10:
                self._walk(entry_sector, entry_length, relative, depth + 1, seen, out)
            else:
                out.append({"path": relative, "offset": absolute, "size": entry_length})
            if right:
                pending.append(right * 4)
            if left:
                pending.append(left * 4)

    def copy_to(self, entry, sink, digest):
        """Reads one file, passing each block to sink and hashing it."""
        with open(self.path, "rb") as f:
            f.seek(entry["offset"])
            remaining = entry["size"]
            while remaining > 0:
                block = f.read(min(CHUNK, remaining))
                if not block:
                    raise BadImage("the disc image is truncated: " + entry["path"])
                digest.update(block)
                sink(block)
                remaining -= len(block)


def read_manifest(path):
    """Returns (version, {path_lower: (sha256, size, path)}) from a launcher manifest."""
    version = None
    files = {}
    with open(path, "r", encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\r\n")
            if not line or line[0] == "#":
                continue
            parts = line.split("\t")
            if parts[0] == "manifest_version" and len(parts) == 2 and version is None:
                version = parts[1]
            elif len(parts) == 3 and len(parts[0]) == 64 and parts[1].isdigit() and parts[2]:
                files[parts[2].lower()] = (parts[0].lower(), int(parts[1]), parts[2])
            else:
                raise BadImage("malformed manifest line: " + line)
    if version is None:
        raise BadImage("the manifest has no manifest_version line")
    return version, files


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            block = f.read(CHUNK)
            if not block:
                break
            digest.update(block)
    return digest.hexdigest()


def matches(path, expected_sha, expected_size):
    try:
        if os.path.getsize(path) != expected_size:
            return False
        return sha256_file(path) == expected_sha
    except OSError:
        return False


def compare_sets(image, manifest):
    """The image's files against the manifest's, case-insensitively; returns them in image order."""
    listed = image.files()
    image_by_key = {entry["path"].lower(): entry for entry in listed}
    missing = sorted(set(manifest) - set(image_by_key))
    extra = sorted(set(image_by_key) - set(manifest))
    if missing or extra:
        for key in missing[:10]:
            print("missing from the image: " + manifest[key][2], file=sys.stderr)
        for key in extra[:10]:
            print("not in the manifest: " + image_by_key[key]["path"], file=sys.stderr)
        raise BadImage("the image holds %d file(s) and the manifest %d: %d missing, %d extra"
                       % (len(listed), len(manifest), len(missing), len(extra)))
    for key, entry in image_by_key.items():
        if entry["size"] != manifest[key][1]:
            raise BadImage("size differs for %s: image %d, manifest %d"
                           % (entry["path"], entry["size"], manifest[key][1]))
    return listed


def safe_join(root, relative):
    target = os.path.normpath(os.path.join(root, relative))
    if not target.startswith(os.path.normpath(root) + os.sep):
        raise BadImage("path outside the output folder: " + relative)
    return target


def cmd_list(image_path, _args):
    image = Xdvdfs(image_path)
    listed = image.files()
    print("# partition 0x%X" % image.partition)
    total = 0
    for entry in listed:
        total += entry["size"]
        print("%d\t%s" % (entry["size"], entry["path"]))
    print("# %d files, %d bytes" % (len(listed), total))
    return 0


def cmd_extract(image_path, args):
    dest = args[0]
    manifest_path = args[1] if len(args) > 1 else None
    image = Xdvdfs(image_path)
    manifest = None
    if manifest_path:
        _, manifest = read_manifest(manifest_path)
        listed = compare_sets(image, manifest)
    else:
        listed = image.files()

    os.makedirs(dest, exist_ok=True)
    total_bytes = sum(entry["size"] for entry in listed)
    done_bytes = 0
    written = resumed = 0
    started = time.time()
    for index, entry in enumerate(listed, 1):
        target = safe_join(dest, entry["path"])
        expected = manifest[entry["path"].lower()] if manifest else None
        if expected and os.path.exists(target) and matches(target, expected[0], expected[1]):
            resumed += 1
            done_bytes += entry["size"]
            continue
        os.makedirs(os.path.dirname(target), exist_ok=True)
        temp = target + ".tmp"
        digest = hashlib.sha256()
        try:
            with open(temp, "wb") as out:
                image.copy_to(entry, out.write, digest)
                out.flush()
                os.fsync(out.fileno())
            actual = digest.hexdigest()
            if expected and (actual != expected[0] or os.path.getsize(temp) != expected[1]):
                raise BadImage("hash mismatch for %s: got %s, expected %s"
                               % (entry["path"], actual, expected[0]))
            os.replace(temp, target)
        finally:
            if os.path.exists(temp):
                os.unlink(temp)
        written += 1
        done_bytes += entry["size"]
        if index % 20 == 0 or index == len(listed):
            elapsed = max(time.time() - started, 0.001)
            print("  %d/%d files, %.2f/%.2f GiB, %.0f MiB/s"
                  % (index, len(listed), done_bytes / 2**30, total_bytes / 2**30,
                     done_bytes / 2**20 / elapsed), file=sys.stderr, flush=True)
    print("extracted %d file(s) (%d already present) to %s in %.1fs"
          % (written, resumed, dest, time.time() - started), file=sys.stderr)
    return 0


def cmd_check(_image_path, args):
    folder, manifest_path = args[0], args[1]
    version, manifest = read_manifest(manifest_path)
    missing = wrong_size = wrong_hash = 0
    present = set()
    total = 0
    for key, (expected_sha, expected_size, path) in manifest.items():
        target = safe_join(folder, path)
        if not os.path.isfile(target):
            print("missing: " + path, file=sys.stderr)
            missing += 1
            continue
        present.add(os.path.normpath(target))
        if os.path.getsize(target) != expected_size:
            print("wrong size: " + path, file=sys.stderr)
            wrong_size += 1
            continue
        if sha256_file(target) != expected_sha:
            print("wrong sha256: " + path, file=sys.stderr)
            wrong_hash += 1
            continue
        total += expected_size
    extra = 0
    for root, _dirs, files in os.walk(folder):
        for name in files:
            if name == ".topspin4-prepared":  # the launcher's marker, not game data
                continue
            candidate = os.path.normpath(os.path.join(root, name))
            if candidate not in present:
                extra += 1
                print("unexpected: " + os.path.relpath(candidate, folder), file=sys.stderr)
    bad = missing + wrong_size + wrong_hash + extra
    print("%s: %d files, %d bytes verified against %s (%s)%s"
          % (folder, len(manifest) - bad, total, os.path.basename(manifest_path), version,
             "" if not bad else "; %d missing, %d wrong size, %d wrong hash, %d unexpected"
             % (missing, wrong_size, wrong_hash, extra)))
    return 0 if bad == 0 else 3


def main(argv):
    if len(argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    command, rest = argv[1], argv[2:]
    try:
        if command == "list":
            if len(rest) != 1:
                print("list needs <iso>", file=sys.stderr)
                return 2
            return cmd_list(rest[0], rest[1:])
        if command == "extract":
            if len(rest) < 2 or len(rest) > 3:
                print("extract needs <iso> <dest> [<manifest.tsv>]", file=sys.stderr)
                return 2
            return cmd_extract(rest[0], rest[1:])
        if command == "check":
            if len(rest) != 2:
                print("check needs <folder> <manifest.tsv>", file=sys.stderr)
                return 2
            return cmd_check(rest[0], rest)
        print("unknown command: " + command, file=sys.stderr)
        return 2
    except BadImage as error:
        print("error: %s" % error, file=sys.stderr)
        return 3
    except OSError as error:
        print("error: %s" % error, file=sys.stderr)
        return 4


if __name__ == "__main__":
    sys.exit(main(sys.argv))
