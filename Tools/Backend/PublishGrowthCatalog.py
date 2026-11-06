"""One-time campaign-v4 -> campaign-v5: 24 picks instead of 12, four active instead of three, and a permanent tree bought with legacy.
Existing profiles need no migration: what they own is still in the catalog. After publication, edit a NEW immutable catalog.
"""
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[2]
COSTS = {"glass_blade": (3, 0), "last_stand": (5, 1), "corpse_burst": (0, 0), "executioner_edge": (4, 0), "first_blood": (4, 0), "bloodthirst": (6, 2),
         "blood_pact": (3, 0), "void_rush": (5, 1), "chain_chant": (0, 0), "full_cup": (4, 0), "mana_heart": (4, 0), "hunter_breath": (6, 2),
         "ghost_mine": (0, 0), "razor_wind": (3, 0), "close_call": (5, 1), "double_step": (6, 2), "momentum": (4, 0), "phantom_step": (5, 1),
         "miser": (0, 0), "revenant": (5, 1), "gambler": (3, 0), "cursed_power": (5, 1), "duelist": (4, 0), "late_bloom": (6, 2)}
PICK = re.compile(r'new TalentDefinition\(TalentId\.\w+,"(\w+)","([^"]+)",TalentFamily\.(\w+),"((?:[^"\\]|\\.)*)"\)')
TRACK = re.compile(r'new MasteryTrack\("(\w+)","([^"]+)","([^"]+)"\)')

source = (ROOT / "Assets/RuinRunner/Runtime/Progression/TalentLoadout.cs").read_text(encoding="utf-8-sig")
rank_costs = [int(x) for x in re.search(r"MasteryCosts=\{([\d,]+)\}", source).group(1).split(",")]
talents = []
for id, name, family, text in PICK.findall(source):
    cost, runs = COSTS[id]
    talents.append(dict(id=id, branch=family.lower(), unlockCost=cost, requiredRuns=runs, name=name, description=text.replace("\\n", "\n")))
assert len(talents) == 24 and {t["id"] for t in talents} == set(COSTS), len(talents)
for id, name, effect in TRACK.findall(source):
    for rank, cost in enumerate(rank_costs, 1):
        node = dict(id=f"mastery_{id}_{rank}", branch="mastery", unlockCost=cost, requiredRuns=0, name=f"{name} {rank}", description=effect)
        if rank > 1:
            node["requires"] = f"mastery_{id}_{rank - 1}"
        talents.append(node)
assert len(talents) == 64, len(talents)

catalog = json.loads((ROOT / "Backend/Content/catalog.campaign-v4.json").read_text(encoding="utf-8"))
catalog["version"] = "campaign-v5"
catalog["talents"] = talents
catalog["maxActiveTalents"] = 4

output = ROOT / "Backend/Content/catalog.campaign-v5.json"
encoded = json.dumps(catalog, ensure_ascii=False, indent=2) + "\n"
if output.exists() and output.read_text(encoding="utf-8") != encoded:
    raise RuntimeError("Refusing to replace an existing version. Publish a new one.")
output.write_text(encoded, encoding="utf-8")
print(json.dumps(dict(path=str(output), talents=len(talents)), ensure_ascii=False))
