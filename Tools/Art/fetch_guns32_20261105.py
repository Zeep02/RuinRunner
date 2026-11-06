"""Download a batch of gun candidates (PixelLab create_1_direction_object) and lay them out for choosing.

python Tools/Art/fetch_guns32_20261105.py <object_id> <folder under Staging/PixelLab> <size>
"""
import json
import os
import re
import sys
import time

import requests
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pixellab_mcp_client import PixelLab


def text_of(result):
    return " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")


def main():
    object_id, folder, size_px = sys.argv[1], sys.argv[2], int(sys.argv[3])
    root = os.path.join(HERE, "Staging", "PixelLab", folder)
    os.makedirs(root, exist_ok=True)
    api = PixelLab()
    body = ""
    for _ in range(40):
        body = text_of(api.call("get_object", {"object_id": object_id, "include_preview": False}))
        if "review" in body or "frame" in body:
            break
        time.sleep(15)
    urls = re.findall(r"https://\S+\.png\S*", body)
    # The candidates are listed as one URL pattern with the frame number left open.
    if len(urls) == 1 and "{i}" in urls[0]:
        urls = [urls[0].replace("{i}", str(i)) for i in range(64)]
    print(len(urls), "candidates")
    if not urls:
        print(body[:600])
        return
    cells = []
    for index, url in enumerate(urls):
        path = os.path.join(root, f"{index:02}.png")
        response = requests.get(url.rstrip(").,"), timeout=60)
        response.raise_for_status()
        open(path, "wb").write(response.content)
        cells.append(Image.open(path).convert("RGBA"))
    json.dump({"tool": "create_1_direction_object", "object_id": object_id, "size": size_px, "view": "sidescroller", "date": "2026-11-05", "candidates": len(urls)},
              open(os.path.join(root, "request.json"), "w", encoding="utf-8"), indent=2)
    scale, columns = 4, 12
    size = size_px * scale + 12
    rows = (len(cells) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * size, rows * size), (58, 62, 74, 255))
    draw = ImageDraw.Draw(sheet)
    for index, cell in enumerate(cells):
        x, y = (index % columns) * size, (index // columns) * size
        sheet.alpha_composite(cell.resize((cell.width * scale, cell.height * scale), Image.NEAREST), (x + 4, y + 8))
        draw.text((x + 2, y), str(index), fill=(255, 255, 120, 255))
    sheet.save(os.path.join(root, "sheet.png"))


if __name__ == "__main__":
    main()
