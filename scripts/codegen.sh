#!/usr/bin/env bash
# codegen.sh - recompile Top Spin 4's three Modules to C++ on macOS (host).
#
# This is the reference's `scripts/build.ps1 -Action Codegen`, ported:
#   1. copy the three Modules (default.xex, Loader_DLL.xex, Swing_DLL.xex) from the
#      prepared disc data and their three Title Update 1 patches (.xexp) from the
#      prepared update data into a staging assets/ folder, gating each on its
#      SHA-256 (the Reference build: media 60819732 with TU1);
#   2. run ReXGlue's recompiler against codegen/topspin4_manifest.toml with
#      --ignore-stamp, which writes C++ for the entrypoint (boot) and the two
#      Modules (loader, swing) plus generated/rexglue.cmake.
#
# Output: ts4-port/generated/ (gitignored; never commit it). Game files are staged
# outside the repo, in work/codegen/assets/, so nothing from dump/ enters the tree.
#
# The recompiler must be built first:
#   cmake -S work/rexglue-sdk -B work/build/rexglue-host -G Ninja \
#       -DCMAKE_BUILD_TYPE=RelWithDebInfo -DCMAKE_OSX_ARCHITECTURES=arm64 \
#       -DCMAKE_C_COMPILER=/usr/bin/clang -DCMAKE_CXX_COMPILER=/usr/bin/clang++ \
#       -DREXGLUE_USE_VULKAN=ON -DREXGLUE_USE_D3D12=OFF \
#       -DREXGLUE_ENABLE_TRACY=OFF -DREXGLUE_BUILD_TESTS=OFF
#   cmake --build work/build/rexglue-host --target rexglue --parallel 8
# See docs/codegen.md.
#
# Environment overrides: TS4_WORK, REXGLUE_BIN.

set -euo pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
root=$(dirname "$here")
base=$(cd "$root/.." && pwd)
work=${TS4_WORK:-$base/work}
gamedata=$work/gamedata
updatedata=$work/updatedata
stage=$work/codegen
assets=$stage/assets
out=$root/generated
manifest_src=$root/codegen/topspin4_manifest.toml
manifest=$stage/topspin4_manifest.toml
host_bin=$work/build/rexglue-host/bin/rexglue
log=$work/logs/codegen.log

# The Reference build (ADR-0002): the disc's Modules plus Title Update 1's patches.
# Same list and hashes as scripts/build.ps1 -Action Codegen in the reference.
module_files=(
    "gamedata/default.xex       2D72E84FEAB9A84AC0E1239785A9AF1ED7B6E693CF39041F206A92AC9454CE28"
    "gamedata/Loader_DLL.xex    8F9ED530C9CF32CF2AE33534BA1CA99E3ABDCF2F5859BC8F68658E23187FD092"
    "gamedata/Swing_DLL.xex     DFA71292F75A649773C280DE7BC2688E6E47E473ABA397C248FB4204C45FAC44"
    "updatedata/default.xexp    1BA6119F45561CA5B916E3D13DF81B6318B3A9AC89E8FD3DC8BF292D30BD3FC7"
    "updatedata/Loader_DLL.xexp 9666DF4D181DB804EB9B59994274A7075475BB5C433E7CB12D116B5FD310F657"
    "updatedata/Swing_DLL.xexp  D5837EF967415ECD77547F3434CF8973F025BE8CB45E398FF7EECFD68A0726ED"
)

say() { printf '%s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

sha256() { shasum -a 256 "$1" | awk '{ print toupper($1) }'; }

mkdir -p "$work/logs" "$stage" "$assets" "$out"
exec > >(tee -a "$log") 2>&1

say "codegen: $(date '+%Y-%m-%d %H:%M:%S')"

[ -f "$manifest_src" ] || die "missing $manifest_src"
[ -d "$gamedata" ] || die "missing $gamedata; run scripts/prepare-gamedata.sh first"
[ -d "$updatedata" ] || die "missing $updatedata; run scripts/prepare-gamedata.sh first"

# The recompiler, built from work/rexglue-sdk by the commands at the top of this file.
if [ -n "${REXGLUE_BIN:-}" ]; then
    rexglue=$REXGLUE_BIN
elif [ -x "$host_bin" ]; then
    rexglue=$host_bin
elif [ -x "$work/rexglue-sdk/out/mac-arm64/rexglue" ]; then
    rexglue=$work/rexglue-sdk/out/mac-arm64/rexglue
elif [ -x "$work/rexglue-sdk/out/mac-arm64/rexgluerd" ]; then
    rexglue=$work/rexglue-sdk/out/mac-arm64/rexgluerd
else
    die "no host recompiler; build it first (commands at the top of this script, docs/codegen.md)"
fi
say "  recompiler: $rexglue ($("$rexglue" --version 2>&1 | head -1))"

# Stage and gate the Modules, exactly as the reference does: copy only if the
# destination is absent, then hash whatever is there.
for row in "${module_files[@]}"; do
    read -r source expected <<< "$row"
    name=${source##*/}
    from=$work/$source
    target=$assets/$name
    [ -f "$from" ] || die "missing $from; run scripts/prepare-gamedata.sh first"
    [ -f "$target" ] || cp -p "$from" "$target"
    actual=$(sha256 "$target")
    [ "$actual" = "$expected" ] || die "$target is not the Reference build's $name (got $actual); expected $expected"
    say "  verified $name"
done

# The manifest is copied per run: codegen stamps sdk_version back into it, and the
# copy in codegen/ stays the reference's own file.
cp -p "$manifest_src" "$manifest"

# generated/ is the repo's gitignored output folder. Codegen writes relative to the
# manifest, so the staging folder's generated/ is a symlink to it; the codegen
# writer only touches regular files inside, never the directory itself.
if [ -L "$stage/generated" ]; then
    rm -f "$stage/generated"
elif [ -e "$stage/generated" ]; then
    rm -rf "$stage/generated"
fi
ln -s "$out" "$stage/generated"

say "  manifest: $manifest"
say "  output: $out"
started=$SECONDS
(
    cd "$stage"
    "$rexglue" codegen topspin4_manifest.toml --ignore-stamp
)
elapsed=$(( SECONDS - started ))

[ -f "$out/rexglue.cmake" ] || die "codegen did not write generated/rexglue.cmake"
for module in boot loader swing; do
    [ -f "$out/$module/sources.cmake" ] || die "codegen did not write generated/$module/sources.cmake"
done

# generated/ must stay out of git: ADR-0006. Fail loudly if that ever stops holding.
git -C "$root" check-ignore -q generated || die "generated/ is not gitignored"
if git -C "$root" ls-files --error-unmatch generated >/dev/null 2>&1; then
    die "generated/ is tracked by git; it must never be committed"
fi

say "codegen: finished in ${elapsed}s"
for module in boot loader swing; do
    files=$(find "$out/$module" -type f | wc -l | tr -d ' ')
    bytes=$(find "$out/$module" -type f -exec stat -f %z {} + | awk '{ total += $1 } END { print total + 0 }')
    say "  generated/$module: $files files, $bytes bytes"
done
total_files=$(find "$out" -type f | wc -l | tr -d ' ')
total_bytes=$(find "$out" -type f -exec stat -f %z {} + | awk '{ total += $1 } END { print total + 0 }')
say "  generated/: $total_files files, $total_bytes bytes"
say "codegen: OK; log: $log"
