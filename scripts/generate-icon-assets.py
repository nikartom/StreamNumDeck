from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "StreamNumDeck.Wpf" / "Assets"


def extract_flat_mark(source: Path) -> Image.Image:
    image = Image.open(source).convert("RGB")
    grayscale = ImageOps.grayscale(image)
    mark = grayscale.point(lambda value: 255 if value >= 96 else 0, mode="L")
    bounds = mark.getbbox()
    if bounds is None:
        raise ValueError(f"No light numpad silhouette found in {source}")

    return mark.crop(bounds)


def compose(mark: Image.Image, width: int, height: int, height_ratio: float) -> Image.Image:
    max_width = round(width * (0.70 if width == height else 0.38))
    max_height = round(height * height_ratio)
    scale = min(max_width / mark.width, max_height / mark.height)
    size = (max(1, round(mark.width * scale)), max(1, round(mark.height * scale)))
    resized = mark.resize(size, Image.Resampling.LANCZOS)

    canvas = Image.new("L", (width, height), 0)
    offset = ((width - size[0]) // 2, (height - size[1]) // 2)
    canvas.paste(resized, offset)
    return Image.merge("RGB", (canvas, canvas, canvas))


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate the StreamNumDeck WPF application icon.")
    parser.add_argument(
        "source",
        nargs="?",
        type=Path,
        default=ASSETS / "AppIconSource-v1.png",
        help="Generated square source artwork.",
    )
    args = parser.parse_args()

    ASSETS.mkdir(parents=True, exist_ok=True)
    mark = extract_flat_mark(args.source)

    master = compose(mark, 1024, 1024, 0.76)
    master.save(ASSETS / "AppIconMaster.png", format="PNG", optimize=True)

    ico_source = master.convert("RGBA")
    ico_source.save(
        ASSETS / "AppIcon.ico",
        format="ICO",
        sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
               (48, 48), (64, 64), (128, 128), (256, 256)],
    )


if __name__ == "__main__":
    main()
