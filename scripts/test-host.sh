#!/usr/bin/env bash
# Build and run the host unit tests: the renderer's pure logic, on this machine, with no device and
# no game data. Catch2 comes from the SDK tree.
#
#   ts4-port/scripts/test-host.sh [catch2 args...]
#
# Examples:
#   scripts/test-host.sh                       # everything
#   scripts/test-host.sh "[clear]"             # one tag/name pattern
#   scripts/test-host.sh --list-tests          # what is covered
#
# The SDK must be prepared first (scripts/prepare-sdk.sh) and built for the host; the tests link
# rex::runtime from work/rexglue-sdk/out/<host> (mac-arm64, linux-arm64, or linux-amd64).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
sdk="${TS4_REXGLUE_SDK:-$root/work/rexglue-sdk}"
build="${TS4_TEST_BUILD:-$root/work/build/host-tests}"

[[ -d "$sdk" ]] || { echo "missing SDK at $sdk; run scripts/prepare-sdk.sh" >&2; exit 1; }

prefix_path="${TS4_REXGLUE_PREFIX:-}"
if [[ -z "$prefix_path" ]]; then
    for candidate in "$sdk/out/mac-arm64" "$sdk/out/linux-arm64" "$sdk/out/linux-amd64" "$root/work/build/rexglue-host"; do
        if [[ -d "$candidate" ]]; then
            prefix_path="$candidate"
            break
        fi
    done
fi
[[ -n "$prefix_path" ]] || prefix_path="$sdk/out/mac-arm64"

cmake -G Ninja -S "$root/ts4-port" -B "$build" \
    -DCMAKE_BUILD_TYPE=RelWithDebInfo \
    -DTOPSPIN4_BUILD_TESTS=ON \
    -DTS4_WITH_REXGLUE=ON \
    -DREXSDK_DIR="$sdk" \
    -DCMAKE_PREFIX_PATH="$prefix_path" >/dev/null

ninja -C "$build" topspin4_host_tests

"$build/topspin4_host_tests" "$@"
