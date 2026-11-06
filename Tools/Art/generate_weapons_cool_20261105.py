"""Candidates for a cooler look on the sixty melee and magic weapons, at the same canvas and in the same pose as the originals.

Nothing here touches the game. Each family's candidates land in Staging/PixelLab/20261105-weapons-cool/<set>/NN.png with a request.json.
Size decides how many objects PixelLab returns: 64 up to 42px, 16 up to 85px, so the larger families take two batches.

python Tools/Art/generate_weapons_cool_20261105.py
"""
import json
import os
import re
import sys
import time

import requests

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pixellab_mcp_client import PixelLab

ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261105-weapons-cool")
FAMILIES = {
    "Dagger": (32, 1, "wicked fantasy dagger, sharply shaped blade, standing vertically with the point straight up and the hilt at the bottom"),
    "Katana": (32, 1, "striking fantasy katana, long slender curved blade, standing vertically with the point straight up and the hilt at the bottom"),
    "Orb": (32, 1, "magical floating fantasy orb weapon, centred, seen from the side, round and self contained"),
    "Greatsword": (48, 2, "massive brutal fantasy greatsword with a dramatic silhouette, standing vertically with the point up and the hilt at the bottom"),
    "Staff": (48, 2, "dramatic fantasy mage staff with an elaborate head, standing vertically with the head at the top"),
    "Spear": (64, 2, "long dramatic fantasy polearm, thin shaft running diagonally from the bottom left corner to a striking head at the top right corner"),
}
GRADE = {
    "Common": "battle worn but cool, strong simple shape",
    "Rare": "enchanted, bold shape, a glowing accent",
    "Epic": "ornate and menacing, glowing magic details",
    "Legendary": "mythic masterpiece, awe inspiring ornate shape, strong glowing magic",
}
ART_FIX = {"ember_claw": "black curved talon shaped dagger with glowing orange ember cracks"}
STYLE = "bold dark outline, saturated colours, high contrast shading, game weapon sprite, no hand, no background, no text"


def text_of(result):
    return " ".join(part.get("text", "") for part in result.get("content", []) if part.get("type") == "text")


def queue(api, family, batch, size, base, weapons):
    slots = 64 if size <= 42 else 16
    per = max(1, slots // len(weapons))
    items = []
    for weapon in weapons:
        art = ART_FIX.get(weapon["id"], weapon["art"])
        items += [f'{art}, {GRADE[weapon["rarity"]]}'] * per
    arguments = {"description": f"{base}, {STYLE}", "name": f"cool_{family}_{batch}", "size": size, "view": "sidescroller", "item_descriptions": items[:slots]}
    for _ in range(40):
        result = api.call("create_1_direction_object", arguments)
        body = text_of(result)
        if not result.get("isError"):
            object_id = re.search(r"id: ([0-9a-f-]{36})", body).group(1)
            print(family, batch, "queued", object_id, flush=True)
            return {"family": family, "batch": batch, "object_id": object_id, "size": size, "per_weapon": per, "slots": slots,
                    "order": [w["id"] for w in weapons], "description": arguments["description"], "items": items[:slots]}
        if re.search(r"slot|rate|busy|too many|concurrent|limit", body, re.I):
            time.sleep(30)
            continue
        raise RuntimeError(body[:400])
    raise RuntimeError("no slot")


def fetch(api, job):
    folder = os.path.join(ROOT, f'{job["family"]}_{job["batch"]}')
    os.makedirs(folder, exist_ok=True)
    body = ""
    for _ in range(60):
        body = text_of(api.call("get_object", {"object_id": job["object_id"], "include_preview": False}))
        if "frame_{i}" in body or "frame_%7Bi%7D" in body:
            break
        time.sleep(15)
    urls = re.findall(r"https://\S+\.png", body)
    if not urls:
        raise RuntimeError(job["family"] + ": " + body[:300])
    for index in range(job["slots"]):
        url = urls[0].replace("{i}", str(index)).replace("%7Bi%7D", str(index))
        response = requests.get(url, timeout=60)
        response.raise_for_status()
        open(os.path.join(folder, f"{index:02}.png"), "wb").write(response.content)
    json.dump(job, open(os.path.join(folder, "request.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(job["family"], job["batch"], "fetched", flush=True)


def main():
    weapons = json.load(open(os.path.join(HERE, "weapons70.json"), encoding="utf-8"))["weapons"]
    api = PixelLab()
    jobs = []
    for family, (size, batches, base) in FAMILIES.items():
        group = [w for w in weapons if w["family"] == family]
        for batch in range(batches):
            jobs.append(queue(api, family, batch, size, base, group))
    for job in jobs:
        fetch(api, job)
    print("done")


if __name__ == "__main__":
    main()
