"""Queue one PixelLab sheet with two candidates for each of the 30 accessories. Sharing a sheet keeps them one set.

The enemies' and hero's own sprites go in as the style. Prints the object id and writes request.json.
Run with PIXELLAB_API_TOKEN set:  python Tools/Art/generate_accessories_20261103.py
"""
import base64
import io
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pixellab_mcp_client import PixelLab

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261103-accessories")
ART = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated"))
STYLE = ["External/SscaryMale/idle/down/00", "Pilot/Shooter/idle/down/00", "Pilot/Leaper/idle/down/00",
         "Pilot/Tidecaller/idle/down/00", "Pilot/Sentinel/idle/down/00"]
DESCRIPTION = ("A set of cute chunky inventory item icons for an action roguelike: each one small object centred, filling most of the "
               "canvas, front view, clean dark 1 pixel outline, chunky pixel clusters, three to four shades lit from the upper left, "
               "bright saturated accent colour, transparent background, no text, no frame, no background plate")

ITEMS = {
    "lucky_dice": "cracked ivory six-sided die with black pips and a thin crack",
    "storm_feather": "curved white feather with pale blue edge and a little yellow lightning streak",
    "guardian_seal": "round red wax seal stamped with a small shield",
    "crimson_ring": "gold ring set with one big crimson ruby",
    "mana_prism": "faceted deep blue crystal prism glowing from inside",
    "bulwark_brooch": "shield shaped stone and gold brooch with a pin",
    "berserker_emblem": "red iron war emblem with crossed axes and two small horns",
    "sage_sigil": "golden disc engraved with a glowing rune star",
    "ruby_heart": "chubby heart shaped ruby gem with a bright highlight",
    "sapphire_flask": "round glass flask of glowing blue potion with a cork",
    "guardian_plate": "small riveted steel breastplate",
    "winged_boots": "brown leather boot with one small white wing on its heel",
    "assassin_mark": "black throwing token shaped like a dagger tip with a red slash mark",
    "executioners_coin": "dark gold coin stamped with a small axe",
    "four_leaf_clover": "bright green four leaf clover with a short stem",
    "arcane_lens": "round brass monocle lens with violet arcane glow",
    "vampire_fang": "one big white fang with a red tip hanging from a dark cord",
    "soul_capacitor": "brass capped glass capsule holding purple soul lightning",
    "mana_thread": "small silver goblet brimming with glowing blue liquid",
    "guard_bell": "bronze ring with one small round bell hanging from it",
    "rush_spur": "red armoured boot with a sharp gold spur",
    "steady_locket": "silver round locket on a chain with a small crosshair on its face",
    "powder_satchel": "small black powder bottle with a brass bullet casing tied to its neck",
    "last_light_pendant": "pendant holding a tiny glowing star lantern on a gold chain",
    "recall_spool": "wooden thread spool wound with glowing pink thread, one loose end curling back",
    "cadence_clasp": "bronze belt clasp with three small lit notches in a row",
    "prism_cage": "rainbow crystal locked inside a small gold wire cage",
    "trail_spindle": "wooden drop spindle wound with bright green thread",
    "focus_prism": "small triangular clear crystal with a red target ring in its centre",
    "mana_aegis": "small round shield made of blue glass with a glowing centre",
}


def style_image(path):
    # A 32 pixel window around the sprite, cropped and never resized, so the sheet stays at the 32 pixel size.
    image = Image.open(os.path.join(ART, *path.split("/")) + ".png").convert("RGBA")
    left, top, right, bottom = image.getbbox()
    cx, cy = (left + right) // 2, (top + bottom) // 2
    crop = image.crop((cx - 16, cy - 16, cx + 16, cy + 16))
    buffer = io.BytesIO()
    crop.save(buffer, "PNG")
    return {"base64": base64.b64encode(buffer.getvalue()).decode(), "format": "png"}


if __name__ == "__main__":
    ids = list(ITEMS)
    # Two rounds through the list, so slots 0-29 and 30-59 are candidates for the same accessories.
    arguments = {"description": DESCRIPTION, "item_descriptions": [ITEMS[i] for i in ids] * 2, "view": "sidescroller",
                 "name": "RR accessories", "style_images": [style_image(p) for p in STYLE]}
    result = PixelLab().call("create_1_direction_object", arguments)
    text = " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")
    print(text[:600])
    os.makedirs(ROOT, exist_ok=True)
    json.dump({"tool": "create_1_direction_object", "description": DESCRIPTION, "view": "sidescroller", "date": "2026-11-03",
               "order": ids, "items": ITEMS, "style_images": STYLE, "response": text[:800]},
              open(os.path.join(ROOT, "request.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
