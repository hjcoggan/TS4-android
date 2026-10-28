"""The before-and-after screenshots of the image Enhancements (image-quality ticket 10), from one
command:

    python scripts/scene_runner_shots.py [--size 2560x1440] [--scenes a,b] [--out DIR]
                                         [--oracle SCENE:FRAME=PNG[@X,Y,W,H]] [--shader-pack FILE]

Each scene (tests/scenes/*.toml, default SCENES) plays twice from boot with the scripted pad,
headless and hidden, in a window of --size (16:9, default 2560x1440):

  dual   Both renderers draw every frame (--ts4_native_dual_compare). Classic presents, so the
         presenter's dump of it at the output size (--ts4_dump_presented) is the ORIGINAL as a
         player sees it: the 1280x720 frame upscaled by the presenter. Native, at Resolution scale
         3x and 8x MSAA (MATCHED), writes its own image of the same guest frame, put through the
         presenter's gamma ramp and shown at the output size. Same run, same frame: camera,
         players and timing match exactly, also in a match. These two Enhancements are Native
         only, so the original is untouched by them.
  max    Native alone with every image Enhancement at its maximum (MAXIMUM: also 16x anisotropic
         filtering and High CAS). The presenter's dump at the output size is what a player sees
         with them on, CAS included. A separate run: in a live scene (a match) the moment can
         differ a little from the dual run's.

Anisotropic filtering and sharpening apply to both renderers and CAS runs in the presenter, which
presents Classic in a dual run, so only the max run shows them; the dual run keeps the original
pure.

The page (compare_page.py: a slider per variant, the scene's close-up crops, an Oracle frame where
one is given) opens from OUT/page/index.html (default analysis/before-after). Runs and captures
are in OUT/runs/<scene>/. Everything is drawn from game data: local, never committed (ADR-0006).

The presenter's dump at the output size needs rex::ui::Presenter::CapturePresentedOutput (see
src/frame_dumps.h). Without it the page falls back to PIL's bilinear upscale of the guest output,
says so, and the command exits 1.

Exit code: 0 every run clean and every image from the presenter, 1 a failure (the page is still
built from what exists), 2 the runs could not start.
"""
import argparse
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))

import compare_page  # noqa: E402
from scene_runner import (EXE, LOCK, ROOT, GameRunLock, RunLockBusy, SceneError, apply_ramp,  # noqa: E402
                          fit_ramp, load_rgb8, load_scene, native_failures, run_failures,
                          run_game, shrink_to)

# The scenes of the 1.0 scope (title and menus, Exhibition, Career, Academy, King of the Court) and
# the practice ground, which has an Oracle frame.
SCENES = ["boot-main-menu", "exhibition-match", "career-start", "academy-lesson",
          "king-of-the-court", "practice-ground"]
DEFAULT_SIZE = "2560x1440"

# Native-only Enhancements: the original beside them stays pure in the same run.
MATCHED = {"ts4_resolution_scale": "3", "ts4_msaa": "8"}
MAXIMUM = {**MATCHED, "ts4_anisotropic_filtering": "16", "ts4_sharpening": "high"}

# The Oracle frame of the practice ground's scene point: Edge's window shot (ticket 02), cropped to
# the game view.
DEFAULT_ORACLES = [
    "practice-ground:1300=analysis/scenes/iq02/edge-practice-original/shot-040s.png@8,93,1280,720",
]


def parse_size(text):
    """'2560x1440' as (2560, 1440); the presenter fits a 16:9 frame, so only 16:9 sizes make a
    like-for-like pair."""
    try:
        width, height = (int(part) for part in text.lower().split("x"))
    except ValueError as error:
        raise SceneError(f"--size {text!r} is not WIDTHxHEIGHT") from error
    if width <= 0 or height <= 0 or width * 9 != height * 16:
        raise SceneError(f"--size {text!r} is not a 16:9 size (e.g. 1920x1080, 2560x1440, "
                         "3840x2160)")
    return width, height


def parse_oracle(text):
    """'scene:frame=path[@x,y,w,h]' as (scene, frame, path, pixel crop or None)."""
    head, sep, tail = text.partition("=")
    scene, colon, frame = head.partition(":")
    if not sep or not colon or not scene or not frame.isdigit() or not tail:
        raise SceneError(f"--oracle {text!r} is not SCENE:FRAME=PNG[@X,Y,W,H]")
    path, at, crop = tail.rpartition("@")
    if not at:
        return scene, int(frame), Path(tail), None
    try:
        x, y, w, h = (int(part) for part in crop.split(","))
    except ValueError as error:
        raise SceneError(f"--oracle {text!r}: the crop is not X,Y,W,H in pixels") from error
    return scene, int(frame), Path(path), (x, y, w, h)


def window_flags(size):
    """The hidden window's size, and the presenter's dump at it. Fullscreen is off so the size
    is the one asked for, not the desktop's."""
    width, height = size
    return ["--fullscreen=false", f"--window_width={width}", f"--window_height={height}",
            f"--ts4_dump_presented={width}x{height}"]


def own_dump_flags(frames, directory):
    """Native's own image of each frame, before the presenter's gamma ramp."""
    return [f"--ts4_native_dump_frames={','.join(map(str, frames))}",
            f"--ts4_native_dump_dir={directory}"]


def run_flags(kind, size, frames, run_dir, shader_pack=None):
    """The game flags of a 'dual' or 'max' run (on top of scene_runner.run_game's)."""
    flags = ["--ts4_renderer=native"]
    if shader_pack:
        flags.append(f"--ts4_shader_pack={shader_pack}")
    if kind == "dual":
        flags.append("--ts4_native_dual_compare=true")
    enhance = MATCHED if kind == "dual" else MAXIMUM
    return (flags + own_dump_flags(frames, run_dir / "native-dumps") + window_flags(size) +
            [f"--{key}={value}" for key, value in enhance.items()])


def presented_files(report, dumps):
    """{frame: (path, width, height)} of each dump the report says has a presented image."""
    found = {}
    for dump in (report or {}).get("dumps", []):
        name = dump.get("presented_file")
        if name and (dumps / name).exists():
            found[dump["frame"]] = (dumps / name, dump["presented_width"], dump["presented_height"])
    return found


def size_failures(label, report, presented, size, guest_size_matters):
    """Where an image is not the output size: a presented dump of another size means the window
    came up differently; for a Native-alone run so does the guest output (Native fits its frame
    to the window), and a frame of another size cannot be a like-for-like pair."""
    wanted = list(size)
    failures = []
    for frame, (_, width, height) in sorted(presented.items()):
        if [width, height] != wanted:
            failures.append(f"{label}: frame {frame} presented at {width}x{height}, not "
                            f"{size[0]}x{size[1]}")
    if guest_size_matters:
        for dump in (report or {}).get("dumps", []):
            if [dump["width"], dump["height"]] != wanted:
                failures.append(f"{label}: frame {dump['frame']} is {dump['width']}x"
                                f"{dump['height']} at the guest output, not {size[0]}x{size[1]}: "
                                "the window did not come up at the size asked for")
    return failures


@dataclass
class SceneShots:
    """What a scene's runs left, by guest frame."""
    name: str
    labels: dict
    crops: dict
    original: dict = field(default_factory=dict)  # presented dump of Classic (dual run)
    matched: dict = field(default_factory=dict)   # Native at MATCHED, graded (dual run)
    maximum: dict = field(default_factory=dict)   # presented dump of Native at MAXIMUM (max run)
    oracle: dict = field(default_factory=dict)
    fallbacks: list = field(default_factory=list)  # images that are not the presenter's


def describe(enhance):
    names = {"ts4_resolution_scale": "Resolution scale {}x", "ts4_msaa": "MSAA {}x",
             "ts4_anisotropic_filtering": "{}x anisotropic filtering",
             "ts4_sharpening": "{} CAS sharpening"}
    return ", ".join(names[key].format(value) for key, value in enhance.items())


def scene_pairs(shots):
    """The compare page's pairs of a scene: one per frame with an original, in frame order."""
    pairs = []
    for frame in sorted(shots.original):
        variants = []
        if frame in shots.matched:
            variants.append({
                "name": describe(MATCHED), "image": shots.matched[frame].as_posix(),
                "note": "Native, the same frame of the same run as the original: only the "
                        "Enhancements differ."})
        if frame in shots.maximum:
            variants.append({
                "name": f"Maximum: {describe(MAXIMUM)}", "image": shots.maximum[frame].as_posix(),
                "note": "As the presenter shows it, CAS included. A separate run of the scene: in "
                        "a live scene the moment can differ a little from the original's."})
        if not variants:
            continue
        pair = {"name": f"{shots.name}: {shots.labels.get(frame, f'frame {frame}')} "
                        f"(guest frame {frame})",
                "original": shots.original[frame].as_posix(), "variants": variants,
                "crops": shots.crops.get(frame, [])}
        if frame in shots.oracle:
            pair["oracle"] = shots.oracle[frame].as_posix()
        pairs.append(pair)
    return pairs


def grade(own_image, ramp, size):
    """Native's own image through the presenter's ramp, at the output size (a whole fraction
    shrunk with a box filter; the window normally made it that size already)."""
    height, width = size[1], size[0]
    return apply_ramp(shrink_to(own_image, (height, width)), ramp)


def capture_scene(scene, out, exe, size, shader_pack=None, runner=run_game):
    """Play both runs of one scene; returns (SceneShots, {label: failures}, {label: report})."""
    runs = {"dual": MATCHED, "max": MAXIMUM}
    shots = SceneShots(scene.name, scene.labels, scene.crops)
    failures, reports, guest = {}, {}, {}
    presented = {}
    for kind, enhance in runs.items():
        run_dir = out / kind
        flags = run_flags(kind, size, scene.frames, run_dir, shader_pack)
        guest[kind], reports[kind] = runner(kind, scene, out, exe, flags, scene.frames)
        problems = run_failures(reports[kind], scene.frames)
        problems += native_failures(reports[kind], enhance)
        presented[kind] = presented_files(reports[kind], run_dir / "dumps")
        problems += size_failures(kind, reports[kind], presented[kind], size, kind == "max")
        missing = [f for f in scene.frames if f not in presented[kind]]
        if missing:
            problems.append(f"{kind}: no presented image of frames {missing} (the presenter "
                            "lacks CapturePresentedOutput, or the dump failed)")
        failures[kind] = problems
    # The presenter's gamma ramp, from the max run: its Native frames exist as Native's own
    # image and as the presenter's guest output (both at the output size, before CAS).
    pairs = [(load_rgb8(run_dir_own(out, "max", f)), load_rgb8(guest["max"][f]))
             for f in scene.frames if f in guest["max"] and run_dir_own(out, "max", f).exists()]
    ramp = None
    if any(own.shape != shown.shape for own, shown in pairs):
        failures["max"].append("no gamma ramp for this scene: Native's own image and its guest "
                               "output are of different sizes")
    elif pairs:
        try:
            ramp = fit_ramp(pairs)
        except SceneError as error:
            failures["max"].append(f"no gamma ramp for this scene: {error}")
    graded = out / "dual" / "native-graded"
    graded.mkdir(parents=True, exist_ok=True)
    for frame in scene.frames:
        original = presented["dual"].get(frame)
        if original:
            shots.original[frame] = original[0]
        elif frame in guest["dual"]:
            shots.original[frame] = guest["dual"][frame]
            shots.fallbacks.append(f"{scene.name} frame {frame}: the original is the guest output "
                                   "(no presenter dump)")
        own = run_dir_own(out, "dual", frame)
        if own.exists() and ramp is not None:
            try:
                path = graded / f"frame-{frame:06}.png"
                Image.fromarray(grade(load_rgb8(own), ramp, size)).save(path)
                shots.matched[frame] = path
            except SceneError as error:
                failures["dual"].append(f"frame {frame}: Native's own image: {error}")
        elif frame in guest["dual"]:
            failures["dual"].append(f"frame {frame}: no Native image to show beside the original")
        maximum = presented["max"].get(frame)
        if maximum:
            shots.maximum[frame] = maximum[0]
        elif frame in guest["max"]:
            shots.maximum[frame] = guest["max"][frame]
            shots.fallbacks.append(f"{scene.name} frame {frame}: the maximum frame is the guest "
                                   "output, without CAS (no presenter dump)")
    return shots, failures, reports


def run_dir_own(out, kind, frame):
    return out / kind / "native-dumps" / f"frame-{frame:06}.ppm"


def prepare_oracles(specs, out, names):
    """Crop each --oracle frame to the game view and save it under out; {scene: {frame: path}}.
    A spec whose file is missing is skipped (the default one needs ticket 02's Edge run)."""
    found = {}
    for spec in specs:
        scene, frame, path, crop = parse_oracle(spec)
        if scene not in names:
            continue
        path = path if path.is_absolute() else ROOT / path
        if not path.exists():
            print(f"oracle frame {path} not found; skipped", flush=True)
            continue
        image = Image.open(path).convert("RGB")
        if crop:
            x, y, w, h = crop
            image = image.crop((x, y, x + w, y + h))
        target = out / "oracle" / f"{scene}-{frame:06}.png"
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target)
        found.setdefault(scene, {})[frame] = target
    return found


def build_page(all_shots, out, size):
    """Write the compare page of every scene's pairs; returns its index.html path."""
    pairs = [pair for shots in all_shots for pair in scene_pairs(shots)]
    spec = {
        "out": (out / "page").as_posix(),
        "title": "Image Enhancements: before and after",
        "intro": (f"Output {size[0]}x{size[1]}. Original: every setting at its default, as the "
                  "presenter shows the game's 1280x720 frame. Drag the slider to reveal each "
                  "Enhancement. Local only: drawn from game data, never committed."),
        "pairs": pairs}
    (out / "page-spec.json").write_text(json.dumps(spec, indent=1), encoding="utf-8")
    return compare_page.build(spec, Path.cwd())  # every path in the spec is absolute


def main(argv):
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n")[0])
    parser.add_argument("--scenes", default=",".join(SCENES),
                        help="comma-separated scene names from tests/scenes (default: %(default)s)")
    parser.add_argument("--size", default=DEFAULT_SIZE, help="the output size, 16:9")
    parser.add_argument("--out", type=Path, default=ROOT / "analysis/before-after")
    parser.add_argument("--exe", type=Path, default=EXE)
    parser.add_argument("--shader-pack", type=Path)
    parser.add_argument("--oracle", action="append", default=None, metavar="SCENE:FRAME=PNG[@X,Y,W,H]",
                        help="an Oracle frame of a scene point, cropped to the game view "
                             "(default: the practice ground's, from ticket 02, when it exists)")
    args = parser.parse_args(argv)
    try:
        size = parse_size(args.size)
        names = [name for name in args.scenes.split(",") if name]
        scenes = []
        for name in names:
            path = ROOT / "tests/scenes" / f"{name}.toml"
            if not path.exists():
                raise SceneError(f"no scene {name} ({path})")
            scenes.append(load_scene(path.read_text(encoding="utf-8")))
        oracle_specs = args.oracle if args.oracle is not None else DEFAULT_ORACLES
        for spec in oracle_specs:
            parse_oracle(spec)
    except (OSError, SceneError) as error:
        print(f"scene error: {error}", file=sys.stderr)
        return 2
    if not args.exe.exists():
        print(f"no game build at {args.exe}", file=sys.stderr)
        return 2
    out = args.out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    oracles = prepare_oracles(oracle_specs, out, names)
    print(f"screenshots: {', '.join(names)} at {size[0]}x{size[1]}, out {out}", flush=True)
    all_shots, failed, summary = [], False, {}
    try:
        with GameRunLock(LOCK):
            for scene in scenes:
                print(f"scene {scene.name}: frames {scene.frames}", flush=True)
                shots, failures, reports = capture_scene(
                    scene, out / "runs" / scene.name, args.exe.resolve(), size,
                    args.shader_pack.resolve() if args.shader_pack else None)
                shots.oracle = oracles.get(scene.name, {})
                all_shots.append(shots)
                summary[scene.name] = {"failures": failures, "fallbacks": shots.fallbacks}
                for kind, problems in failures.items():
                    for problem in problems:
                        print(f"  {scene.name} {kind}: FAIL  {problem}")
                    failed = failed or bool(problems)
                failed = failed or bool(shots.fallbacks)
                for fallback in shots.fallbacks:
                    print(f"  {fallback}")
    except (RunLockBusy, SceneError) as error:
        print(f"scene error: {error}", file=sys.stderr)
        return 2
    (out / "result.json").write_text(json.dumps(summary, indent=1), encoding="utf-8")
    page = build_page(all_shots, out, size)
    print(f"comparison page: {page}")
    print(f"screenshots: {'FAIL' if failed else 'done'}  ({out / 'result.json'})")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
