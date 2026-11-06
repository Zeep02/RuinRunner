"""Install the chosen PixelLab accessory icons over the old ones, keeping each file's name and .meta (so its GUID).

Nothing is resampled: each icon is cut to its visible pixels, and loose sparks are erased.
python Tools/Art/install_accessories_20261103.py
"""
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from install_remaining_enemies_20261103 import ART, without_specks

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261103-accessories")
# The sheet holds two rounds of the list; these took the second candidate.
SECOND = {"storm_feather", "crimson_ring", "mana_prism", "ruby_heart", "four_leaf_clover", "guard_bell", "steady_locket",
          "powder_satchel", "last_light_pendant", "trail_spindle", "focus_prism", "mana_aegis"}


def main():
    request = json.load(open(os.path.join(ROOT, "request.json"), encoding="utf-8"))
    order = request["order"]
    target = os.path.join(ART, "Equipment", "Accessory")
    picks = {}
    for n, name in enumerate(order):
        slot = n + 30 if name in SECOND else n
        picks[name] = slot
        image = without_specks(Image.open(os.path.join(ROOT, "candidates", f"{slot:02d}.png")).convert("RGBA"))
        path = os.path.join(target, name + ".png")
        if not os.path.exists(path + ".meta"):
            raise FileNotFoundError(path + ".meta")
        image.crop(image.getbbox()).save(path)
        print(name, slot, image.getbbox())
    request["picked_slots"] = picks
    json.dump(request, open(os.path.join(ROOT, "request.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
