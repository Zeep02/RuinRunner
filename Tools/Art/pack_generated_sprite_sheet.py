"""Pack an ImageGen grid into fixed RGBA cells without resampling blur.

Image generation previews occasionally bake their transparency checker into
RGB.  This utility removes only bright near-neutral preview pixels, keeps the
generated source untouched, and repacks the requested grid for Unity slicing.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def clear_preview_background(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA")).copy()
    rgb = rgba[..., :3].astype(np.int16)
    maximum = rgb.max(axis=2)
    minimum = rgb.min(axis=2)
    neutral_bright = (minimum >= 178) & ((maximum - minimum) <= 18)
    rgba[neutral_bright, 3] = 0
    rgba[neutral_bright, :3] = 0
    return Image.fromarray(rgba, "RGBA")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--columns", type=int, required=True)
    parser.add_argument("--rows", type=int, required=True)
    parser.add_argument("--cell-size", type=int, default=64)
    args = parser.parse_args()

    source = clear_preview_background(Image.open(args.source))
    result = Image.new(
        "RGBA",
        (args.columns * args.cell_size, args.rows * args.cell_size),
        (0, 0, 0, 0),
    )
    for row in range(args.rows):
        top = round(row * source.height / args.rows)
        bottom = round((row + 1) * source.height / args.rows)
        for column in range(args.columns):
            left = round(column * source.width / args.columns)
            right = round((column + 1) * source.width / args.columns)
            cell = source.crop((left, top, right, bottom))
            cell.thumbnail((args.cell_size, args.cell_size), Image.Resampling.NEAREST)
            x = column * args.cell_size + (args.cell_size - cell.width) // 2
            y = row * args.cell_size + (args.cell_size - cell.height) // 2
            result.alpha_composite(cell, (x, y))

    args.output.parent.mkdir(parents=True, exist_ok=True)
    result.save(args.output, optimize=True)


if __name__ == "__main__":
    main()
