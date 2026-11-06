"""Turn the frames written by --sandbox-skills into one sheet per weapon: frames across the whole use, around the middle of the action.

python Tools/Art/skill_tour_sheets_20261105.py <tour folder> <output folder> [columns] [rows]
"""
import glob
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFont

tour, out = sys.argv[1], sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 6
rows = int(sys.argv[4]) if len(sys.argv) > 4 else 4
os.makedirs(out, exist_ok=True)
font = ImageFont.truetype(r"C:\Windows\Fonts\malgun.ttf", 12)
CELL = (300, 200)

for folder in sorted(glob.glob(os.path.join(tour, "*"))):
    if not os.path.isdir(folder):
        continue
    name = os.path.basename(folder)
    shots = sorted(glob.glob(os.path.join(folder, "[0-9]*.png")))
    if not shots:
        continue
    # Screenshots are written a frame late, so the final one or two may not be complete.
    good = []
    for path in shots:
        try:
            Image.open(path).verify()
            good.append(path)
        except OSError:
            pass
    wanted = cols * rows
    # Skip frames where nothing changes, so the sheet shows the action rather than the waiting.
    picks = [good[int(i * (len(good) - 1) / max(1, wanted - 1))] for i in range(wanted)]
    first = Image.open(picks[0]).convert("RGB")
    w, h = first.size
    crop_w, crop_h = int(w * .62), int(h * .62)
    box = ((w - crop_w) // 2 + int(w * .05), (h - crop_h) // 2, (w - crop_w) // 2 + int(w * .05) + crop_w, (h - crop_h) // 2 + crop_h)
    sheet = Image.new("RGB", (cols * (CELL[0] + 2), rows * (CELL[1] + 2) + 18), (32, 34, 36))
    draw = ImageDraw.Draw(sheet)
    draw.text((4, 2), f"{name}: {len(good)} frames at 24 fps", font=font, fill=(255, 240, 180))
    for i, path in enumerate(picks):
        x, y = (i % cols) * (CELL[0] + 2), 18 + (i // cols) * (CELL[1] + 2)
        sheet.paste(Image.open(path).convert("RGB").crop(box).resize(CELL, Image.BILINEAR), (x, y))
        draw.text((x + 3, y + 2), os.path.basename(path)[:-4], font=font, fill=(255, 255, 255))
    sheet.save(os.path.join(out, name + ".png"))
    print(name, len(good))
