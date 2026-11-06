"""Download the PixelLab candidates for the 20 passive accessories, then install the chosen ones.

python Tools/Art/install_passive_accessories_20261104.py fetch     writes candidates/ and candidates-sheet.png
python Tools/Art/install_passive_accessories_20261104.py install   cuts each pick to its visible pixels and writes icon and .meta
Nothing is resampled. A new .meta copies an existing icon's import settings under a fresh GUID.
"""
import json
import os
import re
import sys
import urllib.request
import uuid

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from install_remaining_enemies_20261103 import ART, without_specks

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261104-passive-accessories")
URL = "https://backblaze.pixellab.ai/file/pixellab-characters/objects/5ba0435a-6d49-45f7-aa17-9ce34a6bd358/caa9b3fa-ed52-42ea-9206-7635e67600bb/rotations/frame_{}.png"
ORDER = ["thunder_charm", "shadow_twin_ring", "cinder_seed", "frost_wave_necklace", "meteor_compass", "poison_censer", "chain_spark",
         "guardian_feather", "retribution_thorn", "soul_lantern", "quake_bell", "mirror_shard", "blood_pact", "time_hourglass",
         "gravity_pearl", "storm_core", "phoenix_ash", "swap_flash", "vine_snare", "holy_tithe"]
# The list was generated three times over; 0 is the first round, 1 the second, 2 the third.
ROUND = {"chain_spark": 1, "mirror_shard": 2, "swap_flash": 1, "soul_lantern": 1, "holy_tithe": 1}


def fetch():
    os.makedirs(os.path.join(ROOT, "candidates"), exist_ok=True)
    cell, scale = 28, 4
    sheet = Image.new("RGB", (20 * (cell * scale + 6) + 6, 3 * (cell * scale + 6) + 6), (58, 62, 72))
    draw = ImageDraw.Draw(sheet)
    for slot in range(60):
        path = os.path.join(ROOT, "candidates", f"{slot:02d}.png")
        if not os.path.exists(path):
            request = urllib.request.Request(URL.format(slot), headers={"User-Agent": "RuinRunner-art-tools"})
            with urllib.request.urlopen(request) as response, open(path, "wb") as out:
                out.write(response.read())
        image = Image.open(path).convert("RGBA").resize((cell * scale, cell * scale), Image.NEAREST)
        x, y = 6 + slot % 20 * (cell * scale + 6), 6 + slot // 20 * (cell * scale + 6)
        sheet.paste(image, (x, y), image)
        draw.text((x + 2, y + 1), str(slot), fill=(255, 255, 160))
    sheet.save(os.path.join(ROOT, "candidates-sheet.png"))


def install():
    target = os.path.join(ART, "Equipment", "Accessory")
    template = open(os.path.join(target, "rush_spur.png.meta"), encoding="utf-8", newline="").read()
    picked = {}
    for n, name in enumerate(ORDER):
        slot = n + 20 * ROUND.get(name, 0)
        picked[name] = slot
        image = without_specks(Image.open(os.path.join(ROOT, "candidates", f"{slot:02d}.png")).convert("RGBA"))
        path = os.path.join(target, name + ".png")
        image.crop(image.getbbox()).save(path)
        if not os.path.exists(path + ".meta"):
            with open(path + ".meta", "w", encoding="utf-8", newline="") as out:
                out.write(re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, template, count=1))
        print(name, slot, image.getbbox())
    json.dump(dict(tool="create_1_direction_object", date="2026-11-04", object="caa9b3fa-ed52-42ea-9206-7635e67600bb", order=ORDER, picked_slots=picked),
              open(os.path.join(ROOT, "request.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


if __name__ == "__main__":
    {"fetch": fetch, "install": install}[sys.argv[1]]()
