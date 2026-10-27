"""A local before-and-after page of the image Enhancements (image-quality ticket 10).

    python scripts/compare_page.py spec.json

The spec names the page's folder and its pairs: the same guest frame drawn with every image setting
at its default (original) and at its maximum (enhanced), with an optional Oracle frame of the same
scene point and close-up crops given as fractions of the frame (x, y, width, height), so one box
fits any output size:

    {"out": "analysis/before-after", "title": "...",
     "pairs": [{"name": "Exhibition, first point", "original": "a.png", "enhanced": "b.png",
                "oracle": "edge.png", "crops": [{"name": "far baseline", "box": [0.3, 0.3, 0.2, 0.1]}]}]}

The original frame is 1280x720; it is scaled up to the enhanced frame's size with a bilinear
filter, as the presenter shows it in a window of that size. The page and every image go to the out
folder. They are drawn from game data, so they stay local and are never committed (ADR-0006).
"""
import html
import json
import sys
from pathlib import Path

from PIL import Image

# Crops are shown at this many pixels wide, scaled up without filtering so pixels stay visible.
CROP_WIDTH = 640


def crop_box(box, size):
    """A fractional (x, y, w, h) box as pixel (left, top, right, bottom) in an image of size."""
    x, y, w, h = box
    width, height = size
    return (round(x * width), round(y * height), round((x + w) * width), round((y + h) * height))


def as_shown(original, size):
    """The original frame as the presenter shows it in a window of size: bilinear scaling."""
    return original if original.size == size else original.resize(size, Image.Resampling.BILINEAR)


def zoomed(image, box):
    """A crop scaled up to CROP_WIDTH with nearest-neighbour filtering."""
    region = image.crop(crop_box(box, image.size))
    factor = max(1, CROP_WIDTH // max(region.width, 1))
    return region.resize((region.width * factor, region.height * factor), Image.Resampling.NEAREST)


def slider(left, right, label_left, label_right):
    """Two images stacked, the right one revealed by a range input."""
    return f"""<div class="slider">
  <img src="{left}" alt="{html.escape(label_left)}">
  <div class="reveal"><img src="{right}" alt="{html.escape(label_right)}"></div>
  <input type="range" min="0" max="100" value="50" aria-label="Reveal {html.escape(label_right)}">
  <span class="tag left">{html.escape(label_left)}</span>
  <span class="tag right">{html.escape(label_right)}</span>
</div>"""


STYLE = """
:root { color-scheme: light dark; --bg: #f6f6f4; --fg: #1d1d1b; --muted: #5d5d58; }
@media (prefers-color-scheme: dark) { :root { --bg: #171716; --fg: #ececea; --muted: #a4a49e; } }
body { margin: 0 auto; max-width: 1400px; padding: 16px; background: var(--bg); color: var(--fg);
       font: 15px/1.5 system-ui, sans-serif; }
h2 { margin-top: 2.5em; }
.slider { position: relative; display: inline-block; max-width: 100%; line-height: 0; }
.slider img { max-width: 100%; display: block; image-rendering: pixelated; }
.slider .reveal { position: absolute; inset: 0; overflow: hidden; clip-path: inset(0 0 0 50%); }
.slider .reveal img { width: 100%; }
.slider input { position: absolute; left: 0; right: 0; bottom: 8px; width: 100%; margin: 0; }
.tag { position: absolute; top: 8px; padding: 2px 6px; background: #000a; color: #fff;
       font-size: 13px; line-height: 1.4; }
.tag.left { left: 8px; } .tag.right { right: 8px; }
.crops { display: flex; flex-wrap: wrap; gap: 16px; }
figure { margin: 0; } figcaption { color: var(--muted); }
"""

SCRIPT = """
for (const s of document.querySelectorAll('.slider')) {
  const reveal = s.querySelector('.reveal'), input = s.querySelector('input');
  input.addEventListener('input', () => { reveal.style.clipPath = `inset(0 0 0 ${input.value}%)`; });
}
"""


def build(spec, root):
    out = (root / spec["out"]).resolve()
    out.mkdir(parents=True, exist_ok=True)
    sections = []
    for index, pair in enumerate(spec["pairs"]):
        enhanced = Image.open(root / pair["enhanced"]).convert("RGB")
        original = as_shown(Image.open(root / pair["original"]).convert("RGB"), enhanced.size)
        stem = f"pair-{index:02}"
        original.save(out / f"{stem}-original.png")
        enhanced.save(out / f"{stem}-enhanced.png")
        parts = [f"<h2>{html.escape(pair['name'])}</h2>",
                 f"<p>{enhanced.width}x{enhanced.height}. Drag to compare.</p>",
                 slider(f"{stem}-original.png", f"{stem}-enhanced.png", "Original", "Enhanced")]
        crops = []
        for c, crop in enumerate(pair.get("crops", [])):
            for kind, image in (("original", original), ("enhanced", enhanced)):
                zoomed(image, crop["box"]).save(out / f"{stem}-crop{c}-{kind}.png")
            crops.append(f"<figure>{slider(f'{stem}-crop{c}-original.png', f'{stem}-crop{c}-enhanced.png', 'Original', 'Enhanced')}"
                         f"<figcaption>{html.escape(crop['name'])}</figcaption></figure>")
        if crops:
            parts.append(f'<div class="crops">{"".join(crops)}</div>')
        if pair.get("oracle"):
            Image.open(root / pair["oracle"]).convert("RGB").save(out / f"{stem}-oracle.png")
            parts.append(f'<figure><img src="{stem}-oracle.png" alt="Oracle" style="max-width:100%">'
                         f"<figcaption>The Oracle (Xenia Edge) at the same scene point: the original "
                         f"look.</figcaption></figure>")
        sections.append("\n".join(parts))
    title = html.escape(spec.get("title", "Image Enhancements: before and after"))
    page = (f"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            f"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            f"<title>{title}</title><style>{STYLE}</style></head><body><h1>{title}</h1>"
            f"<p>{html.escape(spec.get('intro', ''))}</p>{''.join(sections)}"
            f"<script>{SCRIPT}</script></body></html>")
    (out / "index.html").write_text(page, encoding="utf-8")
    return out / "index.html"


def main(argv):
    if len(argv) != 1:
        print(__doc__, file=sys.stderr)
        return 2
    spec_path = Path(argv[0]).resolve()
    page = build(json.loads(spec_path.read_text(encoding="utf-8")), Path.cwd())
    print(f"comparison page: {page}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
