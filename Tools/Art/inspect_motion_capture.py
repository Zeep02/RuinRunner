"""Inspection-only contact sheet from real player captures and their position log.

Does not create or modify game art. Keeps the untouched frames beside the MP4.
"""
import argparse
import csv
from pathlib import Path

from PIL import Image, ImageDraw


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("capture", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--start", type=int, default=128)
    parser.add_argument("--count", type=int, default=20)
    parser.add_argument("--stride", type=int, default=1)
    parser.add_argument("--view-height", type=float, default=13.6)
    parser.add_argument("--viewport", nargs=4, type=int, help="Actual rendered x,y,width,height; raw source is retained")
    args = parser.parse_args()
    with (args.capture / "motion.csv").open(encoding="utf-8") as stream:
        rows = list(csv.reader(stream))
    columns, cell_width, cell_height = 5, 240, 236
    sheet = Image.new("RGB", (columns * cell_width, ((args.count + columns - 1) // columns) * cell_height), "#15261b")
    draw = ImageDraw.Draw(sheet)
    selected = rows[args.start:args.start + args.count * args.stride:args.stride]
    for index, row in enumerate(selected):
        frame = Image.open(args.capture / f"motion-{int(row[0]):04d}.png").convert("RGB")
        if args.viewport:
            vx,vy,vw,vh=args.viewport
            frame=frame.crop((vx,vy,vx+vw,vy+vh))
        ppu = frame.height / args.view_height
        camera_x = float(row[11]) if len(row) >= 13 else 0
        camera_y = float(row[12]) if len(row) >= 13 else 0
        x = round(frame.width / 2 + (float(row[2]) - camera_x) * ppu)
        y = round(frame.height / 2 - (float(row[3]) - camera_y) * ppu)
        crop = frame.crop((x - 60, y - 56, x + 60, y + 50)).resize((240, 212), Image.Resampling.NEAREST)
        left, top = index % columns * cell_width, index // columns * cell_height
        sheet.paste(crop, (left, top + 24))
        family = row[10] if len(row) >= 13 else "Sword"
        draw.text((left + 4, top + 4), f"{row[0]} {family} {row[1]} t={row[6]}", fill="white")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.output)


if __name__ == "__main__":
    main()
