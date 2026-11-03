#!/usr/bin/env bash
set -euo pipefail

DEVICE="${ANDROID_SERIAL:-c27c2f60}"
PKG="com.ts4port.topspin4"
BASE="/sdcard/Android/data/${PKG}/files"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

for required in work/gamedata/default.xex work/shaderpack/topspin4.shaderpack; do
  [[ -f "$required" ]] || { echo "Missing $required" >&2; exit 2; }
done

adb -s "$DEVICE" shell "mkdir -p '$BASE/cache' '$BASE/drivers'; if [ ! -d '$BASE/userdata' ]; then mkdir '$BASE/userdata' && chmod a+rwX '$BASE/userdata'; fi"
adb -s "$DEVICE" push work/gamedata "$BASE/"
adb -s "$DEVICE" push work/updatedata "$BASE/"
adb -s "$DEVICE" push work/shaderpack/topspin4.shaderpack "$BASE/cache/topspin4.shaderpack"

# adb push creates shell-owned directories (mode 2770), which the app cannot
# traverse. Fix only trees created by adb push; existing app-owned directories
# such as userdata, cache, drivers, and logs remain untouched.
adb -s "$DEVICE" shell "chmod -R a+rwX '$BASE/gamedata' '$BASE/updatedata'"
