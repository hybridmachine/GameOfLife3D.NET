#!/usr/bin/env python3
"""Generates the sample textures used by the ready-to-import materials in this
directory (checker.pbr.json / checker.mtlx). Run with the repo venv:

    ./.venv/bin/python resources/materials/generate.py

Outputs (next to this script):
    checker_albedo.png    256x256 two-tone checkerboard
    checker_normal.png    flat tangent-space normal map (128, 128, 255)
    checker_roughness.png left-to-right roughness gradient (0.05 -> 0.9)
"""

from pathlib import Path

from PIL import Image

SIZE = 256
CELLS = 8  # checker cells per row/column
OUT_DIR = Path(__file__).resolve().parent


def make_albedo() -> Image.Image:
    image = Image.new("RGB", (SIZE, SIZE))
    cell = SIZE // CELLS
    for y in range(SIZE):
        for x in range(SIZE):
            on = (x // cell + y // cell) % 2 == 0
            image.putpixel((x, y), (235, 235, 235) if on else (45, 48, 60))
    return image


def make_normal() -> Image.Image:
    # Flat tangent-space normal: (0, 0, 1) remapped to [0, 255].
    return Image.new("RGB", (SIZE, SIZE), (128, 128, 255))


def make_roughness() -> Image.Image:
    image = Image.new("L", (SIZE, SIZE))
    lo, hi = round(0.05 * 255), round(0.9 * 255)
    for y in range(SIZE):
        for x in range(SIZE):
            value = lo + (hi - lo) * x // (SIZE - 1)
            image.putpixel((x, y), value)
    return image.convert("RGB")


def main() -> None:
    make_albedo().save(OUT_DIR / "checker_albedo.png")
    make_normal().save(OUT_DIR / "checker_normal.png")
    make_roughness().save(OUT_DIR / "checker_roughness.png")
    print(f"Wrote 3 textures to {OUT_DIR}")


if __name__ == "__main__":
    main()
