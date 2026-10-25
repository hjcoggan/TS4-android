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
decide. Native against Classic (ticket 26 on) and the Oracle comparison come later.

Runs never overlap: the game animates by elapsed time, so a run slowed by another reaches a
different state at the same frame number. A lock (analysis/game-run.lock, shared with
scripts/smoke.ps1) refuses a second run while one is going.

    python scripts/scene_runner.py tests/scenes/boot-main-menu.toml [--out DIR] [--runs N]

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
    return Scene(name, script, sorted(set(frames)), check)


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


def run_game(label, scene, out, exe):
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
             "--log_flush_interval=1", "--headless", "--audio_mute=true",
             f"--ts4_input_script={script}",
             f"--ts4_dump_frames={','.join(map(str, scene.frames))}",
             f"--ts4_dump_dir={dumps}", f"--ts4_exit_after_frame={max(scene.frames)}",
             f"--ts4_session_report={report_path}"]
    proc = subprocess.Popen([str(exe), *flags], cwd=exe.parent,
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


def run_scene(scene, out, exe):
    labels = [f"run-{i + 1}" for i in range(int(scene.check["runs"]))]
    shots, runs = {}, {}
    with GameRunLock(LOCK):
        for label in labels:
            shots[label], report = run_game(label, scene, out, exe)
            runs[label] = {"report": report, "failures": run_failures(report, scene.frames)}
    captures = []
    for frame in scene.frames:
        missing = [label for label in labels if frame not in shots[label]]
        if missing:
            captures.append({"frame": frame, "passed": False, "error": f"no capture from {missing}"})
            continue
        verdicts = repeatability([load_image(shots[label][frame]) for label in labels],
                                 scene.check)
        captures.append({"frame": frame, "passed": all(v["passed"] for v in verdicts.values()),
                         "runs": verdicts,
                         "images": {label: shots[label][frame].as_posix() for label in labels}})
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
        result = run_scene(scene, out, args.exe.resolve())
    except RunLockBusy as error:
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
            print(f"  frame {c['frame']} {label} vs run-1: {'PASS' if v['passed'] else 'FAIL'}  "
                  f"worst tile {v['worst_tile_hot']} hot px (K={scene.check['tile_hot_max']}), "
                  f"mean FLIP {v['mean_flip']:.4f}")
    print(f"scene {scene.name}: {'PASS' if result['passed'] else 'FAIL'}  ({out / 'result.json'})")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
