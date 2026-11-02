"""Extract tracked, nearest-neighbour character crops from a gameplay GIF.

This is a reference-analysis helper, not a runtime dependency.  It finds the
small red/white/dark player cluster, follows the closest plausible candidate,
and writes labelled contact sheets without resampling the source pixels.
"""

from __future__ import annotations

import argparse
import csv
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageSequence


def candidate_centres(rgb: np.ndarray) -> list[tuple[float, float, float]]:
    values = rgb.astype(np.int16)
    red, green, blue = values[..., 0], values[..., 1], values[..., 2]
    # The reference character has a compact pale face.  Tracking this is more
    # reliable than its brown tunic because the terrain uses similar browns.
    face_mask = (
        (red > 180)
        & (green > 150)
        & (blue > 130)
        & ((red - blue) < 100)
    ).astype(np.uint8)
    count, _, stats, centres = cv2.connectedComponentsWithStats(face_mask, 8)
    height, width = face_mask.shape
    results: list[tuple[float, float, float]] = []
    for index in range(1, count):
        x, y, w, h, area = stats[index]
        if area < 12 or area > 360 or y < 55 or y > height - 55:
            continue
        cx, cy = centres[index]
        x0, x1 = max(0, x - 24), min(width, x + w + 24)
        y0, y1 = max(0, y - 24), min(height, y + h + 24)
        patch = values[y0:y1, x0:x1]
        warm = (
            (patch[..., 0] > 105)
            & (patch[..., 0] > patch[..., 1] * 1.12)
            & (patch[..., 1] < 150)
        ).sum()
        dark = (patch.max(axis=2) < 85).sum()
        score = float(area * 5 + min(warm, 220) * 0.7 + min(dark, 320) * 0.2)
        results.append((float(cx), float(cy), score))
    return results


def track(frames: list[Image.Image]) -> list[tuple[float, float]]:
    centre = (frames[0].width * 0.5, frames[0].height * 0.5)
    positions: list[tuple[float, float]] = []
    for frame_index, frame in enumerate(frames):
        candidates = candidate_centres(np.asarray(frame.convert("RGB")))
        if candidates:
            ranked = []
            for cx, cy, visual_score in candidates:
                distance = np.hypot(cx - centre[0], cy - centre[1])
                maximum_step = 160 if frame_index == 0 else 60
                if distance <= maximum_step:
                    penalty = 1.0 if frame_index == 0 else 10.0
                    ranked.append((visual_score - distance * penalty, cx, cy))
            if ranked:
                _, cx, cy = max(ranked)
                centre = (cx, cy)
        positions.append(centre)
    return positions


def crop_at(frame: Image.Image, centre: tuple[float, float], size: int) -> Image.Image:
    half = size // 2
    cx, cy = (int(round(value)) for value in centre)
    canvas = Image.new("RGB", (size, size), (20, 20, 24))
    left, top = cx - half, cy - half
    source = frame.crop((left, top, left + size, top + size)).convert("RGB")
    canvas.paste(source, (0, 0))
    return canvas


def write_contacts(
    crops: list[Image.Image], output: Path, step: int, scale: int, columns: int
) -> None:
    chosen = list(range(0, len(crops), step))
    tile = crops[0].width * scale
    label_height = 26
    rows_per_page = 4
    per_page = columns * rows_per_page
    font = ImageFont.load_default(size=18)
    for page, start in enumerate(range(0, len(chosen), per_page), 1):
        indices = chosen[start : start + per_page]
        sheet = Image.new(
            "RGB", (columns * tile, rows_per_page * (tile + label_height)), (18, 18, 22)
        )
        draw = ImageDraw.Draw(sheet)
        for slot, frame_index in enumerate(indices):
            x = (slot % columns) * tile
            y = (slot // columns) * (tile + label_height)
            enlarged = crops[frame_index].resize((tile, tile), Image.Resampling.NEAREST)
            sheet.paste(enlarged, (x, y + label_height))
            draw.text((x + 4, y + 3), f"F{frame_index:03d}", font=font, fill="white")
        sheet.save(output / f"tracked-contact-{page:02d}.png")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("gif", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--crop", type=int, default=64)
    parser.add_argument("--step", type=int, default=2)
    parser.add_argument("--scale", type=int, default=4)
    parser.add_argument("--columns", type=int, default=8)
    args = parser.parse_args()

    args.output.mkdir(parents=True, exist_ok=True)
    with Image.open(args.gif) as source:
        frames = [frame.convert("RGB") for frame in ImageSequence.Iterator(source)]
    positions = track(frames)
    crops = [crop_at(frame, position, args.crop) for frame, position in zip(frames, positions)]

    crop_dir = args.output / "tracked-crops"
    crop_dir.mkdir(exist_ok=True)
    for index, crop in enumerate(crops):
        crop.save(crop_dir / f"crop-{index:03d}.png")
    write_contacts(crops, args.output, args.step, args.scale, args.columns)

    with (args.output / "tracked-centres.csv").open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(("frame", "x", "y"))
        writer.writerows((index, round(x, 2), round(y, 2)) for index, (x, y) in enumerate(positions))


if __name__ == "__main__":
    main()
