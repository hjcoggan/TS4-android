"""Play a scene on the Classic renderer and check its captures repeat (ticket 09).

Adapted from WWE All Stars' scene runner (ADR-0004). A scene (tests/scenes/*.toml) is
scripted input from boot plus the guest frames to capture. Each run plays it with the scripted
pad (--ts4_input_script), dumps every capture frame (--ts4_dump_frames), exits after the last
one (--ts4_exit_after_frame) and writes a session report (--ts4_session_report;
src/frame_dumps.h). Frame numbers are the clock of the pad script's `at <frame>`.

The scene plays `runs` times (2 by default). A run fails when it does not exit on its own with a
report, a capture is missing or black, or the dumps' frame clock strays from the pad's
(run_failures). Each later run's captures are judged against
the first run's: per pixel NVIDIA FLIP, and a capture differs when a tile x tile block has more
than tile_hot_max pixels above hot_flip (WWE's tile rule). Mean FLIP is reported, never used to
decide. The Oracle comparison comes later.

With --native N (ticket 26), N Native runs follow the Classic ones. Each Native capture is judged
per pixel against the envelope of the Classic runs (the lowest FLIP error over them), with the
same tile rule, as in WWE's scene runner. A Native run also fails on any renderer fallback, draw
skip, Native debug cvar or Enhancement its session report shows (native_failures).

With --dual N (needs --native), N more runs draw every frame with both renderers at once
(--ts4_native_dual_compare): Classic presents and is dumped as usual, and Native writes its own
image of the same frame. Each such pair is judged with the tile rule, so a scene whose game state
differs between runs (a match: AI, load times) is still judged frame for frame. Native's own
images come before the presenter's gamma ramp; the ramp is read off the Native runs, where both
the presenter's and Native's own images of the same frames exist (fit_ramp). In this mode the
dual verdicts decide; the Classic-against-Classic and envelope verdicts are reported only.

Runs never overlap: the game animates by elapsed time, so a run slowed by another reaches a
different state at the same frame number. A lock (analysis/game-run.lock, shared with
scripts/smoke.ps1) refuses a second run while one is going.

    python scripts/scene_runner.py tests/scenes/boot-main-menu.toml [--out DIR] [--runs N]
                                   [--native N [--dual N]] [--shader-pack FILE]

Results and captures go to analysis/scenes/<name>/ (not committed).
Exit code: 0 pass, 1 fail, 2 the scene could not run.
"""
import argparse
import json
import msvcrt
import os
import shutil
import subprocess
import sys
import tomllib
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / "out/build/win-amd64-relwithdebinfo/topspin4.exe"
LOCK = ROOT / "analysis/game-run.lock"

# The judgement every scene uses unless its [check] overrides a value.
DEFAULTS = {
    "runs": 2,           # Classic runs; every later one is compared with the first
    "hot_flip": 0.5,     # a pixel is hot above this FLIP error
    "tile": 32,          # tile edge in pixels
    "tile_hot_max": 50,  # hot pixels a tile may hold
}


# A capture whose brightest pixel stays below this (0-255) is black, as in scripts/smoke.ps1.
BLACK_MAX = 16
# How far the guest's swaps may run ahead of the frames the GPU has shown.
MAX_FRAMES_IN_FLIGHT = 4


class SceneError(Exception):
    pass


class RunLockBusy(Exception):
    pass


class GameRunLock:
    """Held while the game runs, so two runs never overlap. scripts/smoke.ps1 opens the same
    file with FileShare.None: either side finding the other's handle is refused."""

    def __init__(self, path):
        self.path = Path(path)
        self.fd = None

    def __enter__(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        try:
            self.fd = os.open(self.path, os.O_RDWR | os.O_CREAT)
        except PermissionError as error:  # the smoke run holds it
            raise RunLockBusy(f"a game run is going ({self.path} is held)") from error
        try:
            msvcrt.locking(self.fd, msvcrt.LK_NBLCK, 1)
        except OSError as error:  # another scene run holds it
            os.close(self.fd)
            self.fd = None
            raise RunLockBusy(f"a game run is going ({self.path} is held)") from error
        return self

    def __exit__(self, *exc):
        os.lseek(self.fd, 0, os.SEEK_SET)
        msvcrt.locking(self.fd, msvcrt.LK_UNLCK, 1)
        os.close(self.fd)
        self.fd = None


@dataclass
class Scene:
    name: str
    input: str
    frames: list
    check: dict = field(default_factory=lambda: dict(DEFAULTS))
    log: list = field(default_factory=list)  # text every run's runtime.log must contain


def load_scene(text):
    try:
        data = tomllib.loads(text)
    except tomllib.TOMLDecodeError as error:
        raise SceneError(f"bad TOML: {error}") from error
    name = data.get("name")
    if not isinstance(name, str) or not name:
        raise SceneError("scene needs a non-empty 'name'")
    if data.get("from") != "boot":
        raise SceneError(f"scene '{name}': 'from' must be \"boot\" (the only start point so far)")
    script = data.get("input", "")
    if not isinstance(script, str):
        raise SceneError(f"scene '{name}': 'input' must be a string of scripted-pad lines")
    captures = data.get("capture")
    if not isinstance(captures, list) or not captures:
        raise SceneError(f"scene '{name}': needs at least one [[capture]] with a frame")
    frames = []
    for capture in captures:
        frame = capture.get("frame") if isinstance(capture, dict) else None
        if not isinstance(frame, int) or isinstance(frame, bool) or frame < 0:
            raise SceneError(f"scene '{name}': every [[capture]] needs frame = <guest frame >= 0>")
        frames.append(frame)
    check = dict(DEFAULTS)
    for key, value in data.get("check", {}).items():
        if key not in DEFAULTS:
            raise SceneError(f"scene '{name}': unknown [check] key '{key}' "
                             f"(known: {', '.join(DEFAULTS)})")
        if not isinstance(value, (int, float)) or isinstance(value, bool):
            raise SceneError(f"scene '{name}': [check] {key} must be a number")
        check[key] = value
    if check["runs"] < 2:
        raise SceneError(f"scene '{name}': [check] runs must be at least 2, so captures can be "
                         "compared")
    log = data.get("log", [])
    if not isinstance(log, list) or not all(isinstance(line, str) and line for line in log):
        raise SceneError(f"scene '{name}': 'log' must be a list of non-empty strings")
    return Scene(name, script, sorted(set(frames)), check, log)


def log_failures(log_text, expected):
    """What a run's log lacks: each expected text it does not contain (a missing log lacks all)."""
    return [f"log has no line with {text!r}" for text in expected
            if log_text is None or text not in log_text]


def run_failures(report, frames):
    """What fails one run, from its session report: no report (the game did not reach the last
    capture and exit on its own), or a capture frame it did not dump."""
    if report is None:
        return ["no session report (the run did not exit on its own)"]
    dumps = {dump["frame"]: dump for dump in report.get("dumps", [])}
    failures = []
    for frame in frames:
        if frame not in dumps:
            failures.append(f"no capture of frame {frame}")
        elif dumps[frame]["max_luma"] < BLACK_MAX:
            failures.append(f"capture of frame {frame} is black")
    # Dumps count the swaps the GPU shows, the pad the guest's VdSwap calls (patch 0031). Frame N
    # is swap N + 1, and the guest can be a few frames ahead of the GPU, never behind.
    last, swaps = report["exit_after_frame"], report["guest_swaps"]
    if swaps < last + 1:
        failures.append(f"frame clocks disagree: frame {last} shown after only {swaps} guest swaps")
    elif swaps > last + 1 + MAX_FRAMES_IN_FLIGHT:
        failures.append(f"frame clocks disagree: frame {last} shown with {swaps} guest swaps issued")
    return failures


def fit_ramp(pairs):
    """The presenter's gamma ramp as a 256-entry table per channel, from (Native's own image,
    the presenter's image of it) pairs, HxWx3 uint8. Values the pairs never show are
    interpolated. Raises SceneError when one input value maps to several outputs (no ramp)."""
    table = np.full((3, 256), -1, np.int32)
    for own, shown in pairs:
        for c in range(3):
            a = own[..., c].ravel().astype(np.int32)
            b = shown[..., c].ravel().astype(np.int32)
            lo = np.full(256, 256, np.int32)
            hi = np.full(256, -1, np.int32)
            np.minimum.at(lo, a, b)
            np.maximum.at(hi, a, b)
            seen = hi >= 0
            if np.any(hi[seen] - lo[seen] > 1):
                raise SceneError("the presenter's image is not a per-value ramp of Native's")
            known = table[c] >= 0
            if np.any(np.abs(table[c][known & seen] - lo[known & seen]) > 1):
                raise SceneError("the presenter's ramp differs between frames")
            table[c][seen] = lo[seen]
    xs = np.arange(256)
    out = np.zeros((3, 256), np.uint8)
    for c in range(3):
        known = table[c] >= 0
        if not known.any():
            raise SceneError("no pixels to read the presenter's ramp from")
        out[c] = np.round(np.interp(xs, xs[known], table[c][known])).astype(np.uint8)
    return out


def apply_ramp(image, ramp):
    """HxWx3 uint8 through a fit_ramp table."""
    return np.stack([ramp[c][image[..., c]] for c in range(3)], axis=-1)


def native_failures(report):
    """What fails a Native run, from its session report's "renderer" object (src/native): Native
    not active (a renderer fallback), any draw skip, a Native debug cvar, an Enhancement on."""
    if report is None:
        return []  # run_failures already says there is no report
    renderer = report.get("renderer")
    if renderer is None:
        return ["the session report has no renderer object"]
    failures = []
    if not renderer.get("native_active"):
        failures.append(f"Native was not active (renderer fallback: {renderer.get('fallback')})")
    for reason, record in sorted(renderer.get("draw_skips", {}).items()):
        failures.append(f"draw skip {reason}: {record['total']} from frame {record['first_frame']} "
                        f"(key {record['first_key']})")
    for name in renderer.get("debug_cvars", []):
        failures.append(f"debug cvar {name} is set")
    for enhancement in renderer.get("enhancements", []):
        if enhancement.get("on"):
            failures.append(f"Enhancement {enhancement['name']} is on")
    if renderer.get("gpu_failure"):
        failures.append(f"GPU failure {renderer['gpu_failure']}")
    return failures


def envelope_error(references, test, error_map=None):
    """Per-pixel error of test against several references: the lowest error over them, so a
    pixel the references disagree on passes when test matches any of them."""
    error_map = error_map or flip_map
    return np.minimum.reduce([error_map(reference, test) for reference in references])


def flip_map(reference, test):
    """Per-pixel FLIP error (0..1) of test against reference, both HxWx3 sRGB in 0..1."""
    import flip_evaluator as flip
    error, _, _ = flip.evaluate(reference, test, "LDR", applyMagma=False)
    return np.asarray(error, np.float32).squeeze()


def judge_capture(error, check):
    """WWE's tile rule over a per-pixel error map."""
    tile = int(check["tile"])
    hot = error > check["hot_flip"]
    h, w = hot.shape
    rows, cols = -(-h // tile), -(-w // tile)
    padded = np.zeros((rows * tile, cols * tile), bool)
    padded[:h, :w] = hot
    counts = padded.reshape(rows, tile, cols, tile).sum(axis=(1, 3))
    failing = np.argwhere(counts > check["tile_hot_max"])
    return {
        "passed": not len(failing),
        "worst_tile_hot": int(counts.max()),
        "failing_tiles": [[int(y), int(x)] for y, x in failing],
        "mean_flip": float(error.mean()),
    }


def repeatability(images, check, error_map=flip_map):
    """One capture's images in run order (HxWx3, 0..1): each later run judged against run 1,
    by label ("run-2", ...)."""
    return {f"run-{i + 1}": judge_capture(error_map(images[0], image), check)
            for i, image in enumerate(images[1:], start=1)}


def run_game(label, scene, out, exe, extra_flags=()):
    """One run that dumps every capture frame and exits after the last one; returns
    ({frame: png path}, session report or None)."""
    run_dir = out / label
    shutil.rmtree(run_dir, ignore_errors=True)
    run_dir.mkdir(parents=True)
    script = run_dir / "input.txt"
    script.write_text(scene.input.strip() + "\n")
    dumps = run_dir / "dumps"
    report_path = run_dir / "report.json"
    # Each run starts from a copy of the save data and cache, so no run sees another's saves.
    for name in ("userdata", "cache"):
        if (ROOT / name).exists():
            shutil.copytree(ROOT / name, run_dir / name)
    flags = ["--gpu_plugin=xenos", "--vulkan_device=-1",
             f"--game_data_root={ROOT / 'gamedata'}", f"--update_data_root={ROOT / 'updatedata'}",
             f"--user_data_root={run_dir / 'userdata'}", f"--cache_root={run_dir / 'cache'}",
             f"--log_file={run_dir / 'runtime.log'}", "--log_level=info",
             "--log_flush_interval=1", "--headless", "--window_hidden=true", "--audio_mute=true",
             f"--ts4_input_script={script}",
             f"--ts4_dump_frames={','.join(map(str, scene.frames))}",
             f"--ts4_dump_dir={dumps}", f"--ts4_exit_after_frame={max(scene.frames)}",
             f"--ts4_session_report={report_path}", *extra_flags]
    # Unattended: the window is never shown (--window_hidden, patch 0038), and the process starts
    # hidden too, so a run never takes the desktop's focus.
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 0  # SW_HIDE
    proc = subprocess.Popen([str(exe), *flags], cwd=exe.parent, startupinfo=startup,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    # The game presents 60 frames a second when it keeps up; allow half that, plus start-up.
    try:
        proc.wait(timeout=max(scene.frames) / 30 + 60)
    except subprocess.TimeoutExpired:
        print(f"  {label}: still running past the deadline, killed", flush=True)
        proc.kill()
        proc.wait()
    report = json.loads(report_path.read_text()) if report_path.exists() else None
    # The report names each dump's file; a run without one left nothing to judge.
    shots = {dump["frame"]: dumps / dump["file"] for dump in (report or {}).get("dumps", [])
             if (dumps / dump["file"]).exists()}
    print(f"  {label}: {len(shots)}/{len(scene.frames)} captures, exit code {proc.returncode}",
          flush=True)
    return shots, report


def load_image(path):
    return np.asarray(Image.open(path).convert("RGB"), np.float32) / 255


def read_log(path):
    return path.read_text(encoding="utf-8", errors="replace") if path.exists() else None


def load_rgb8(path):
    return np.asarray(Image.open(path).convert("RGB"), np.uint8)


def run_scene(scene, out, exe, native_runs=0, shader_pack=None, dual_runs=0):
    classic = [f"run-{i + 1}" for i in range(int(scene.check["runs"]))]
    native = [f"native-{i + 1}" for i in range(native_runs)]
    dual = [f"dual-{i + 1}" for i in range(dual_runs)]
    native_flags = ["--ts4_renderer=native"]
    if shader_pack:
        native_flags.append(f"--ts4_shader_pack={shader_pack}")
    frames = ",".join(map(str, scene.frames))

    def own_dump_flags(label):  # Native's own image of each capture, before the ramp
        return [f"--ts4_native_dump_frames={frames}",
                f"--ts4_native_dump_dir={out / label / 'native-dumps'}"]

    def own_image(label, frame):
        return out / label / "native-dumps" / f"frame-{frame:06}.ppm"

    shots, runs = {}, {}
    with GameRunLock(LOCK):
        for label in classic + native + dual:
            if label in native:
                flags = native_flags + (own_dump_flags(label) if dual else [])
            elif label in dual:
                flags = native_flags + ["--ts4_native_dual_compare=true"] + own_dump_flags(label)
            else:
                flags = ["--ts4_renderer=classic"]
            shots[label], report = run_game(label, scene, out, exe, flags)
            failures = (run_failures(report, scene.frames) +
                        log_failures(read_log(out / label / "runtime.log"), scene.log))
            if label in native or label in dual:
                failures += native_failures(report)
            runs[label] = {"report": report, "failures": failures}
    ramp = None
    if dual:
        pairs = [(load_rgb8(own_image(label, frame)), load_rgb8(shots[label][frame]))
                 for label in native for frame in scene.frames
                 if frame in shots[label] and own_image(label, frame).exists()]
        ramp = fit_ramp(pairs)
    captures = []
    for frame in scene.frames:
        missing = [label for label in classic + native + dual if frame not in shots[label]]
        missing += [f"{label} (Native's own image)" for label in dual
                    if not own_image(label, frame).exists()]
        if missing:
            captures.append({"frame": frame, "passed": False, "error": f"no capture from {missing}"})
            continue
        references = [load_image(shots[label][frame]) for label in classic]
        verdicts = repeatability(references, scene.check)
        for label in native:
            verdicts[label] = judge_capture(
                envelope_error(references, load_image(shots[label][frame])), scene.check)
        for label in dual:
            native_image = apply_ramp(load_rgb8(own_image(label, frame)), ramp)
            verdicts[label] = judge_capture(
                flip_map(load_image(shots[label][frame]), native_image.astype(np.float32) / 255),
                scene.check)
        deciding = dual if dual else list(verdicts)
        captures.append({"frame": frame,
                         "passed": all(verdicts[label]["passed"] for label in deciding),
                         "runs": verdicts,
                         "images": {label: shots[label][frame].as_posix()
                                    for label in classic + native + dual}})
    passed = all(c["passed"] for c in captures) and not any(r["failures"] for r in runs.values())
    result = {"scene": scene.name, "passed": passed, "check": scene.check, "runs": runs,
              "captures": captures}
    (out / "result.json").write_text(json.dumps(result, indent=1))
    return result


def main(argv):
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n")[0])
    parser.add_argument("scene", type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--exe", type=Path, default=EXE)
    parser.add_argument("--runs", type=int, help="override the scene's [check] runs (2 or more)")
    parser.add_argument("--native", type=int, default=0,
                        help="Native runs after the Classic ones, judged against them")
    parser.add_argument("--dual", type=int, default=0,
                        help="runs with both renderers at once, judged frame for frame "
                             "(needs --native)")
    parser.add_argument("--shader-pack", type=Path,
                        help="the shader pack Native runs load (default: the one in cache/)")
    args = parser.parse_args(argv)
    try:
        scene = load_scene(args.scene.read_text(encoding="utf-8"))
        if args.runs is not None:
            if args.runs < 2:
                raise SceneError("--runs must be at least 2")
            scene.check["runs"] = args.runs
    except (OSError, SceneError) as error:
        print(f"scene error: {error}", file=sys.stderr)
        return 2
    if not args.exe.exists():
        print(f"no game build at {args.exe}", file=sys.stderr)
        return 2
    out = (args.out or ROOT / "analysis/scenes" / scene.name).resolve()
    out.mkdir(parents=True, exist_ok=True)
    print(f"scene {scene.name}: frames {scene.frames}, {scene.check['runs']} runs, out {out}",
          flush=True)
    try:
        shader_pack = args.shader_pack.resolve() if args.shader_pack else None
        if args.dual and not args.native:
            raise SceneError("--dual needs --native, whose runs give the presenter's ramp")
        result = run_scene(scene, out, args.exe.resolve(), args.native, shader_pack, args.dual)
    except (RunLockBusy, SceneError) as error:
        print(f"scene error: {error}", file=sys.stderr)
        return 2
    for label, run in result["runs"].items():
        for failure in run["failures"]:
            print(f"  {label}: FAIL  {failure}")
    for c in result["captures"]:
        if "error" in c:
            print(f"  frame {c['frame']}: FAIL  {c['error']}")
            continue
        for label, v in c["runs"].items():
            against = ("Classic" if label.startswith("native") else
                       "its own Classic frame" if label.startswith("dual") else "run-1")
            print(f"  frame {c['frame']} {label} vs {against}: {'PASS' if v['passed'] else 'FAIL'}  "
                  f"worst tile {v['worst_tile_hot']} hot px (K={scene.check['tile_hot_max']}), "
                  f"mean FLIP {v['mean_flip']:.4f}")
    print(f"scene {scene.name}: {'PASS' if result['passed'] else 'FAIL'}  ({out / 'result.json'})")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
