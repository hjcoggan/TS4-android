#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${ANDROID_NDK_HOME:-}" ]]; then
    echo "ERROR: ANDROID_NDK_HOME is not set. Install the Android NDK and export ANDROID_NDK_HOME to its root directory." >&2
    exit 1
fi

PRESET="${1:-android-arm64-debug}"

cd "$(dirname "$0")/.."

cmake --preset "$PRESET"
cmake --build --preset "$PRESET"
