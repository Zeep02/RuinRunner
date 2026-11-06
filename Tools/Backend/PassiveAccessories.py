"""The 20 passive accessories added for campaign-v3, each with one visible pixelfx passive.

PublishSignatureWeaponCatalog.py reads ACCESSORIES for the catalog.
Run this file to write them into Unity's enum, loot table and the shared tuning schema; running it again replaces its own rows:
python Tools/Backend/PassiveAccessories.py
"""
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[2]
RARITIES = ["Common", "Uncommon", "Rare", "Epic", "Legendary"]
# Same prices and drop weights per grade as the 30 accessories already in the catalog.
PRICE = [14, 27, 40, 53, 66]
SELL = [3, 6, 10, 13, 16]
DROP = [100, 62, 28, 9, 3]
COLOR = ["new Color(.8f,.8f,.8f)", "new Color(.3f,1f,.5f)", "new Color(.3f,.6f,1f)", "new Color(.75f,.3f,1f)", "new Color(1f,.65f,.15f)"]
BASE = ["maxHealth", "maxMana", "armor", "moveSpeed", "criticalChance", "criticalDamage", "luck",
        "manaRegeneration", "lifeOnKill", "manaOnKill", "weaponDamageBonus", "specialDamageBonus"]
FRACTION_BASE = {"criticalChance", "criticalDamage", "weaponDamageBonus", "specialDamageBonus"}
INTEGER = {"maxCopies", "hitCount", "killCount", "targetCount", "pierce"}
EXTRA = "장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%"


def item(id, enum, name, rarity, description, base, **effect):
    effect = dict(maxCopies=3, **effect)
    return dict(id=id, enum=enum, name=name, rarity=rarity, description=description, base=base, effect=effect)


ACCESSORIES = [
    item("retribution_thorn", "RetributionThorn", "보복의 가시", 0,
         "방어력 +{armor}\n맞으면 가까운 적 {effect.targetCount}마리의 발밑에서 가시가 솟아 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(armor=1), cooldownSeconds=1.5, damageFraction=.5, extraCopyDamageFraction=.15, range=2.6, targetCount=3),
    item("mirror_shard", "MirrorShard", "거울 조각", 0,
         "치명타 확률 +1%\n공격을 {effect.hitCount}번 맞히면 무기 끝에서 빛의 창이 뻗어 나가 일직선의 적에게 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n길이 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(criticalChance=.01), hitCount=3, cooldownSeconds=1, damageFraction=.3, extraCopyDamageFraction=.08, range=3.5, width=.9),
    item("cinder_seed", "CinderSeed", "불씨 씨앗", 0,
         "적을 처치하면 쓰러진 자리에서 불기둥이 솟아 주변에 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(), cooldownSeconds=1, damageFraction=.5, extraCopyDamageFraction=.15, range=1.8),
    item("thunder_charm", "ThunderCharm", "번개 부적", 1,
         "치명타 확률 +1%\n공격을 {effect.hitCount}번 맞히면 맞은 적에게 번개가 떨어져 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(criticalChance=.01), hitCount=4, cooldownSeconds=1.5, damageFraction=.55, extraCopyDamageFraction=.15),
    item("poison_censer", "PoisonCenser", "독 향로", 1,
         "공격을 {effect.hitCount}번 맞히면 맞은 적의 발밑에 독 웅덩이가 {effect.seconds}초 동안 고입니다.\n안에 있는 적은 0.5초마다 공격력의 {effect.damageFraction:p}% 피해를 받습니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(), hitCount=5, cooldownSeconds=4, damageFraction=.1, extraCopyDamageFraction=.03, seconds=3.5, range=1.5),
    item("chain_spark", "ChainSpark", "불똥 사슬", 1,
         "공격을 {effect.hitCount}번 맞히면 맞은 적에서 전기가 튀어 최대 {effect.targetCount}마리에게 이어집니다.\n피해: 공격력의 {effect.damageFraction:p}%, 튈 때마다 20% 감소 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(), hitCount=3, cooldownSeconds=2, damageFraction=.3, extraCopyDamageFraction=.08, targetCount=3, range=3.5, hopDamageMultiplier=.8),
    item("soul_lantern", "SoulLantern", "넋 등불", 1,
         "처치 시 마나 +{manaOnKill}\n적을 처치하면 넋이 빠져나와 가장 가까운 적에게 날아가 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n탐지 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(manaOnKill=.5), cooldownSeconds=.8, damageFraction=.6, extraCopyDamageFraction=.15, range=6, projectileSpeed=11),
    item("vine_snare", "VineSnare", "덩굴 올가미", 1,
         "같은 적을 {effect.hitCount}번 맞히면 덩굴이 {effect.seconds}초 동안 묶고 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n보스는 묶이는 대신 느려집니다. 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(), hitCount=3, cooldownSeconds=3, damageFraction=.25, extraCopyDamageFraction=.08, seconds=1.6),
    item("shadow_twin_ring", "ShadowTwinRing", "그림자 쌍가락지", 2,
         "치명타 피해 +5%\n공격을 {effect.hitCount}번 맞히면 그림자 칼날이 맞은 적과 근처의 적을 합쳐 {effect.targetCount}마리까지 다시 벱니다.\n피해: 공격력의 {effect.damageFraction:p}% · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(criticalDamage=.05), hitCount=3, cooldownSeconds=1.2, damageFraction=.35, extraCopyDamageFraction=.1, targetCount=2, range=3),
    item("quake_bell", "QuakeBell", "진동 종", 2,
         "공격을 {effect.hitCount}번 맞히면 맞은 적의 자리에 충격파가 터져 주변을 {effect.seconds}초 동안 멈춥니다.\n피해: 공격력의 {effect.damageFraction:p}% · 범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(), hitCount=6, cooldownSeconds=3, damageFraction=.7, extraCopyDamageFraction=.15, range=2, seconds=.8),
    item("frost_wave_necklace", "FrostWaveNecklace", "서리 물결 목걸이", 2,
         "이동 속도 +{moveSpeed}\n대시하면 떠난 자리에 서리가 터져 주변 적에게 공격력의 {effect.damageFraction:p}% 피해를 주고 {effect.seconds}초 동안 느리게 합니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(moveSpeed=.15), cooldownSeconds=3, damageFraction=.4, extraCopyDamageFraction=.1, range=2.4, slowFactor=.55, seconds=2),
    item("guardian_feather", "GuardianFeather", "수호 깃털", 2,
         "방어력 +{armor}\n가드나 패링에 성공하면 앞쪽으로 깃털이 흩날려 적을 밀어내고 공격력의 {effect.damageFraction:p}% 피해를 줍니다. 체력 {effect.heal} 회복.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(armor=2), cooldownSeconds=3, damageFraction=.6, extraCopyDamageFraction=.15, range=2.8, push=5, heal=2),
    item("blood_pact", "BloodPact", "피의 서약", 2,
         "최대 체력 +{maxHealth}\n체력이 {effect.healthThreshold:p}% 이하일 때 공격을 {effect.hitCount}번 맞히면 맞은 적의 피를 빨아 공격력의 {effect.damageFraction:p}% 피해를 주고 체력 {effect.heal} 회복.\n재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(maxHealth=6), hitCount=3, cooldownSeconds=1, damageFraction=.3, extraCopyDamageFraction=.08, healthThreshold=.5, heal=1.5),
    item("swap_flash", "SwapFlash", "교체 섬광", 2,
         "무기를 바꾸면 앞쪽에서 마력이 터져 적을 밀어내고 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(), cooldownSeconds=4, damageFraction=.7, extraCopyDamageFraction=.15, range=3, push=4),
    item("meteor_compass", "MeteorCompass", "유성 나침반", 3,
         "무기 스킬 피해 +{specialDamageBonus:p}%\n무기 스킬을 쓰면 가까운 적 {effect.targetCount}마리에게 유성이 떨어져 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n탐지 {effect.range}칸 · 폭발 {effect.radius}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(specialDamageBonus=.06), cooldownSeconds=6, damageFraction=1.1, extraCopyDamageFraction=.25, targetCount=3, range=7, radius=1.4, delaySeconds=.8),
    item("gravity_pearl", "GravityPearl", "중력 진주", 3,
         "공격을 {effect.hitCount}번 맞히면 맞은 적의 자리에 작은 블랙홀이 열려 주변 적을 끌어모은 뒤 터집니다.\n피해: 공격력의 {effect.damageFraction:p}% · 범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(), hitCount=5, cooldownSeconds=5, damageFraction=1, extraCopyDamageFraction=.25, range=3, seconds=.7),
    item("time_hourglass", "TimeHourglass", "시간의 모래시계", 3,
         "마나 재생 +{manaRegeneration}\n무기 스킬을 쓰면 앞쪽의 시간이 느려져 적이 {effect.seconds}초 동안 거의 움직이지 못하고 공격력의 {effect.damageFraction:p}% 피해를 받습니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · " + EXTRA,
         dict(manaRegeneration=.4), cooldownSeconds=8, damageFraction=.4, extraCopyDamageFraction=.1, range=4, slowFactor=.3, seconds=2.5),
    item("holy_tithe", "HolyTithe", "신성한 십일조", 3,
         "처치 시 체력 +{lifeOnKill}\n적을 {effect.killCount}마리 처치할 때마다 가장 가까운 적에게 심판의 빛이 내려 공격력의 {effect.damageFraction:p}% 피해를 주고 체력 {effect.heal} 회복.\n탐지 {effect.range}칸 · " + EXTRA,
         dict(lifeOnKill=.2), killCount=5, damageFraction=1.2, extraCopyDamageFraction=.25, range=7, heal=4),
    item("storm_core", "StormCore", "폭풍의 핵", 4,
         "마나 재생 +{manaRegeneration}, 공격력 +{weaponDamageBonus:p}%\n{effect.cooldownSeconds}초마다 가까운 적 {effect.targetCount}마리에게 벼락이 저절로 떨어져 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n탐지 {effect.range}칸 · " + EXTRA,
         dict(manaRegeneration=.6, weaponDamageBonus=.05), cooldownSeconds=7, damageFraction=1.4, extraCopyDamageFraction=.3, targetCount=2, range=7),
    item("phoenix_ash", "PhoenixAsh", "불사조의 재", 4,
         "최대 체력 +{maxHealth}\n쓰러질 피해를 받으면 재에서 되살아나 최대 체력의 {effect.healFraction:p}%를 회복하고, 앞쪽에 폭발을 일으켜 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 하나만 효과가 있습니다.",
         dict(maxHealth=12), cooldownSeconds=150, damageFraction=2, extraCopyDamageFraction=0, range=3.5, healFraction=.5),
]
for row in ACCESSORIES:
    if row["id"] == "phoenix_ash":
        row["effect"]["maxCopies"] = 1
assert len(ACCESSORIES) == 20 and len({a["id"] for a in ACCESSORIES}) == 20
assert [sum(a["rarity"] == r for a in ACCESSORIES) for r in range(5)] == [3, 5, 6, 4, 2]


def catalog_items():
    return [dict(id=a["id"], kind="accessory", name=a["name"], description=a["description"], rarity=a["rarity"],
                 price=PRICE[a["rarity"]], sellPrice=SELL[a["rarity"]], dropWeight=DROP[a["rarity"]], minStage=0, maxStage=2,
                 stats={**{k: a["base"].get(k, 0) for k in BASE}, "effect": a["effect"]}) for a in ACCESSORIES]


def number(value):
    text = repr(float(value))
    return text[:-2] if text.endswith(".0") else text


def rule(key, value, integer, top):
    return '                    result.Add("%s",new AccessoryValueRule(%sf,%sf,%sf,%s));\n' % (
        key, number(value), "1" if integer else "0", top, "true" if integer else "false")


def schema_cases(eol):
    out = ""
    for a in ACCESSORIES:
        out += '                case "%s":\n' % a["id"]
        for key in BASE:
            out += rule(key, a["base"].get(key, 0), False, "5" if key in FRACTION_BASE else "1000")
        for key, value in a["effect"].items():
            integer = key in INTEGER
            fraction = key.endswith("Fraction") or key in {"slowFactor", "healthThreshold", "hopDamageMultiplier"}
            out += rule("effect." + key, value, integer, "32" if integer else "5" if fraction else "1000")
        out += "                    break;\n"
    return out.replace("\n", eol)


def csharp_text(text):
    return json.dumps(text, ensure_ascii=False)


def read(path):
    with open(path, encoding="utf-8", newline="") as handle:
        return handle.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text)


def main():
    runtime = ROOT / "Assets/RuinRunner/Runtime"
    schema = runtime / "Backend/AccessoryTuningSchema.cs"
    source = read(schema)
    eol = "\r\n" if "\r\n" in source else "\n"
    first_set = source.rfind("\n", 0, source.index('case "set.')) + 1
    own = source.find('                case "' + ACCESSORIES[0]["id"] + '":')
    write(schema, source[:own if own >= 0 else first_set] + schema_cases(eol) + source[first_set:])

    loot = runtime / "Data/AccessoryLootTable.cs"
    source = read(loot)
    eol = "\r\n" if "\r\n" in source else "\n"
    last_old = 'new AccessoryDefinition("mana_aegis"'
    close = "new Color(.3f,.6f,1))"
    start = source.index(close, source.index(last_old)) + len(close)
    end = source.index("};", start)
    rows = "".join(",%s            new AccessoryDefinition(\"%s\",AccessoryType.%s,%s,%s,WeaponRarity.%s,ItemSetType.None,%s)" % (
        eol, a["id"], a["enum"], csharp_text(a["name"]), csharp_text(a["description"]), RARITIES[a["rarity"]], COLOR[a["rarity"]]) for a in ACCESSORIES)
    write(loot, source[:start] + rows + eol + "        " + source[end:])

    definition = runtime / "Data/AccessoryDefinition.cs"
    source = read(definition)
    if ACCESSORIES[0]["enum"] not in source:
        eol = "\r\n" if "\r\n" in source else "\n"
        start = source.index("enum AccessoryType")
        close = source.index("}", start)
        body = source[start:close].rstrip()
        if not body.endswith(","):
            body += ","
        added = "".join(eol + "        " + a["enum"] + "," for a in ACCESSORIES).rstrip(",")
        write(definition, source[:start] + body + added + eol + "    " + source[close:])
    print("wrote", len(ACCESSORIES))


if __name__ == "__main__":
    main()
