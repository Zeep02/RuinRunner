"""Turn the frames written by --enemy-art-tour into one sheet per enemy: each strike as a row, from warning through contact.

python Tools/Art/attack_tour_sheets_20261103.py <tour folder> <output folder>
"""
import csv
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

tour, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
font = ImageFont.truetype(r"C:\Windows\Fonts\malgun.ttf", 12)
CROP = 330
CELL = 210
OFFSETS = (-12, -6, 0, 2, 4, 7, 10, 13)
MAX_ROWS = 7


def nearest(frames, wanted, limit=6):
    best = min(frames, key=lambda f: abs(f - wanted))
    return best if abs(best - wanted) <= limit else None


for folder in sorted(glob.glob(os.path.join(tour, "*"))):
    if not os.path.isdir(folder) or not os.path.exists(os.path.join(folder, "frames.csv")):
        continue
    name = os.path.basename(folder)
    rows = list(csv.DictReader(open(os.path.join(folder, "frames.csv"), encoding="utf-8")))
    shots = {int(os.path.basename(p)[:-4]): p for p in glob.glob(os.path.join(folder, "[0-9]*.png"))}
    if not shots:
        continue
    strikes, last = [], "0"
    for row in rows:
        if row["phase"] == "1" and last != "1" and row["acting"] == "True":
            frame = int(row["frame"])
            if not strikes or frame - strikes[-1][0] > 18:
                strikes.append((frame, int(row["sx"]), int(row["sy"]), row["clip"]))
        last = row["phase"]
    # Spread the rows over the whole visit so every move has a chance to appear.
    step = max(1, len(strikes) / MAX_ROWS)
    chosen = [strikes[int(i * step)] for i in range(min(MAX_ROWS, len(strikes)))]
    sheet = Image.new("RGB", (len(OFFSETS) * (CELL + 2), max(1, len(chosen)) * (CELL + 2) + 18), (32, 34, 36))
    draw = ImageDraw.Draw(sheet)
    draw.text((4, 2), f"{name}: {len(strikes)} strikes, columns are frames {OFFSETS} from the strike at 30 fps", font=font, fill=(255, 240, 180))
    for r, (frame, sx, sy, clip) in enumerate(chosen):
        for c, offset in enumerate(OFFSETS):
            found = nearest(list(shots), frame + offset, 3)
            if found is None:
                continue
            try:
                image = Image.open(shots[found]).convert("RGB")
            except OSError:
                continue
            y = image.height - sy
            box = (sx - CROP // 2, y - CROP // 2 - 40, sx + CROP // 2, y + CROP // 2 - 40)
            sheet.paste(image.crop(box).resize((CELL, CELL), Image.NEAREST), (c * (CELL + 2), 18 + r * (CELL + 2)))
            draw.text((c * (CELL + 2) + 3, 20 + r * (CELL + 2)), f"{found}", font=font, fill=(255, 255, 255))
    sheet.save(os.path.join(out, name + ".png"))
    print(name, "strikes", len(strikes), "shots", len(shots))
    entrance = sorted(glob.glob(os.path.join(folder, "entrance-*.png")))
    if entrance:
        strip = Image.new("RGB", (6 * 322, 182), (32, 34, 36))
        for i, path in enumerate(entrance[:: max(1, len(entrance) // 6)][:6]):
            strip.paste(Image.open(path).convert("RGB").resize((320, 180), Image.BILINEAR), (i * 322, 1))
        strip.save(os.path.join(out, name + "-entrance.png"))
