"""Queue one PixelLab sheet of floor details and small standing props for each stage, two candidates per item.

Sharing a sheet keeps a stage's set consistent. The stage's existing art goes in as the style.
Run with PIXELLAB_API_TOKEN set:  python Tools/Art/generate_stage_dressing_20261103.py [Outer Flooded Core]
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

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261103-dressing")
ART = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated"))
COMMON = ("clean dark 1 pixel outline, chunky pixel clusters, three to four shades lit from the upper left, transparent background, "
          "no text, no frame, no ground plate, centred in the canvas")

# flat: lies on the floor and can be walked over. stand: a small upright object that can be smashed or stepped around.
STAGES = {
    "Outer": {
        "style": ["External/Cainos/Props/pot-a", "External/Cainos/Props/stone-a", "Pilot/Grunt/idle/down/00", "External/SscaryMale/idle/down/00"],
        "theme": "A set of props for sunlit mossy overgrown ruins in a cute top-down action roguelike, seen from slightly above",
        "flat": {
            "moss": "flat irregular patch of bright green moss lying on the ground, no height",
            "crack": "thin dark branching crack in the ground, flat",
            "leaves": "small scatter of fallen orange and green leaves, flat on the ground",
            "pebbles": "scatter of five small grey pebbles on the ground",
            "tile": "two broken sandstone floor tiles lying flat with a mossy edge",
            "flowers": "tiny cluster of white and yellow wildflowers low on the ground",
            "roots": "thin brown tree roots creeping along the ground, flat",
            "bones": "small scatter of old white bones on the ground, flat",
        },
        "stand": {
            "pot": "round terracotta clay pot with a chipped rim",
            "jar": "tall narrow clay jar with two small handles",
            "crate": "small weathered wooden crate with moss on top",
            "stump": "short broken stone column stump with moss",
            "rocks": "small pile of three mossy boulders",
            "mushrooms": "cluster of three small red spotted mushrooms",
            "grass": "tall tuft of green grass blades",
            "bush": "small round leafy green bush",
        },
    },
    "Flooded": {
        "style": ["Campaign/Terrain/Flooded/Cistern", "Campaign/Environment/FloodedCluster", "Pilot/Tidecaller/idle/down/00", "External/SscaryMale/idle/down/00"],
        "theme": "A set of props for dark teal flooded stone corridors in a cute top-down action roguelike, seen from slightly above",
        "flat": {
            "puddle": "small shallow puddle of teal water with a pale highlight, flat on the ground",
            "algae": "flat irregular patch of dark green slimy algae, no height",
            "crack": "thin dark wet crack in the stone floor, flat",
            "shells": "scatter of tiny pale sea shells on the ground",
            "grate": "small rusted square iron drain grate set into the floor, flat",
            "lilies": "two small round green lily pads with a tiny pink flower, flat",
            "ripple": "scatter of wet stones half sunk with a ring of water, flat",
            "coral": "thin flat crust of orange coral spreading on the floor",
        },
        "stand": {
            "pot": "round grey stone jar crusted with barnacles",
            "barrel": "small rusted iron banded barrel dripping water",
            "crate": "small waterlogged dark wooden crate with seaweed",
            "stump": "short broken teal stone column stump with barnacles",
            "rocks": "small pile of three wet dark teal rocks",
            "coralrock": "small upright branching orange coral growth",
            "reeds": "tuft of tall dark green water reeds",
            "pipe": "short bent rusty pipe sticking up out of the floor",
        },
    },
    "Core": {
        "style": ["Campaign/Environment/CoreCluster", "Campaign/Environment/CoreMonument", "Pilot/SealKnight/idle/down/00", "External/SscaryMale/idle/down/00"],
        "theme": "A set of props for an ink black sealed vault with gold inlay in a cute top-down action roguelike, seen from slightly above",
        "flat": {
            "crack": "thin crack glowing faint gold in black stone floor, flat",
            "glyph": "small gold rune glyph inlaid flat in the floor",
            "coins": "scatter of five small old gold coins on the floor, flat",
            "chain": "short length of broken dark iron chain lying flat on the floor",
            "ash": "flat irregular grey ash pile with a few embers",
            "plate": "broken black stone seal plate with a gold edge, lying flat",
            "shards": "scatter of small dark violet crystal shards on the floor",
            "wax": "two melted candle stubs in a pool of pale wax, low on the floor",
        },
        "stand": {
            "pot": "round black ceramic urn with a gold band",
            "brazier": "small iron brazier holding a low amber flame",
            "chest": "small locked black coffer with gold corners",
            "stump": "short broken black obelisk stump with gold lines",
            "rocks": "small pile of three jagged black rocks with gold veins",
            "crystal": "small upright cluster of dark violet crystals",
            "candles": "cluster of three tall lit candles on the floor",
            "chains": "short iron post with a dangling broken chain",
        },
    },
}


def style_image(path):
    # A 32 pixel window around the sprite, cropped and never resized, so the sheet stays at 32 pixels.
    image = Image.open(os.path.join(ART, *path.split("/")) + ".png").convert("RGBA")
    left, top, right, bottom = image.getbbox()
    cx, cy = (left + right) // 2, (top + bottom) // 2
    crop = image.crop((cx - 16, cy - 16, cx + 16, cy + 16))
    buffer = io.BytesIO()
    crop.save(buffer, "PNG")
    return {"base64": base64.b64encode(buffer.getvalue()).decode(), "format": "png"}


if __name__ == "__main__":
    api = PixelLab()
    os.makedirs(ROOT, exist_ok=True)
    for name in sys.argv[1:] or list(STAGES):
        stage = STAGES[name]
        keys = [("flat", k) for k in stage["flat"]] + [("stand", k) for k in stage["stand"]]
        items = [stage[kind][key] for kind, key in keys]
        arguments = {"description": stage["theme"] + ", " + COMMON, "item_descriptions": items * 2, "view": "top-down",
                     "name": "RR dressing " + name, "style_images": [style_image(p) for p in stage["style"]]}
        for attempt in range(60):
            result = api.call("create_1_direction_object", arguments)
            text = " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")
            if not result.get("isError") and "rate limit" not in text:
                break
            # Other generations hold the concurrent slots; wait for one to free up.
            api.call("wait_for_jobs", {"min_finished": 1, "timeout_seconds": 50})
        print(name, text[:300], flush=True)
        json.dump({"tool": "create_1_direction_object", "description": arguments["description"], "view": "top-down", "date": "2026-11-03",
                   "order": [f"{k}/{v}" for k, v in keys], "items": items, "style_images": stage["style"], "response": text[:800]},
                  open(os.path.join(ROOT, name + ".request.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
