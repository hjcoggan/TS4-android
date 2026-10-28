"""scripts/compare_page.py: the before-and-after page of the image Enhancements."""

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from compare_page import as_shown, build, crop_box  # noqa: E402


def test_a_fractional_crop_box_fits_any_frame_size():
    assert crop_box((0.25, 0.5, 0.5, 0.25), (1280, 720)) == (320, 360, 960, 540)
    assert crop_box((0.25, 0.5, 0.5, 0.25), (2560, 1440)) == (640, 720, 1920, 1080)


def test_the_original_is_shown_at_the_enhanced_frame_size():
    original = Image.new("RGB", (1280, 720), (10, 20, 30))
    assert as_shown(original, (2560, 1440)).size == (2560, 1440)
    assert as_shown(original, (1280, 720)) is original


PIL_WARNING = "upscaled with PIL"


def test_a_presenter_dump_of_the_original_is_used_as_it_is(tmp_path):
    shown = Image.new("RGB", (2560, 1440), (10, 20, 30))
    shown.putpixel((5, 5), (200, 100, 50))
    shown.save(tmp_path / "presented.png")
    Image.new("RGB", (2560, 1440), (255, 255, 255)).save(tmp_path / "max.png")
    spec = {"out": "page", "pairs": [{"name": "Menu", "original": "presented.png",
                                      "variants": [{"name": "Maximum", "image": "max.png"}]}]}
    text = build(spec, tmp_path).read_text(encoding="utf-8")
    assert Image.open(tmp_path / "page/pair-00-original.png").getpixel((5, 5)) == (200, 100, 50)
    assert PIL_WARNING not in text


def test_an_original_of_another_size_is_upscaled_and_the_page_says_so(tmp_path):
    Image.new("RGB", (1280, 720), (0, 0, 0)).save(tmp_path / "guest.png")
    Image.new("RGB", (2560, 1440), (255, 255, 255)).save(tmp_path / "max.png")
    spec = {"out": "page", "pairs": [{"name": "Menu", "original": "guest.png",
                                      "variants": [{"name": "Maximum", "image": "max.png"}]}]}
    text = build(spec, tmp_path).read_text(encoding="utf-8")
    assert Image.open(tmp_path / "page/pair-00-original.png").size == (2560, 1440)
    assert PIL_WARNING in text


def test_each_variant_gets_a_slider_and_crops_against_the_original(tmp_path):
    for name, colour in (("o", 0), ("a", 120), ("b", 255)):
        Image.new("RGB", (1280, 720), (colour,) * 3).save(tmp_path / f"{name}.png")
    spec = {"out": "page", "pairs": [{
        "name": "Match", "original": "o.png",
        "variants": [{"name": "Scale 4x and MSAA 8x", "image": "a.png", "note": "same frame"},
                     {"name": "Maximum", "image": "b.png"}],
        "crops": [{"name": "net", "box": [0, 0, 0.5, 0.5]}]}]}
    text = build(spec, tmp_path).read_text(encoding="utf-8")
    for name in ("Scale 4x and MSAA 8x", "Maximum", "same frame", "net"):
        assert name in text
    for variant in (0, 1):
        assert (tmp_path / f"page/pair-00-variant{variant}.png").exists()
        assert (tmp_path / f"page/pair-00-crop0-variant{variant}.png").exists()
    assert (tmp_path / "page/pair-00-crop0-original.png").exists()


def test_the_page_holds_each_pair_its_crops_and_the_oracle(tmp_path):
    Image.new("RGB", (1280, 720), (0, 0, 0)).save(tmp_path / "a.png")
    Image.new("RGB", (2560, 1440), (255, 255, 255)).save(tmp_path / "b.png")
    Image.new("RGB", (1280, 720), (9, 9, 9)).save(tmp_path / "edge.png")
    spec = {"out": "page", "pairs": [{"name": "Exhibition", "original": "a.png",
                                      "variants": [{"name": "Enhanced", "image": "b.png"}],
                                      "oracle": "edge.png",
                                      "crops": [{"name": "net", "box": [0, 0, 0.5, 0.5]}]}]}
    page = build(spec, tmp_path)
    text = page.read_text(encoding="utf-8")
    assert "Exhibition" in text and "net" in text and "pair-00-oracle.png" in text
    assert Image.open(tmp_path / "page/pair-00-original.png").size == (2560, 1440)
    assert (tmp_path / "page/pair-00-crop0-variant0.png").exists()
