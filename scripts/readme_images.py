"""The README's images (ADR-0012), cut from a scene_runner_shots.py run:

    python scripts/scene_runner_shots.py --scenes ... --out analysis/readme-shots
    dotnet run --project launcher/tools/Launcher.Screenshots -- analysis/launcher-shots ...
    python scripts/readme_images.py [--shots analysis/readme-shots] [--launcher analysis/launcher-shots]
                                    [--out docs/images]

Screenshots and the banner come from the max run (every image setting at its highest), the
comparison pairs from the dual run's Classic frame (the original) beside the max run's frame.
JPEG for full frames, PNG for the 200 % details (each pixel a 2x2 block); every file must stay
under the pre-commit guard's 512 KiB (scripts/repo_guard.py), so a JPEG too large is saved again
at a lower quality.
"""
import argparse
import io
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
LIMIT = 512 * 1024
WIDTH = 1280  # a full frame in the README, at 2x for a 640 px column

# (file, scene, frame) of the screenshot grid, from the max run.
SHOTS = [
    ("shot-match", "king-of-the-court", 4300),
    ("shot-select", "exhibition-match", 2400),
    ("shot-creator", "career-start", 2880),
    ("shot-academy", "academy-lesson", 2200),
]
# (name, scene, frame, detail box in the 2560x1440 frame: x, y, w, h)
PAIRS = [
    ("compare-match", "exhibition-match", 4200, (1440, 120, 400, 225)),
    ("compare-creator", "career-start", 2880, (1530, 180, 400, 225)),
]
# (file, Launcher.Screenshots page) of the Launcher grid, from its 200 % renders.
LAUNCHER = [
    ("launcher-home", "home"),
    ("launcher-setup", "setup"),
    ("launcher-settings", "settings"),
    ("launcher-achievements", "achievements"),
]
BANNER = ("king-of-the-court", 6400, (0, 300, 2560, 800))
FONT = ROOT / "launcher/src/Launcher.App/Assets/Fonts/Anton-Regular.ttf"


def frame(shots, scene, run, number):
    return Image.open(shots / "runs" / scene / run / "dumps" / f"frame-{number:06d}-presented.png").convert("RGB")


def save_jpeg(image, path):
    for quality in (88, 84, 80, 75, 70, 65):
        data = io.BytesIO()
        image.save(data, "JPEG", quality=quality, optimize=True, progressive=True)
        if data.tell() <= LIMIT:
            path.write_bytes(data.getvalue())
            return quality
    raise SystemExit(f"{path.name}: over {LIMIT} bytes even at quality 65")


def save_png(image, path):
    image.save(path, "PNG", optimize=True)
    if path.stat().st_size > LIMIT:
        raise SystemExit(f"{path.name}: {path.stat().st_size} bytes, over {LIMIT}")


def fit(image, width):
    return image.resize((width, round(image.height * width / image.width)), Image.Resampling.LANCZOS)


def detail(image, box):
    x, y, w, h = box
    return image.crop((x, y, x + w, y + h)).resize((w * 2, h * 2), Image.Resampling.NEAREST)


def banner(image, box):
    x, y, w, h = box
    art = fit(image.crop((x, y, x + w, y + h)), 1920)
    # A dark band at the bottom so the name reads over the court.
    shade = Image.new("L", art.size, 0)
    draw = ImageDraw.Draw(shade)
    for row in range(art.height):
        draw.line([(0, row), (art.width, row)], fill=int(200 * max(0, row / art.height - 0.35) / 0.65))
    art = Image.composite(Image.new("RGB", art.size, (6, 22, 36)), art, shade)
    draw = ImageDraw.Draw(art)
    title = ImageFont.truetype(str(FONT), 132)
    sub = ImageFont.truetype(str(FONT), 40)
    draw.text((72, art.height - 250), "TOP SPIN 4 RECOMP", font=title, fill=(255, 255, 255))
    draw.text((76, art.height - 92), "AN UNOFFICIAL PC PORT OF THE XBOX 360 GAME", font=sub, fill=(250, 190, 40))
    return art


def main(argv):
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n")[0])
    parser.add_argument("--shots", type=Path, default=ROOT / "analysis/readme-shots")
    parser.add_argument("--launcher", type=Path, default=ROOT / "analysis/launcher-shots")
    parser.add_argument("--out", type=Path, default=ROOT / "docs/images")
    args = parser.parse_args(argv)
    args.out.mkdir(parents=True, exist_ok=True)
    written = []
    for name, scene, number in SHOTS:
        save_jpeg(fit(frame(args.shots, scene, "max", number), WIDTH), args.out / f"{name}.jpg")
        written.append(f"{name}.jpg")
    for name, scene, number, box in PAIRS:
        for side, run in (("original", "dual"), ("pc", "max")):
            image = frame(args.shots, scene, run, number)
            save_jpeg(fit(image, WIDTH), args.out / f"{name}-{side}.jpg")
            save_png(detail(image, box), args.out / f"{name}-{side}-zoom.png")
            written += [f"{name}-{side}.jpg", f"{name}-{side}-zoom.png"]
    for name, page in LAUNCHER:
        image = Image.open(args.launcher / f"{page}-200.png").convert("RGB")
        save_jpeg(fit(image, WIDTH), args.out / f"{name}.jpg")
        written.append(f"{name}.jpg")
    scene, number, box = BANNER
    save_jpeg(banner(frame(args.shots, scene, "max", number), box), args.out / "banner.jpg")
    written.append("banner.jpg")
    for name in written:
        print(f"{args.out / name}  {(args.out / name).stat().st_size // 1024} KiB")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
