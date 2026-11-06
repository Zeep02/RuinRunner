"""Original weapon beside its candidates, one sheet per family, with a check on whichever one is in the game.

python Tools/Art/compare_weapons_20261105.py <output folder>
"""
import glob
import json
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

import install_weapon_picks_20261105 as picks

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "Staging", "PixelLab")
FIRST_SET = os.path.join(ROOT, "20261103-weapons70")
FONT_FILES = glob.glob(os.path.join(HERE, "..", "..", "Assets", "**", "Galmuri11*.ttf"), recursive=True)
FONT = ImageFont.truetype(FONT_FILES[0], 12) if FONT_FILES else ImageFont.load_default()
BACK, CELL_BACK, ORIGINAL_BACK, CHOSEN_BACK = (40, 43, 52, 255), (58, 62, 74, 255), (86, 66, 58, 255), (44, 92, 62, 255)
TEXT, LABEL, GREEN = (235, 235, 225, 255), (255, 240, 150, 255), (110, 255, 140, 255)
SCALE = 3
MAX_OPTIONS = 5
# Where each family's candidates are. Greatsword batch 1 came back with several weapons overlapping in each cell, so it is not shown.
SOURCES = {
    "Dagger": ["20261105-weapons-clean/daggers_0"], "Katana": ["20261105-weapons-cool/Katana_0"], "Orb": ["20261105-weapons-cool/Orb_0"],
    "Greatsword": ["20261105-weapons-cool/Greatsword_0", "20261105-weapons-cool/Greatsword_2"],
    "Staff": ["20261105-weapons-cool/Staff_0", "20261105-weapons-cool/Staff_1"], "Spear": ["20261105-weapons-cool/Spear_0", "20261105-weapons-cool/Spear_1"],
}
GUN_SOURCES = {
    "scrap_revolver": "guns-short_0", "ricochet_pistol": "guns-short_0", "gate_shotgun": "guns-short_0", "furnace_sprayer": "guns-short_0", "meteor_waybill": "guns-short_0",
    "scout_carbine": "guns-long_0", "last_bell": "guns-long_0", "frost_bolt_rifle": "guns-three_0", "lightning_carbine": "guns-three_0", "siege_engineer": "guns-three_0",
}


def cropped(path):
    image = Image.open(path).convert("RGBA")
    return image.crop(image.getbbox()) if image.getbbox() else image


def first_version(weapon):
    """What the game had before 2026-11-05."""
    source = open(os.path.join(HERE, "build_weapons70.py"), encoding="utf-8").read()
    if weapon["id"] == "ember_claw":
        return cropped(os.path.join(ROOT, "20261103-ember_claw", "ember_claw.png"))
    pick = re.search(r'"' + weapon["id"] + r'":\("(\w+)",(\d+)(?:,"(\w+)")?\)', source)
    image = Image.open(os.path.join(FIRST_SET, pick.group(1), f"{int(pick.group(2)):02}.png")).convert("RGBA")
    if pick.group(3):
        half = image.width // 2
        image = image.crop((0, 0, half, image.height) if pick.group(3) == "left" else (half, 0, image.width, image.height))
    if weapon["id"] in ("meteor_waybill",):
        image = picks.largest_part(image)
    return image.crop(image.getbbox())


def options_for(weapon):
    weapon_id, family = weapon["id"], weapon["family"]
    folders = [f"20261105-weapons-clean/{GUN_SOURCES[weapon_id]}"] if weapon_id in GUN_SOURCES else SOURCES[family]
    found = []
    for folder in folders:
        job = json.load(open(os.path.join(ROOT, folder, "request.json"), encoding="utf-8"))
        at = job["order"].index(weapon_id)
        for frame in range(at * job["per_weapon"], (at + 1) * job["per_weapon"]):
            found.append((folder, frame))
    chosen = picks.PICKS.get(weapon_id)
    if chosen:
        found = [option for option in found if option != chosen[:2]]
        found.insert(0, chosen[:2])
    return found[:MAX_OPTIONS]


def check_mark(draw, x, y):
    draw.line((x, y + 6, x + 5, y + 11), fill=GREEN, width=3)
    draw.line((x + 5, y + 11, x + 14, y), fill=GREEN, width=3)


def sheet(title, weapons, out):
    rows = []
    for weapon in weapons:
        chosen = picks.PICKS.get(weapon["id"])
        cells = [("기존", first_version(weapon), chosen is None)]
        for folder, frame in options_for(weapon):
            is_chosen = chosen is not None and (folder, frame) == chosen[:2]
            art = picks.picked_image(weapon["id"]) if is_chosen else cropped(os.path.join(ROOT, folder, f"{frame:02}.png"))
            cells.append((str(frame), art, is_chosen))
        rows.append((weapon["name"], cells))
    widest = max(art.width for _, cells in rows for _, art, _ in cells)
    tallest = max(art.height for _, cells in rows for _, art, _ in cells)
    cell_w, cell_h = max(widest * SCALE + 14, 76), tallest * SCALE + 28
    name_w = 128
    columns = max(len(cells) for _, cells in rows)
    image = Image.new("RGBA", (name_w + columns * (cell_w + 6) + 12, 34 + len(rows) * (cell_h + 6) + 8), BACK)
    draw = ImageDraw.Draw(image)
    kept = sum(1 for _, cells in rows if cells[0][2])
    draw.text((8, 8), f"{title}  (초록 체크 = 게임에 들어간 그림, 교체 {len(rows) - kept} / 기존 유지 {kept})", fill=TEXT, font=FONT)
    for r, (name, cells) in enumerate(rows):
        y = 34 + r * (cell_h + 6)
        draw.text((8, y + cell_h // 2 - 8), name, fill=TEXT, font=FONT)
        for c, (label, art, is_chosen) in enumerate(cells):
            x = name_w + c * (cell_w + 6)
            draw.rectangle((x, y, x + cell_w, y + cell_h), fill=CHOSEN_BACK if is_chosen else ORIGINAL_BACK if c == 0 else CELL_BACK, outline=GREEN if is_chosen else None, width=2)
            big = art.resize((art.width * SCALE, art.height * SCALE), Image.NEAREST)
            image.alpha_composite(big, (x + (cell_w - big.width) // 2, y + 16 + (cell_h - 16 - big.height) // 2))
            draw.text((x + 4, y + 2), label, fill=LABEL, font=FONT)
            if is_chosen:
                check_mark(draw, x + cell_w - 20, y + 4)
    image.save(out)
    print(out, image.size, "kept", kept)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    weapons = json.load(open(os.path.join(HERE, "weapons70.json"), encoding="utf-8"))["weapons"]
    for family in SOURCES:
        sheet(family, [w for w in weapons if w["family"] == family], os.path.join(out, family + ".png"))
    sheet("Gun", [w for w in weapons if w["id"] in GUN_SOURCES], os.path.join(out, "Gun.png"))


if __name__ == "__main__":
    main()
