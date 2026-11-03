#!/usr/bin/env bash
# prepare-gamedata.sh - prepare the game data the port runs from, on macOS.
#
# The reference's setup tool (launcher/, Windows) does three things: it checks the
# disc image and Title Update 1 against launcher/disc-manifest.tsv and
# launcher/tu1-manifest.tsv, extracts the disc's game partition into gamedata/ and
# unpacks TU1 into updatedata/, and then builds the Native shader pack with
# XenosRecomp (Windows only; see docs/codegen.md). This script does the first two,
# with the same layout, checks and marker files, so the runtime can mount
# work/gamedata as game: and work/updatedata as update:.
#
# Inputs (read only):
#   dump/Top Spin 4 (USA, Europe) (En,Fr,De,Es,It).iso   XGD2, XDVDFS at 0xFD90000
#   dump/tu1/                                            TU1 already unpacked (STFS)
# Outputs:
#   work/gamedata/    230 files, 5,737,767,977 bytes (~5.34 GiB)
#   work/updatedata/    7 files, 2,425,486 bytes
#   work/logs/prepare-gamedata.log
#
# Environment overrides: TS4_DUMP, TS4_WORK, TS4_ISO, TS4_TU1.
# Re-running is safe: a complete folder is re-verified and kept; an interrupted one
# is resumed from the files that verify.

set -euo pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
root=$(dirname "$here")
base=$(cd "$root/.." && pwd)
dump=${TS4_DUMP:-$base/dump}
work=${TS4_WORK:-$base/work}
iso=${TS4_ISO:-$dump/Top Spin 4 (USA, Europe) (En,Fr,De,Es,It).iso}
tu1=${TS4_TU1:-$dump/tu1}
manifests=$root/codegen/manifests
disc_manifest=$manifests/disc-manifest.tsv
tu1_manifest=$manifests/tu1-manifest.tsv
extractor=$here/xdvdfs_extract.py

# The launcher's SpareBytes: 512 MiB head room, the 256 MiB shader pack and the
# 64 MiB pipeline store (Preparation.cs, PipelinePreparation.cs). The shader pack is
# not built here; the room is still reserved so a full drive fails at the start.
spare=$(( (512 + 256 + 64) * 1024 * 1024 ))
marker=.topspin4-prepared

mkdir -p "$work/logs"
log=$work/logs/prepare-gamedata.log
exec > >(tee -a "$log") 2>&1

say() { printf '%s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

[ -f "$iso" ] || die "disc image not found: $iso (set TS4_ISO)"
[ -d "$tu1" ] || die "TU1 folder not found: $tu1 (set TS4_TU1)"
[ -f "$disc_manifest" ] || die "missing $disc_manifest"
[ -f "$tu1_manifest" ] || die "missing $tu1_manifest"
command -v python3 >/dev/null || die "python3 is required"

# Manifest helpers: version and total bytes of a launcher manifest.
manifest_version() { awk -F'\t' '$1 == "manifest_version" { print $2; exit }' "$1"; }
manifest_bytes() {
    awk -F'\t' '!/^#/ && $1 != "manifest_version" { total += $2 } END { print total + 0 }' "$1"
}

say "prepare-gamedata: $(date '+%Y-%m-%d %H:%M:%S')"
say "  disc image: $iso"
say "  title update: $tu1"
say "  outputs: $work/gamedata, $work/updatedata"

# Disk space first, like the launcher: both folders and the spare must fit before
# the first byte is written. Extracting resumes into <folder>.partial, so the
# finished folder is never doubled.
need=$(( $(manifest_bytes "$disc_manifest") + $(manifest_bytes "$tu1_manifest") + spare ))
free_kb=$(df -Pk "$work" | awk 'NR == 2 { print $4 }')
free=$(( free_kb * 1024 ))
say "  free on $work: $(( free / 1024 / 1024 )) MiB; needed: $(( need / 1024 / 1024 )) MiB"
[ "$free" -ge "$need" ] || die "not enough free space: need $(( need / 1024 / 1024 )) MiB, have $(( free / 1024 / 1024 )) MiB"

# A folder is done when its marker names this manifest and every file verifies.
# The marker is the launcher's (PreparedFolder.MarkerFileName); the runtime does not
# need it, but it keeps this script's bookkeeping the same as the reference's.
folder_done() {
    local dir=$1 manifest=$2 version
    version=$(manifest_version "$manifest")
    [ -d "$dir" ] || return 1
    [ -f "$dir/$marker" ] || return 1
    [ "$(cat "$dir/$marker")" = "manifest_version=$version" ] || return 1
    python3 -I "$extractor" check "$dir" "$manifest" >/dev/null 2>&1
}

commit() {
    local dir=$1 manifest=$2
    printf 'manifest_version=%s\n' "$(manifest_version "$manifest")" > "$dir/$marker"
    mv "$dir" "${dir%.partial}"
}

# Move a folder that is not done aside so writing resumes into it, like the launcher
# (a stale copy is dropped when a newer partial one exists).
begin_partial() {
    local dir=$1
    if [ -d "$dir" ]; then
        if [ -d "$dir.partial" ]; then rm -rf "$dir"; else mv "$dir" "$dir.partial"; fi
    fi
    mkdir -p "$dir.partial"
}

prepare_disc() {
    local dest=$work/gamedata started
    if folder_done "$dest" "$disc_manifest"; then
        say "gamedata: already prepared and verified ($(manifest_version "$disc_manifest"))"
        return
    fi
    begin_partial "$dest"
    started=$SECONDS
    say "gamedata: extracting the disc's game partition..."
    python3 -I "$extractor" extract "$iso" "$dest.partial" "$disc_manifest"
    python3 -I "$extractor" check "$dest.partial" "$disc_manifest"
    commit "$dest.partial" "$disc_manifest"
    say "gamedata: done in $(( SECONDS - started ))s"
}

prepare_update() {
    local dest=$work/updatedata sha size path src count=0
    if folder_done "$dest" "$tu1_manifest"; then
        say "updatedata: already prepared and verified ($(manifest_version "$tu1_manifest"))"
        return
    fi
    begin_partial "$dest"
    say "updatedata: unpacking Title Update 1..."
    while IFS=$'\t' read -r sha size path; do
        case $sha in ''|\#*|manifest_version) continue ;; esac
        src=$tu1/$path
        [ -f "$src" ] || die "missing $src"
        mkdir -p "$(dirname "$dest.partial/$path")"
        cp -f "$src" "$dest.partial/$path"
        count=$((count + 1))
    done < "$tu1_manifest"
    python3 -I "$extractor" check "$dest.partial" "$tu1_manifest"
    commit "$dest.partial" "$tu1_manifest"
    say "updatedata: $count files"
}

prepare_disc
prepare_update

# The final word: every file, every hash, against the manifests the reference build
# was verified with, and nothing unexpected left in either folder.
say "verifying both folders against the manifests..."
python3 -I "$extractor" check "$work/gamedata" "$disc_manifest"
python3 -I "$extractor" check "$work/updatedata" "$tu1_manifest"

for folder in gamedata updatedata; do
    files=$(find "$work/$folder" -type f ! -name "$marker" | wc -l | tr -d ' ')
    bytes=$(find "$work/$folder" -type f ! -name "$marker" -exec stat -f %z {} + | awk '{ total += $1 } END { print total + 0 }')
    say "  $folder: $files files, $bytes bytes"
done
say "prepare-gamedata: OK; log: $log"
