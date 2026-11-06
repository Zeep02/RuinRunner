"""Second try at guns and daggers, which came back too noisy at small sizes.

The generator is limited to a short palette from the start, because cutting colours afterwards also cut the detail.
Prompts ask for thick flat shapes, and guns lying level so that a 30px gun stays about 30px long.

python Tools/Art/generate_weapons_clean_20261105.py
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import generate_weapons_cool_20261105 as cool

CLEAN = "clean crisp pixel art, thick simple shapes, flat cel shading with few colours, solid black outline, no thin lines, no particles, no sparkles, no loose pixels, no hand, no background, no text"
SHORT = {
    "scrap_revolver": "stubby revolver riveted together from rusty scrap plates, one brass gear on the side, short thick barrel",
    "ricochet_pistol": "short golden flintlock pistol with one white crystal on top, thick barrel",
    "gate_shotgun": "short blunderbuss with a wide flared bronze horn muzzle and dark wood grip",
    "furnace_sprayer": "hand flame gun: a round iron furnace with a glowing orange grate, thick wooden pistol grip, short brass dragon head nozzle",
    "meteor_waybill": "short thick purple and gold hand mortar with a pistol grip and a glowing orange meteor in its mouth",
}
LONG = {
    "scout_carbine": "pale wood elven musket, thick stock, brass spyglass on top, green ribbon",
    "frost_bolt_rifle": "rifle with a thick clear blue ice crystal barrel, silver bolt, blue stock",
    "lightning_carbine": "carbine with thick copper coils round the barrel and a glowing yellow crystal at the breech, wooden stock",
    "siege_engineer": "wood and iron repeating hand crossbow gun with a round drum of bolts on top, brass gear, thick stock",
    "last_bell": "long black musket with a bone white stock and one small brass bell hanging under the muzzle",
}
GUN = "single fantasy RPG gun in exact side profile lying perfectly level, barrel horizontal pointing right, not diagonal, "
DAGGER = "single fantasy dagger with a thick readable blade, standing vertically with the point straight up and the hilt at the bottom, "
BATCHES = [
    ("guns-short", 28, 14, GUN + CLEAN, SHORT),
    ("guns-long", 32, 16, GUN + CLEAN, LONG),
]


def queue(api, name, size, colours, description, items):
    per = 64 // len(items)
    listed = []
    for art in items:
        listed += [art] * per
    arguments = {"description": description, "name": "clean_" + name, "size": size, "view": "sidescroller", "max_colors": colours, "item_descriptions": listed[:64]}
    result = api.call("create_1_direction_object", arguments)
    body = cool.text_of(result)
    if result.get("isError"):
        raise RuntimeError(body[:400])
    object_id = cool.re.search(r"id: ([0-9a-f-]{36})", body).group(1)
    print(name, "queued", object_id, flush=True)
    return {"family": name, "batch": 0, "object_id": object_id, "size": size, "max_colors": colours, "per_weapon": per, "slots": 64,
            "order": list(items) if isinstance(items, dict) else None, "description": description, "items": listed[:64]}


def main():
    weapons = json.load(open(os.path.join(HERE, "weapons70.json"), encoding="utf-8"))["weapons"]
    daggers = {w["id"]: cool.ART_FIX.get(w["id"], w["art"]) + ", " + cool.GRADE[w["rarity"]] for w in weapons if w["family"] == "Dagger"}
    api = cool.PixelLab()
    jobs = []
    for name, size, colours, description, table in BATCHES:
        job = queue(api, name, size, colours, description, list(table.values()))
        job["order"] = list(table)
        jobs.append(job)
    job = queue(api, "daggers", 32, 14, DAGGER + CLEAN, list(daggers.values()))
    job["order"] = list(daggers)
    jobs.append(job)
    cool.ROOT = os.path.join(HERE, "Staging", "PixelLab", "20261105-weapons-clean")
    for job in jobs:
        cool.fetch(api, job)
    print("done")


if __name__ == "__main__":
    main()
