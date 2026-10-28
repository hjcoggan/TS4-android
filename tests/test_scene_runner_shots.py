"""scripts/scene_runner_shots.py: the one command that makes ticket 10's screenshots. The game is
not started here: a fake runner leaves the files a run would."""

import json
import sys
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from scene_runner import SceneError, load_scene  # noqa: E402
from scene_runner_shots import (MATCHED, MAXIMUM, SCENES, SceneShots, build_page,  # noqa: E402
                                capture_scene, parse_oracle, parse_size, presented_files,
                                prepare_oracles, run_flags, scene_pairs, size_failures)

SIZE = (64, 36)  # a 16:9 output, small for the tests
SCENE = load_scene("""
name = "menu"
from = "boot"
input = ""
[[capture]]
frame = 10
label = "the menu"
crops = [{ name = "title", box = [0, 0, 0.5, 0.5] }]
[[capture]]
frame = 20
""")


def test_an_output_size_is_16_by_9():
    assert parse_size("2560x1440") == (2560, 1440)
    assert parse_size("1920X1080") == (1920, 1080)
    for bad in ("1280x1024", "2560", "axb", "0x0", "-16x-9"):
        with pytest.raises(SceneError):
            parse_size(bad)


def test_an_oracle_frame_names_its_scene_point_and_may_crop_the_window_shot():
    assert parse_oracle("practice-ground:1300=a/b.png") == ("practice-ground", 1300,
                                                            Path("a/b.png"), None)
    assert parse_oracle("practice-ground:1300=a/b.png@8,93,1280,720") == (
        "practice-ground", 1300, Path("a/b.png"), (8, 93, 1280, 720))
    for bad in ("a=b.png", "a:x=b.png", "a:1=", "a:1=b.png@1,2,3"):
        with pytest.raises(SceneError):
            parse_oracle(bad)


def test_the_dual_run_keeps_the_original_pure_and_the_max_run_turns_everything_on(tmp_path):
    dual = run_flags("dual", SIZE, [10, 20], tmp_path / "dual")
    maximum = run_flags("max", SIZE, [10, 20], tmp_path / "max")
    assert "--ts4_native_dual_compare=true" in dual
    assert "--ts4_native_dual_compare=true" not in maximum
    for key, value in MATCHED.items():
        assert f"--{key}={value}" in dual
    assert not any("anisotropic" in f or "sharpening" in f for f in dual)
    for key, value in MAXIMUM.items():
        assert f"--{key}={value}" in maximum
    for flags in (dual, maximum):
        assert "--ts4_renderer=native" in flags
        assert "--window_width=64" in flags and "--window_height=36" in flags
        assert "--ts4_dump_presented=64x36" in flags
        assert "--fullscreen=false" in flags
        assert "--ts4_native_dump_frames=10,20" in flags


def test_a_report_names_the_presented_dumps_that_exist(tmp_path):
    (tmp_path / "frame-000010-presented.png").write_bytes(b"x")
    report = {"dumps": [
        {"frame": 10, "presented_file": "frame-000010-presented.png", "presented_width": 64,
         "presented_height": 36},
        {"frame": 20, "presented_file": "frame-000020-presented.png", "presented_width": 64,
         "presented_height": 36},  # the file is not there
        {"frame": 30}]}
    assert presented_files(report, tmp_path) == {10: (tmp_path / "frame-000010-presented.png", 64, 36)}
    assert presented_files(None, tmp_path) == {}


def test_a_frame_of_another_size_than_the_output_fails_the_run():
    report = {"dumps": [{"frame": 10, "width": 1280, "height": 720}]}
    presented = {10: (Path("p.png"), 1280, 720)}
    assert size_failures("max", report, presented, SIZE, guest_size_matters=False) != []
    failures = size_failures("max", report, {}, SIZE, guest_size_matters=True)
    assert len(failures) == 1 and "window did not come up" in failures[0]
    ok = {"dumps": [{"frame": 10, "width": 64, "height": 36}]}
    assert size_failures("max", ok, {10: (Path("p.png"), 64, 36)}, SIZE, True) == []


def test_a_pair_per_frame_with_the_original_in_frame_order_and_the_oracle():
    shots = SceneShots("menu", {10: "the menu"}, {10: [{"name": "title", "box": [0, 0, 0.5, 0.5]}]})
    shots.original = {20: Path("/o20.png"), 10: Path("/o10.png"), 30: Path("/o30.png")}
    shots.matched = {10: Path("/m10.png"), 20: Path("/m20.png")}
    shots.maximum = {10: Path("/x10.png")}
    shots.oracle = {20: Path("/e20.png")}
    pairs = scene_pairs(shots)
    assert [p["name"] for p in pairs] == ["menu: the menu (guest frame 10)",
                                          "menu: frame 20 (guest frame 20)"]  # 30 has no variant
    first, second = pairs
    assert first["original"] == "/o10.png"
    assert [v["image"] for v in first["variants"]] == ["/m10.png", "/x10.png"]
    assert first["variants"][0]["name"] == "Resolution scale 3x, MSAA 8x"
    assert first["variants"][1]["name"].startswith("Maximum")
    assert first["crops"][0]["name"] == "title"
    assert "oracle" not in first and second["oracle"] == "/e20.png"
    assert [v["image"] for v in second["variants"]] == ["/m20.png"]


def test_the_scenes_the_command_plays_exist():
    for name in SCENES:
        load_scene((ROOT / "tests/scenes" / f"{name}.toml").read_text(encoding="utf-8"))


def test_every_default_scene_capture_has_a_label_and_the_matches_have_crops():
    for name in SCENES:
        text = (ROOT / "tests/scenes" / f"{name}.toml").read_text(encoding="utf-8")
        scene = load_scene(text)
        assert all(not label.startswith("frame ") for label in scene.labels.values()), name
        assert scene.crops, name


# A fake game: leaves the files a run would, at the sizes the window should have.

def gradient(size, shift=0):
    width, height = size
    ramp = (np.arange(width * height) % 256).reshape(height, width)
    return np.stack([ramp, (ramp + shift) % 256, ramp], axis=-1).astype(np.uint8)


def fake_runner(presented=True, window=SIZE):
    """Stands in for scene_runner.run_game; window is the size the 'window' came up at."""
    calls = []

    def run(label, scene, out, exe, flags, frames):
        calls.append((label, flags))
        run_dir = out / label
        (run_dir / "dumps").mkdir(parents=True)
        (run_dir / "native-dumps").mkdir()
        dumps, shots = [], {}
        guest_size = (1280, 720) if label == "dual" else window
        for frame in frames:
            own = gradient(window, frame)
            Image.fromarray(own).save(run_dir / "native-dumps" / f"frame-{frame:06}.ppm")
            guest = run_dir / "dumps" / f"frame-{frame:06}.png"
            Image.fromarray(gradient(guest_size, frame) if label == "dual" else own).save(guest)
            shots[frame] = guest
            entry = {"frame": frame, "file": guest.name, "width": guest_size[0],
                     "height": guest_size[1], "max_luma": 200}
            if presented:
                name = f"frame-{frame:06}-presented.png"
                Image.fromarray(gradient(SIZE, frame + 1)).save(run_dir / "dumps" / name)
                entry |= {"presented_file": name, "presented_width": SIZE[0],
                          "presented_height": SIZE[1]}
            dumps.append(entry)
        asked = MATCHED if label == "dual" else MAXIMUM
        report = {"exit_after_frame": max(frames), "guest_swaps": max(frames) + 1, "dumps": dumps,
                  "renderer": {"native_active": True, "draw_skips": {}, "debug_cvars": [],
                               "enhancements": [{"name": name, "on": True} for name in asked]}}
        return shots, report

    run.calls = calls
    return run


def test_a_scene_leaves_the_original_the_matched_frame_and_the_maximum_at_the_output_size(tmp_path):
    runner = fake_runner()
    shots, failures, _ = capture_scene(SCENE, tmp_path, Path("game.exe"), SIZE, runner=runner)
    assert failures == {"dual": [], "max": []} and shots.fallbacks == []
    assert [label for label, _ in runner.calls] == ["dual", "max"]
    for frame in (10, 20):
        for image in (shots.original[frame], shots.matched[frame], shots.maximum[frame]):
            assert Image.open(image).size == SIZE
        # the original and the maximum are the presenter's dumps; the matched frame is Native's
        # own image through the ramp (the identity here)
        assert shots.original[frame].name.endswith("-presented.png")
        assert shots.maximum[frame].name.endswith("-presented.png")
        assert np.array_equal(np.asarray(Image.open(shots.matched[frame])), gradient(SIZE, frame))
    page = build_page([shots], tmp_path, SIZE)
    text = page.read_text(encoding="utf-8")
    assert "the menu" in text and "title" in text and "upscaled with PIL" not in text
    spec = json.loads((tmp_path / "page-spec.json").read_text(encoding="utf-8"))
    assert len(spec["pairs"]) == 2 and len(spec["pairs"][0]["variants"]) == 2


def test_without_a_presenter_dump_the_guest_output_stands_in_and_the_run_fails(tmp_path):
    shots, failures, _ = capture_scene(SCENE, tmp_path, Path("game.exe"), SIZE,
                                       runner=fake_runner(presented=False))
    assert any("no presented image" in f for f in failures["dual"] + failures["max"])
    assert len(shots.fallbacks) == 4  # two frames, the original and the maximum of each
    page = build_page([shots], tmp_path, SIZE).read_text(encoding="utf-8")
    assert "upscaled with PIL" in page  # the 1280x720 original, scaled to the output size


def test_a_window_that_came_up_at_another_size_fails_the_max_run(tmp_path):
    _, failures, _ = capture_scene(SCENE, tmp_path, Path("game.exe"), SIZE,
                                   runner=fake_runner(window=(128, 72)))
    assert any("window did not come up" in f for f in failures["max"])


def test_an_oracle_frame_is_cropped_to_the_game_view(tmp_path):
    shot = tmp_path / "edge.png"
    Image.new("RGB", (40, 30), (1, 2, 3)).save(shot)
    found = prepare_oracles([f"menu:10={shot}@4,6,16,9", "other:1=x.png"], tmp_path / "out",
                            ["menu"])
    assert set(found) == {"menu"}
    assert Image.open(found["menu"][10]).size == (16, 9)
    assert prepare_oracles([f"menu:10={tmp_path / 'missing.png'}"], tmp_path / "out", ["menu"]) == {}
