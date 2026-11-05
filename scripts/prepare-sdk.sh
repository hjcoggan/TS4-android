#!/usr/bin/env bash
# Reproduces the Android working copy of the ReXGlue SDK from scratch.
#
#   base:      _ref/rexglue-sdk @ f5337cd (pristine, read only)
#   branch:    ts4-android
#   patches:   _ref/Top-Spin-4-Recomp/patches/rexglue/*.patch (46, in name order)
#              ts4-port/patches/rexglue-android/*.patch  (our Android series)
#
# Every reference patch becomes one commit ("ref: NNNN-name"); every Android patch
# is applied with `git am` so its commit message and author are preserved.
#
# Idempotent: re-running resets the working copy to f5337cd and rebuilds the branch
# from the two series, so the result always matches the patches on disk. Build
# output under <work>/out is left alone.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
REF_SDK="$ROOT/_ref/rexglue-sdk"
REF_PORT="$ROOT/_ref/Top-Spin-4-Recomp"
WORK="$ROOT/work"
SDK="$WORK/rexglue-sdk"
BRANCH="ts4-android"
BASE="f5337cd"
ANDROID_PATCHES="$ROOT/ts4-port/patches/rexglue-android"
SDL_PATCHES="$ROOT/ts4-port/patches/sdl3"
REF_PATCHES="$REF_PORT/patches/rexglue"

# Where the two reference trees come from when they are not already present. Neither is vendored:
# the SDK is BSD-3-Clause and the reference port ships only its patch series here.
SDK_URL="${TS4_SDK_URL:-https://github.com/rexglue/rexglue-sdk.git}"
PORT_URL="${TS4_PORT_URL:-https://github.com/shaneomac1337/Top-Spin-4-Recomp.git}"

say() { printf '\n== %s\n' "$*"; }

mkdir -p "$WORK" "$WORK/logs" "$ROOT/_ref"

# The pristine SDK at the pinned commit, and the reference port (only its patches are read).
if [[ ! -d "$REF_SDK/.git" ]]; then
    say "clone $SDK_URL -> $REF_SDK"
    git clone --filter=blob:none "$SDK_URL" "$REF_SDK"
    git -C "$REF_SDK" checkout -q "$BASE"
fi
if [[ ! -d "$REF_PORT/.git" ]]; then
    say "clone $PORT_URL -> $REF_PORT (patch series only)"
    git clone --depth 1 "$PORT_URL" "$REF_PORT"
fi
[[ -d "$REF_PATCHES" ]] || { echo "missing reference patches: $REF_PATCHES" >&2; exit 1; }

# ---------------------------------------------------------------- working copy
if [[ ! -d "$SDK/.git" ]]; then
    say "clone $REF_SDK -> $SDK"
    git clone --no-hardlinks --no-checkout "$REF_SDK" "$SDK"
else
    say "reuse existing clone $SDK"
fi

cd "$SDK"

# ---------------------------------------------------------------- base commit
say "checkout $BASE on $BRANCH"
if git show-ref --verify --quiet "refs/heads/$BRANCH"; then
    git checkout -q -B "$BRANCH" "$BASE"
else
    git checkout -q -b "$BRANCH" "$BASE"
fi
git reset -q --hard "$BASE"
git clean -qfd -e out

# Submodules: clone from the reference clone's own module stores (offline, exact
# commits, no second download) instead of the upstream URLs in .gitmodules.
say "submodules"
git submodule init
while read -r key _; do
    path="${key#submodule.}"
    path="${path%.url}"
    local_ref="$REF_SDK/$path"
    if [[ -e "$local_ref/.git" ]]; then
        git config "submodule.$path.url" "$local_ref"
    fi
done < <(git config -f .gitmodules --get-regexp '^submodule\..*\.url$')
git -c protocol.file.allow=always submodule update --init --recursive

# ------------------------------------------------------------ SDL submodule series
# Applied inside the submodule: the patch touches thirdparty/sdl3, not the SDK.
shopt -s nullglob
sdl_patches=("$SDL_PATCHES"/*.patch)
if (( ${#sdl_patches[@]} )); then
    say "apply sdl3 series (${#sdl_patches[@]} patches)"
    for patch in "${sdl_patches[@]}"; do
        if ! git -C thirdparty/sdl3 apply --whitespace=nowarn "$patch"; then
            echo "sdl3 patch failed: $patch" >&2
            exit 1
        fi
    done
fi

# ------------------------------------------------------- reference patch series
say "apply reference series ($(ls "$REF_PATCHES"/*.patch | wc -l | tr -d ' ') patches)"
for patch in "$REF_PATCHES"/*.patch; do
    name="$(basename "$patch" .patch)"
    if ! git apply --whitespace=nowarn "$patch"; then
        echo "reference patch failed: $patch" >&2
        exit 1
    fi
    git add -A
    git commit -q -m "ref: $name" \
        -m "Applied verbatim from _ref/Top-Spin-4-Recomp/patches/rexglue/$name.patch."
done

# --------------------------------------------------------- our Android series
shopt -s nullglob
android_patches=("$ANDROID_PATCHES"/*.patch)
if (( ${#android_patches[@]} )); then
    say "apply android series (${#android_patches[@]} patches)"
    git am --whitespace=nowarn "${android_patches[@]}"
else
    say "no android patches yet"
fi

say "done: $(git rev-parse --short HEAD) on $BRANCH, $(git rev-list --count "$BASE"..HEAD) commits"
