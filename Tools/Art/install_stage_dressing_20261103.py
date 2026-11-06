"""Fetch the stage dressing candidates, draw a review sheet, and install the chosen ones.

python Tools/Art/install_stage_dressing_20261103.py fetch      download every candidate and write Staging/.../<Theme>-sheet.png
python Tools/Art/install_stage_dressing_20261103.py install    cut each chosen candidate to its visible pixels and install it

Nothing is resampled. Flat details pivot at their centre, standing props at the middle of their lowest row.
"""
import json
import os
import re
import sys
import uuid

import numpy
import requests
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from install_remaining_enemies_20261103 import ART, TEMPLATE, folder_meta, without_specks, write_meta

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261103-dressing")
OWNER = "5ba0435a-6d49-45f7-aa17-9ce34a6bd358"
THEMES = ("Outer", "Flooded", "Core")
# Which round of the two on the sheet each item uses, and any item left out entirely, chosen on the review sheets.
PICKS = json.load(open(os.path.join(ROOT, "picks.json"), encoding="utf-8")) if os.path.exists(os.path.join(ROOT, "picks.json")) else {}


def object_id(theme):
    request = json.load(open(os.path.join(ROOT, theme + ".request.json"), encoding="utf-8"))
    return re.search(r"id: ([0-9a-f-]{36})", request["response"]).group(1), request


def fetch():
    font = ImageFont.truetype(r"C:\Windows\Fonts\malgun.ttf", 11)
    for theme in THEMES:
        oid, request = object_id(theme)
        folder = os.path.join(ROOT, "candidates", theme)
        os.makedirs(folder, exist_ok=True)
        for i in range(len(request["order"]) * 2):
            path = os.path.join(folder, f"{i:02d}.png")
            if not os.path.exists(path):
                response = requests.get(f"https://backblaze.pixellab.ai/file/pixellab-characters/objects/{OWNER}/{oid}/rotations/frame_{i}.png", timeout=60)
                response.raise_for_status()
                open(path, "wb").write(response.content)
        order = request["order"]
        cell = 96
        sheet = Image.new("RGB", (8 * (cell + 2), 4 * (cell + 16)), (70, 74, 66))
        draw = ImageDraw.Draw(sheet)
        for round_index in range(2):
            for n, name in enumerate(order):
                slot = n + round_index * len(order)
                image = Image.open(os.path.join(folder, f"{slot:02d}.png")).convert("RGBA")
                big = image.resize((image.width * 3, image.height * 3), Image.NEAREST)
                x = (n % 8) * (cell + 2)
                y = (n // 8 * 2 + round_index) * (cell + 16)
                sheet.paste(big, (x, y + 14), big)
                draw.text((x + 2, y), f"{name} {round_index}", font=font, fill=(255, 240, 180))
        sheet.save(os.path.join(ROOT, theme + "-sheet.png"))
        print(theme, "sheet written")


def without_outline(image):
    """A dark rim makes something lying on the floor look like a sticker; it should sink into the ground."""
    pixels = numpy.array(image)
    alpha = pixels[..., 3] > 40
    open_side = ~alpha
    edge = alpha & (numpy.roll(open_side, 1, 0) | numpy.roll(open_side, -1, 0) | numpy.roll(open_side, 1, 1) | numpy.roll(open_side, -1, 1))
    dark = pixels[..., :3].astype(int).sum(axis=2) < 200
    pixels[edge & dark] = 0
    return Image.fromarray(pixels, "RGBA")


def install():
    target_root = os.path.join(ART, "Campaign", "Dressing")
    os.makedirs(target_root, exist_ok=True)
    folder_meta(target_root)
    for theme in THEMES:
        _, request = object_id(theme)
        order = request["order"]
        target = os.path.join(target_root, theme)
        os.makedirs(target, exist_ok=True)
        folder_meta(target)
        for n, name in enumerate(order):
            kind, key = name.split("/")
            use = PICKS.get(theme, {}).get(name, 0)
            slot = n + use * len(order)
            image = without_specks(Image.open(os.path.join(ROOT, "candidates", theme, f"{slot:02d}.png")).convert("RGBA"))
            if kind == "flat":
                image = without_outline(image)
            image = image.crop(image.getbbox())
            path = os.path.join(target, f"{kind}-{key}.png")
            image.save(path)
            write_meta(path, .5, .5 if kind == "flat" else round(1 / (image.height * 2), 5))
            print(theme, name, slot, image.size)


if __name__ == "__main__":
    {"fetch": fetch, "install": install}[sys.argv[1]]()
