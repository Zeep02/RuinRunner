"""One-time campaign-v3 -> campaign-v4 migration: twelve new talents, 13 plain stat accessories retired, skills pay with mana only.
007_talent_rework.sql moves existing profiles over. After publication, edit a NEW immutable catalog.
"""
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[2]
RETIRED = {"lucky_dice", "storm_feather", "guardian_seal", "crimson_ring", "mana_prism", "bulwark_brooch", "ruby_heart",
           "sapphire_flask", "guardian_plate", "winged_boots", "assassin_mark", "executioners_coin", "four_leaf_clover"}
# Recipes that used a retired stat piece take a survivor of the same set instead.
RECIPES = {"mana_aegis": ["mana_thread", "soul_capacitor"], "prism_cage": ["guard_bell", "last_light_pendant"],
           "trail_spindle": ["rush_spur", "cadence_clasp"]}
DEFAULTS = ["corpse_burst", "chain_chant", "ghost_mine", "miser"]
COSTS = {"glass_blade": (3, 0), "last_stand": (5, 1), "corpse_burst": (0, 0), "blood_pact": (3, 0), "void_rush": (5, 1), "chain_chant": (0, 0),
         "ghost_mine": (0, 0), "razor_wind": (3, 0), "close_call": (5, 1), "miser": (0, 0), "revenant": (5, 1), "gambler": (3, 0)}
ROW = re.compile(r'new TalentDefinition\(TalentId\.\w+,"(\w+)","([^"]+)",TalentFamily\.(\w+),"((?:[^"\\]|\\.)*)"\)')

source = (ROOT / "Assets/RuinRunner/Runtime/Progression/TalentLoadout.cs").read_text(encoding="utf-8-sig")
talents = []
for id, name, family, text in ROW.findall(source):
    cost, runs = COSTS[id]
    talents.append(dict(id=id, branch=family.lower(), unlockCost=cost, requiredRuns=runs, name=name, description=text.replace("\\n", "\n")))
assert len(talents) == 12 and {t["id"] for t in talents} == set(COSTS), len(talents)

catalog = json.loads((ROOT / "Backend/Content/catalog.campaign-v3.json").read_text(encoding="utf-8"))
catalog["version"] = "campaign-v4"
catalog["talents"] = talents
catalog["defaultUnlockedTalentIds"] = DEFAULTS
catalog["items"] = [item for item in catalog["items"] if item["id"] not in RETIRED]
for item in catalog["items"]:
    if item["kind"] == "weapon":
        item["description"] = re.sub(r" · 쿨타임 [\d.]+초$", "", item["description"])
for recipe in catalog["exchangeRecipes"]:
    if set(recipe["inputIds"]) & RETIRED:
        recipe["inputIds"] = RECIPES[recipe["id"]]
assert not any(set(r["inputIds"]) & RETIRED for r in catalog["exchangeRecipes"])

output = ROOT / "Backend/Content/catalog.campaign-v4.json"
encoded = json.dumps(catalog, ensure_ascii=False, indent=2) + "\n"
if output.exists() and output.read_text(encoding="utf-8") != encoded:
    raise RuntimeError("Refusing to replace an existing version. Publish a new one.")
output.write_text(encoded, encoding="utf-8")
print(json.dumps(dict(path=str(output), items=len(catalog["items"]), talents=len(talents)), ensure_ascii=False))
