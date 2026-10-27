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


def test_the_page_holds_each_pair_its_crops_and_the_oracle(tmp_path):
    Image.new("RGB", (1280, 720), (0, 0, 0)).save(tmp_path / "a.png")
    Image.new("RGB", (2560, 1440), (255, 255, 255)).save(tmp_path / "b.png")
    Image.new("RGB", (1280, 720), (9, 9, 9)).save(tmp_path / "edge.png")
    spec = {"out": "page", "pairs": [{"name": "Exhibition", "original": "a.png", "enhanced": "b.png",
                                      "oracle": "edge.png",
                                      "crops": [{"name": "net", "box": [0, 0, 0.5, 0.5]}]}]}
    page = build(spec, tmp_path)
    text = page.read_text(encoding="utf-8")
    assert "Exhibition" in text and "net" in text and "pair-00-oracle.png" in text
    assert Image.open(tmp_path / "page/pair-00-original.png").size == (2560, 1440)
    assert (tmp_path / "page/pair-00-crop0-enhanced.png").exists()
