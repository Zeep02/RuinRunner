"""Queue the expedition gate (four candidates at its real size) and the room seal pillars (one sheet, all three stages).

Run with PIXELLAB_API_TOKEN set:  python Tools/Art/generate_gate_and_seals_20261103.py
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

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261103-gate")
ART = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated"))
COMMON = "clean dark 1 pixel outline, chunky pixel clusters, three to four shades lit from the upper left, transparent background, no text, no frame"
GATE = ("An ancient stone double door gate for a cute action roguelike, seen straight from the front: a thick weathered sandstone arch with carved amber runes "
        "and moss on top, with two heavy dark bronze door leaves shut tight in the middle, a glowing amber seal where the leaves meet, stone steps at the foot. "
        "Grand but chunky and charming, the gate fills most of the canvas, its base on the bottom edge, " + COMMON)
PILLARS = {
    "Outer": "a short square sandstone seal post with moss on top and one glowing amber rune carved on its face, standing upright on the ground, seen from the front",
    "Flooded": "a short square teal stone seal post crusted with barnacles and one glowing cyan rune carved on its face, standing upright on the ground, seen from the front",
    "Core": "a short square black obsidian seal post banded in gold and one glowing gold rune carved on its face, standing upright on the ground, seen from the front",
}


def plate(path, size):
    """A sprite on a transparent square of the wanted size. Cropped when larger, never resized."""
    image = Image.open(os.path.join(ART, *path.split("/")) + ".png").convert("RGBA")
    left, top, right, bottom = image.getbbox()
    image = image.crop((left, top, right, bottom))
    if image.width > size or image.height > size:
        x, y = max(0, (image.width - size) // 2), max(0, (image.height - size) // 2)
        image = image.crop((x, y, x + size, y + size))
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(image, ((size - image.width) // 2, size - image.height), image)
    buffer = io.BytesIO()
    canvas.save(buffer, "PNG")
    return {"base64": base64.b64encode(buffer.getvalue()).decode(), "format": "png"}


def queue(api, arguments):
    for _ in range(60):
        result = api.call("create_1_direction_object", arguments)
        text = " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")
        if not result.get("isError") and "rate limit" not in text:
            return text
        api.call("wait_for_jobs", {"min_finished": 1, "timeout_seconds": 50})
    raise RuntimeError(text[:300])


if __name__ == "__main__":
    api = PixelLab()
    os.makedirs(ROOT, exist_ok=True)
    gate = queue(api, {"description": GATE, "view": "sidescroller", "name": "RR expedition gate",
                       "style_images": [plate("External/SscaryMale/idle/down/00", 112), plate("Campaign/Environment/OuterMonument", 112), plate("External/Cainos/Props/statue", 112)]})
    print("gate", gate[:200], flush=True)
    order = [theme for theme in PILLARS for _ in range(5)] + ["Outer"]
    pillars = queue(api, {"description": "A set of barrier posts for a cute action roguelike, each filling the canvas height, " + COMMON,
                          "item_descriptions": [PILLARS[theme] for theme in order], "view": "sidescroller", "name": "RR seal pillars",
                          "style_images": [plate("External/SscaryMale/idle/down/00", 48), plate("External/Cainos/Props/pillar", 48), plate("Campaign/Environment/CoreMonument", 48)]})
    print("pillars", pillars[:200], flush=True)
    json.dump({"tool": "create_1_direction_object", "date": "2026-11-03", "gate": {"description": GATE, "response": gate[:600]},
               "pillars": {"order": order, "items": PILLARS, "response": pillars[:600]}},
              open(os.path.join(ROOT, "request.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
