"""A local before-and-after page of the image Enhancements (image-quality ticket 10).

    python scripts/compare_page.py spec.json

scripts/scene_runner_shots.py writes the spec and runs this; this file stays usable alone. The spec
names the page's folder and its pairs: the same guest frame drawn with every image setting at its
default (the original) and with Enhancements on (each a variant), an optional Oracle frame of the
same scene point, and close-up crops given as fractions of the frame (x, y, width, height), so one
box fits any output size:

    {"out": "analysis/before-after/page", "title": "...", "intro": "...",
     "pairs": [{"name": "Exhibition, first point", "note": "...",
                "original": "presented.png",
                "variants": [{"name": "Resolution scale 3x and MSAA 8x", "image": "scaled.png",
                              "note": "the same frame"}],
                "oracle": "edge.png",
                "crops": [{"name": "far baseline", "box": [0.3, 0.3, 0.2, 0.1]}]}]}

The original should be the presenter's dump at the output size (--ts4_dump_presented), as a player
sees it. A frame of another size is scaled to the first variant's size with PIL (bilinear) and the
page says so. The page and every image go to the out folder; paths in the spec are relative to the
working folder. They are drawn from game data, so they stay local and are never committed
(ADR-0006).
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
    """The original frame at size with PIL's bilinear filter: a stand-in for the presenter's
    upscaling, for a frame that has no presenter dump."""
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
:root { color-scheme: light dark; --bg: #f6f6f4; --fg: #1d1d1b; --muted: #5d5d58; --warn: #a33b00; }
@media (prefers-color-scheme: dark) {
  :root { --bg: #171716; --fg: #ececea; --muted: #a4a49e; --warn: #ff9a5c; }
}
body { margin: 0 auto; max-width: 1400px; padding: 16px; background: var(--bg); color: var(--fg);
       font: 15px/1.5 system-ui, sans-serif; }
h2 { margin-top: 2.5em; }
.warn { color: var(--warn); }
.note { color: var(--muted); }
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


def open_rgb(root, path):
    return Image.open(root / path).convert("RGB")


def build_pair(index, pair, root, out, warnings):
    """One pair's section of the page; its images go to out."""
    stem = f"pair-{index:02}"
    variants = pair.get("variants") or []
    if not variants:
        raise ValueError(f"pair {pair['name']!r} has no variants")
    images = [open_rgb(root, variant["image"]) for variant in variants]
    size = images[0].size
    original = open_rgb(root, pair["original"])
    if original.size != size:
        warnings.append(f"{pair['name']}: the original ({original.width}x{original.height}) was "
                        f"upscaled with PIL (bilinear) to {size[0]}x{size[1]}, not by the "
                        "presenter: no presenter dump at the output size")
        original = as_shown(original, size)
    for i, image in enumerate(images):
        if image.size != size:
            warnings.append(f"{pair['name']}: {variants[i]['name']} ({image.width}x{image.height}) "
                            f"was resized with PIL to {size[0]}x{size[1]}")
            images[i] = as_shown(image, size)
    original.save(out / f"{stem}-original.png")
    parts = [f"<h2>{html.escape(pair['name'])}</h2>"]
    if pair.get("note"):
        parts.append(f'<p class="note">{html.escape(pair["note"])}</p>')
    for v, (variant, image) in enumerate(zip(variants, images)):
        image.save(out / f"{stem}-variant{v}.png")
        parts.append(f"<h3>{html.escape(variant['name'])}</h3>")
        note = variant.get("note")
        parts.append(f'<p class="note">{size[0]}x{size[1]}. Drag to compare.'
                     f'{" " + html.escape(note) if note else ""}</p>')
        parts.append(slider(f"{stem}-original.png", f"{stem}-variant{v}.png", "Original",
                            variant["name"]))
    crops = []
    for c, crop in enumerate(pair.get("crops", [])):
        zoomed(original, crop["box"]).save(out / f"{stem}-crop{c}-original.png")
        for v, (variant, image) in enumerate(zip(variants, images)):
            zoomed(image, crop["box"]).save(out / f"{stem}-crop{c}-variant{v}.png")
            crops.append(f"<figure>{slider(f'{stem}-crop{c}-original.png', f'{stem}-crop{c}-variant{v}.png', 'Original', variant['name'])}"
                         f"<figcaption>{html.escape(crop['name'])}: {html.escape(variant['name'])}"
                         f"</figcaption></figure>")
    if crops:
        parts.append("<h3>Close-ups</h3>")
        parts.append(f'<div class="crops">{"".join(crops)}</div>')
    if pair.get("oracle"):
        open_rgb(root, pair["oracle"]).save(out / f"{stem}-oracle.png")
        parts.append(f'<figure><img src="{stem}-oracle.png" alt="Oracle" style="max-width:100%">'
                     f"<figcaption>The Oracle (Xenia Edge) at the same scene point: the original "
                     f"look.</figcaption></figure>")
    return "\n".join(parts)


def build(spec, root):
    out = (root / spec["out"]).resolve()
    out.mkdir(parents=True, exist_ok=True)
    warnings = []
    sections = [build_pair(index, pair, root, out, warnings)
                for index, pair in enumerate(spec["pairs"])]
    title = html.escape(spec.get("title", "Image Enhancements: before and after"))
    alerts = "".join(f'<p class="warn">{html.escape(w)}</p>' for w in warnings)
    page = (f"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            f"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            f"<title>{title}</title><style>{STYLE}</style></head><body><h1>{title}</h1>"
            f"<p>{html.escape(spec.get('intro', ''))}</p>{alerts}{''.join(sections)}"
            f"<script>{SCRIPT}</script></body></html>")
    (out / "index.html").write_text(page, encoding="utf-8")
    for warning in warnings:
        print(f"warning: {warning}", file=sys.stderr)
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
