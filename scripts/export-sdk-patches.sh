#!/usr/bin/env bash
# Exports the Android commits of the SDK working copy (everything after the
# reference's 46-patch series) into ts4-port/patches/rexglue-android/, replacing
# the directory's contents so it always matches the branch.
#
# prepare-sdk.sh consumes the result with `git am`, in name order.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SDK="$ROOT/work/rexglue-sdk"
REF_PATCHES="$ROOT/_ref/Top-Spin-4-Recomp/patches/rexglue"
OUT="$ROOT/ts4-port/patches/rexglue-android"

[[ -d "$SDK/.git" ]] || { echo "no working copy at $SDK; run prepare-sdk.sh" >&2; exit 1; }

# The reference series is applied first, one commit per patch; ours start after.
ref_count=$(ls "$REF_PATCHES"/*.patch | wc -l | tr -d ' ')
first_android=$(git -C "$SDK" rev-list --reverse f5337cd..HEAD | sed -n "$((ref_count + 1))p")

mkdir -p "$OUT"
rm -f "$OUT"/*.patch
if [[ -z "$first_android" ]]; then
    echo "no Android commits yet; $OUT emptied"
    exit 0
fi

git -C "$SDK" format-patch --no-signature --zero-commit --no-numbered-files \
    -o "$OUT" "$first_android^..HEAD" >/dev/null

echo "exported $(ls "$OUT"/*.patch | wc -l | tr -d ' ') patches to $OUT"
ls "$OUT"
