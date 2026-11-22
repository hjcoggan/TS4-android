#!/usr/bin/env bash
# Top Spin 4 on Linux: what TopSpin4Activity.java does on Android, before the game starts.
#
#   TS4_GAME_DIR   the game's folder (default ~/Games/ts4), holding
#                    gamedata/ updatedata/   from scripts/prepare-gamedata.sh
#                    cache/topspin4.shaderpack from scripts/build-shaders.sh
#                    userdata/               saves, settings, display.conf, cvars.conf
#   TS4_BIN        the game binary (default: topspin4 beside this script, or ../libexec/ts4)
#   TS4_SHARE      where pipeline-keys.txt is (default ../share/ts4); copied into the game folder
#
# userdata/display.conf  width=, height=, refresh=, aspect=W:H  -> REX_VIDEO_MODE_*
# userdata/cvars.conf    name=value per line                     -> REX_<NAME>
# userdata/input_script.txt  a scripted pad (src/script_input.h) -> REX_TS4_INPUT_SCRIPT
#
# Values already in the environment (for example from a launcher's manifest) are kept unless
# one of these files sets them.
set -euo pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
game_dir=${TS4_GAME_DIR:-${HOME}/Games/ts4}
bin=${TS4_BIN:-}
if [ -z "$bin" ]; then
    for candidate in "$here/topspin4" "$here/../libexec/ts4/topspin4"; do
        if [ -x "$candidate" ]; then bin=$candidate; break; fi
    done
fi
[ -n "$bin" ] || { echo "ts4: no topspin4 binary beside $here (set TS4_BIN)" >&2; exit 2; }

mkdir -p "$game_dir"/{gamedata,updatedata,userdata,cache,logs}

# The shipped pipeline key list is read from the executable folder, which is the game folder
# here (src/native/pipelines.cpp, LoadShippedKeys).
share=${TS4_SHARE:-$here/../share/ts4}
if [ -f "$share/pipeline-keys.txt" ] && ! cmp -s "$share/pipeline-keys.txt" "$game_dir/pipeline-keys.txt"; then
    cp -f "$share/pipeline-keys.txt" "$game_dir/pipeline-keys.txt"
fi
export REX_APP_FOLDER=$game_dir
export REX_EXECUTABLE_FOLDER=$game_dir

trim() { local s=$1; s=${s#"${s%%[![:space:]]*}"}; s=${s%"${s##*[![:space:]]}"}; printf '%s' "$s"; }

# name=value lines, without blanks and # or ; comments.
read_conf() {
    local line
    while IFS= read -r line || [ -n "$line" ]; do
        line=$(trim "$line")
        case $line in ''|'#'*|';'*) continue ;; esac
        case $line in *=*) ;; *) continue ;; esac
        printf '%s\n' "$line"
    done < "$1"
}

display=$game_dir/userdata/display.conf
if [ -f "$display" ]; then
    width='' height='' refresh='' aspect=''
    while IFS= read -r line; do
        key=$(trim "${line%%=*}"); value=$(trim "${line#*=}")
        case ${key,,} in
            width) width=$value ;;
            height) height=$value ;;
            refresh) refresh=$value ;;
            aspect) aspect=$value ;;
        esac
    done < <(read_conf "$display")
    if [ -n "$aspect" ] && [ -n "$width" ] && [ -z "$height" ]; then
        num=${aspect%%:*}; den=${aspect##*:}
        if [ "$num" -gt 0 ] 2>/dev/null && [ "$den" -gt 0 ] 2>/dev/null; then
            height=$(( width * den / num ))
        fi
    fi
    [ -n "$width" ] && export REX_VIDEO_MODE_WIDTH=$width
    [ -n "$height" ] && export REX_VIDEO_MODE_HEIGHT=$height
    [ -n "$refresh" ] && export REX_VIDEO_MODE_REFRESH_RATE=$refresh
fi

cvars=$game_dir/userdata/cvars.conf
if [ -f "$cvars" ]; then
    while IFS= read -r line; do
        name=$(trim "${line%%=*}"); value=$(trim "${line#*=}")
        [ -n "$name" ] || continue
        export "REX_${name^^}=$value"
    done < <(read_conf "$cvars")
fi

script=$game_dir/userdata/input_script.txt
[ -f "$script" ] && export REX_TS4_INPUT_SCRIPT=$script

cd "$game_dir"
exec "$bin" "$@"
