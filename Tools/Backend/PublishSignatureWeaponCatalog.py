"""One-time campaign-v2 -> campaign-v3 migration: the 70 signature weapons replace every earlier weapon, and PassiveAccessories adds 20 accessories.
Weapon values are read from Unity's generated SignatureWeaponCatalog.cs. After publication, edit a NEW immutable catalog.
"""
import json
import pathlib
import re

from PassiveAccessories import catalog_items

ROOT = pathlib.Path(__file__).resolve().parents[2]
RARITIES = {"Common": 0, "Uncommon": 1, "Rare": 2, "Epic": 3, "Legendary": 4}
# Same price per grade as before. No weapon is Uncommon now, so these weights give about 60/30/8/2 percent by grade.
PRICE = [18, 33, 48, 63, 78]
SELL = [4, 8, 12, 15, 19]
DROP = [100, 62, 50, 20, 5]
# One weapon of each kind to start with. Costs follow the old starters; 006_signature_weapons.sql moves earlier unlocks here.
STARTERS = [("trainee_blade", 0), ("alley_awl", 3), ("scrap_revolver", 3), ("merc_slab", 5), ("sentry_spear", 5),
            ("novice_branch", 5), ("whetstone_orb", 5), ("gate_shotgun", 6)]
MAKE = re.compile(r'Make\("(\w+)","([^"]+)",WeaponRarity\.(\w+),WeaponFamily\.(\w+),([\d.]+)f,([\d.]+)f,new Color\([^)]*\),'
                  r'WeaponAttackType\.(\w+),(\d+),([\d.]+)f,([\d.]+)f,([\d.]+)f,([\d.]+)f,"((?:[^"\\]|\\.)*)"\)')

source = (ROOT / "Assets/RuinRunner/Runtime/Data/SignatureWeaponCatalog.cs").read_text(encoding="utf-8-sig")
weapons = []
for id, name, rarity, family, power, delay, kind, magazine, reload, speed, mana, cooldown, text in MAKE.findall(source):
    grade = RARITIES[rarity]
    text = text.replace('\\"', '"').replace("\\n", "\n")
    weapons.append(dict(id=id, kind="weapon", name=name, description=text + "\n마나 " + mana + " · 쿨타임 " + cooldown + "초",
        rarity=grade, price=PRICE[grade], sellPrice=SELL[grade], dropWeight=DROP[grade], minStage=0, maxStage=2,
        stats=dict(attackPower=float(power), attackDelayMultiplier=float(delay), magazineSize=int(magazine), reloadSeconds=float(reload),
            projectileSpeed=max(1.0, float(speed)), projectilePierce=1, heavyDamageMultiplier=1.4, guardDamageReduction=0.4,
            specialManaCost=float(mana), specialCooldown=float(cooldown))))
assert len(weapons) == 70 and len({w["id"] for w in weapons}) == 70, len(weapons)

catalog = json.loads((ROOT / "Backend/Content/catalog.campaign-v2.json").read_text(encoding="utf-8"))
catalog["version"] = "campaign-v3"
catalog["items"] = weapons + [item for item in catalog["items"] if item["kind"] != "weapon"] + catalog_items()
names = {w["id"]: w for w in weapons}
for id, _ in STARTERS:
    assert names[id]["rarity"] == 0, id
catalog["starterWeapons"] = [dict(id=id, unlockCost=cost, name=names[id]["name"]) for id, cost in STARTERS]

output = ROOT / "Backend/Content/catalog.campaign-v3.json"
encoded = json.dumps(catalog, ensure_ascii=False, indent=2) + "\n"
if output.exists() and output.read_text(encoding="utf-8") != encoded:
    raise RuntimeError("Refusing to replace an existing version. Publish a new one.")
output.write_text(encoded, encoding="utf-8")
print(json.dumps(dict(path=str(output), weapons=len(weapons), items=len(catalog["items"]), starters=[w for w, _ in STARTERS]), ensure_ascii=False))
