"""One-time campaign-v1 -> campaign-v2 migration from reviewed C# values.

Published JSON is the editable source for NEW catalog versions. This script never
overwrites published bytes and emits frozen v1 compatibility rules for the client.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ATTRIBUTES = dict.fromkeys(("maxHealth", "maxMana", "armor", "moveSpeed", "criticalChance", "criticalDamage", "luck", "manaRegeneration", "lifeOnKill", "manaOnKill", "weaponDamageBonus", "specialDamageBonus"), 0)
BONUSES = {
    "ruby_heart": {"maxHealth": 18}, "sapphire_flask": {"maxMana": 18}, "assassin_mark": {"criticalChance": .06},
    "executioners_coin": {"criticalDamage": .25}, "guardian_plate": {"armor": 6}, "four_leaf_clover": {"luck": 3},
    "winged_boots": {"moveSpeed": .55}, "arcane_lens": {"specialDamageBonus": .18}, "vampire_fang": {"lifeOnKill": 1.5},
    "soul_capacitor": {"manaRegeneration": 1.2, "manaOnKill": 2}, "crimson_ring": {"criticalChance": .05},
    "storm_feather": {"moveSpeed": .4}, "guardian_seal": {"armor": 5}, "mana_prism": {"maxMana": 14},
    "lucky_dice": {"luck": 2}, "berserker_emblem": {"weaponDamageBonus": .1}, "bulwark_brooch": {"maxHealth": 16},
    "sage_sigil": {"manaRegeneration": 1, "specialDamageBonus": .1}, "mana_thread": {"maxMana": 6},
    "guard_bell": {"armor": 2}, "rush_spur": {"moveSpeed": .15}, "steady_locket": {"criticalChance": .02},
    "powder_satchel": {"specialDamageBonus": .04}, "last_light_pendant": {"maxHealth": 8},
}
EFFECTS = {
    "berserker_emblem": dict(hitCount=3, legacyHitCount=2, damageFraction=.12, extraCopyDamageFraction=.03, maxMarks=12, tickCount=3, tickInterval=.6),
    "sage_sigil": dict(cooldownSeconds=6, durationSeconds=2.4, damageFraction=.24, extraCopyDamageFraction=.04, orbitCount=2, targetCount=4, legacyTargetCount=5, orbitRadius=1.15, angularSpeed=5, hitRadius=.25, detectionRadius=1.65),
    "arcane_lens": dict(cooldownSeconds=1.2, windowSeconds=2, damageFraction=.30, extraCopyDamageFraction=.04, releaseDelaySeconds=.08, range=3, targetCount=1),
    "vampire_fang": dict(hitCount=6, legacyHitCount=5, cooldownSeconds=2, maxShards=3, heal=2, extraCopyHeal=.5, durationSeconds=8, pickupRadius=.65, pickupDelaySeconds=.28),
    "soul_capacitor": dict(cooldownSeconds=3, damageFraction=.35, extraCopyDamageFraction=.05, targetCount=3, legacyTargetCount=4, initialRange=4, hopRange=2.5, hopDamageMultiplier=.8, push= .6),
    "mana_thread": dict(hitCount=4, legacyHitCount=3, cooldownSeconds=1.5, mana=4, extraCopyMana=1),
    "guard_bell": dict(cooldownSeconds=4, legacyCooldownSeconds=3, mana=4, extraCopyMana=1),
    "rush_spur": dict(cooldownSeconds=3, legacyCooldownSeconds=2.4, windowSeconds=2, damageFraction=.3, extraCopyDamageFraction=.05, push=5),
    "steady_locket": dict(preparationSeconds=.75, legacyPreparationSeconds=.55, cooldownSeconds=2, damageFraction=.35, extraCopyDamageFraction=.05),
    "powder_satchel": dict(cooldownSeconds=4, legacyCooldownSeconds=3, range=1.8, targetCount=3, damageFraction=.22, extraCopyDamageFraction=.04, push=4),
    "last_light_pendant": dict(healthThreshold=.35, legacyHealthThreshold=.45, cooldownSeconds=10, durationSeconds=3, shield=10, extraCopyShield=2),
    "recall_spool": dict(windowSeconds=2, range=2.8, width=1, damageFraction=.42, cooldownSeconds=3),
    "cadence_clasp": dict(windowSeconds=3, cooldownSeconds=4, range=3.2, width=1.2, damageFraction=.4, push=8, manaLimit=6, manaRefundFraction=.5),
    "prism_cage": dict(windowSeconds=.6, cooldownSeconds=4, maxProjectiles=3, catchRadius=.9, facingDot=.2, damageCapFraction=1.2, returnedDamageMultiplier=1.5, projectileSpeed=14, fanAngle=16, projectileWidth=.24, projectileHeight=.18, pierce=1, spawnOffset=.25),
    "trail_spindle": dict(markCount=3, requiredMarkCount=2, markSpacing=1.25, width=.7, damageFraction=.35, cooldownSeconds=3),
    "focus_prism": dict(hitCount=3, range=5, damageFraction=.55, cooldownSeconds=3),
    "mana_aegis": dict(manaCost=12, absorbDamage=10, cooldownSeconds=5),
}
BASE_LABELS = {
    "maxHealth": "최대 체력 +{maxHealth}", "maxMana": "최대 마나 +{maxMana}", "armor": "방어력 +{armor}",
    "moveSpeed": "이동 속도 +{moveSpeed}", "criticalChance": "치명타 확률 +{criticalChance:p}%", "criticalDamage": "치명타 피해 +{criticalDamage:p}%",
    "luck": "운 +{luck}", "manaRegeneration": "마나 재생 +{manaRegeneration}", "lifeOnKill": "처치 시 체력 +{lifeOnKill}",
    "manaOnKill": "처치 시 마나 +{manaOnKill}", "weaponDamageBonus": "공격력 +{weaponDamageBonus:p}%", "specialDamageBonus": "특수공격 피해 +{specialDamageBonus:p}%",
}
SETS = [
    ("blood_moon", 2, "핏빛 서약", dict(lifeOnKill=1.5), dict(hitCount=3, damageFraction=.08, maxMarks=12, tickCount=3, tickInterval=.6), "처치 회복 +{lifeOnKill}. 같은 적에 원본 {effect.hitCount}적중: {effect.damageFraction:p}% 출혈 {effect.tickCount}회/{effect.tickInterval}초, 최대 {effect.maxMarks}표식. 문장과 중복 적용 없음."),
    ("blood_moon", 3, "붉은 달", dict(weaponDamageBonus=.15), dict(spreadRange=2, spreadCount=2, spreadDamageFraction=.75), "공격력 +{weaponDamageBonus:p}%. 출혈 적을 원본 공격으로 처치하면 {effect.spreadRange}칸 내 {effect.spreadCount}적에게 {effect.spreadDamageFraction:p}% 출혈 전파. 발동 피해 처치는 전파하지 않음."),
    ("storm", 2, "순풍", dict(moveSpeed=.5, criticalChance=.04), dict(cooldownSeconds=1.1, damageFraction=.22, markSpacing=.6, maxWakes=5, durationSeconds=.45, targetCount=3, hitRadius=.65), "속도 +{moveSpeed}, 치명타 +{criticalChance:p}%. 대시 경로에 {effect.durationSeconds}초 바람: 특수 기준 {effect.damageFraction:p}%, 최대 {effect.targetCount}적·각 1회. 재사용 {effect.cooldownSeconds}초."),
    ("storm", 3, "폭풍의 눈", dict(specialDamageBonus=.25), dict(mana=3), "특수공격 +{specialDamageBonus:p}%. 대시 바람이 적중하면 대시당 마나 {effect.mana}을 1회 회복. 회복은 소비 효과를 발동하지 않음."),
    ("guardian", 2, "철벽", dict(armor=8), dict(cooldownSeconds=8, durationSeconds=5, shield=8), "방어 +{armor}. 가드·패링 성공 후 피해 {effect.shield} 흡수 보호막, {effect.durationSeconds}초 유지·재사용 {effect.cooldownSeconds}초. 보스 공격에도 적용."),
    ("guardian", 3, "불굴", dict(maxHealth=30), dict(cooldownSeconds=3, range=1.8, targetCount=3, damageFraction=.4, armorDamageFraction=.3, push=5), "최대 체력 +{maxHealth}. 방어 성공 시 전방 {effect.range}칸 최대 {effect.targetCount}적 반격: 특수 기준 {effect.damageFraction:p}% + 방어력 {effect.armorDamageFraction:p}%. 재사용 {effect.cooldownSeconds}초·벽 차단·재발동 없음."),
    ("arcanist", 2, "마력 공명", dict(manaRegeneration=1.5), dict(castCount=3, mana=1), "초당 마나 +{manaRegeneration}. 마나를 소비한 특수 행동 {effect.castCount}회마다 마나 {effect.mana} 회복. 장비 중복과 이동으로 횟수 초기화 없음."),
    ("arcanist", 3, "대마도", dict(maxMana=30, specialDamageBonus=.15), dict(manaRefundFraction=.3, manaLimit=4, lensTargetCount=3), "최대 마나 +{maxMana}, 특수공격 +{specialDamageBonus:p}%. 소비 회복량: 마지막 비용의 {effect.manaRefundFraction:p}%, 최대 {effect.manaLimit}. 렌즈 메아리 대상 최대 {effect.lensTargetCount}. 회복·발동 피해는 재발동하지 않음."),
]
DESCRIPTIONS = {
    "berserker_emblem": "같은 적에 원본 공격 {effect.hitCount}회 적중: {effect.tickInterval}초마다 해당 공격 피해의 {effect.damageFraction:p}% 출혈, {effect.tickCount}회. 최대 {effect.maxMarks}표식. 사본마다 피해 +{effect.extraCopyDamageFraction:p}%p (최대 {effect.maxCopies}개). 출혈은 겹치지 않고 갱신된다. 보스 적용 · 추가 피해 재발동 없음.",
    "sage_sigil": "마나 소비: {effect.durationSeconds}초간 반경 {effect.orbitRadius}의 마력구 {effect.orbitCount}개, 최대 {effect.targetCount}적에 각 1회. 특수 기준 피해 {effect.damageFraction:p}%, 사본마다 +{effect.extraCopyDamageFraction:p}%p (최대 {effect.maxCopies}개). 재사용 {effect.cooldownSeconds}초 · 벽 차단 · 추가 피해 재발동 없음.",
    "arcane_lens": "특수행동 지점에 {effect.windowSeconds}초 동안 렌즈를 남긴다. 다음 직접 적중 시 렌즈에서 목표 방향 {effect.range}칸으로 메아리. 피해 {effect.damageFraction:p}%, 사본마다 +{effect.extraCopyDamageFraction:p}%p (최대 {effect.maxCopies}개). 재사용 {effect.cooldownSeconds}초 · 벽 차단 · 추가 효과 재발동 없음.",
    "vampire_fang": "처치 또는 원본 적중 {effect.hitCount}회: 체력 {effect.heal} 회수 파편, {effect.durationSeconds}초 유지 · 최대 {effect.maxShards}개. 재사용 {effect.cooldownSeconds}초 · {effect.pickupRadius}칸 접근 시 회수. 사본마다 회복 +{effect.extraCopyHeal} (최대 {effect.maxCopies}개). 보스는 적중으로 생성.",
    "soul_capacitor": "마나 소비: 전방 {effect.initialRange}칸부터 최대 {effect.targetCount}적 연쇄 번개, 다음 간격 {effect.hopRange}칸. 특수 기준 피해 {effect.damageFraction:p}%, 다음 적마다 ×{effect.hopDamageMultiplier}. 사본마다 피해 +{effect.extraCopyDamageFraction:p}%p (최대 {effect.maxCopies}개). 재사용 {effect.cooldownSeconds}초 · 벽 차단 · 추가 피해 재발동 없음.",
    "mana_thread": "공격 {effect.hitCount}회 적중하면 마나 {effect.mana} 회복. 산탄·연사·여러 적도 공격당 1회만 센다. 재사용 {effect.cooldownSeconds}초 · 사본마다 회복 +{effect.extraCopyMana} (최대 {effect.maxCopies}개).",
    "guard_bell": "가드 또는 패링 성공 시 마나 {effect.mana} 회복. 재사용 {effect.cooldownSeconds}초 · 사본마다 회복 +{effect.extraCopyMana} (최대 {effect.maxCopies}개).",
    "rush_spur": "대시 후 {effect.windowSeconds}초 안에 처음 맞힌 적을 밀치고 해당 공격 피해의 {effect.damageFraction:p}% 추가. 재사용 {effect.cooldownSeconds}초 · 사본마다 피해 +{effect.extraCopyDamageFraction:p}%p (최대 {effect.maxCopies}개). 벽 차단 · 보스 적용.",
    "steady_locket": "{effect.preparationSeconds}초 정지 후 처음 맞힌 1적에게 해당 공격 피해의 {effect.damageFraction:p}% 추가. 이동·대시하면 준비가 풀린다. 재사용 {effect.cooldownSeconds}초 · 사본마다 피해 +{effect.extraCopyDamageFraction:p}%p (최대 {effect.maxCopies}개).",
    "powder_satchel": "장착 무기의 빈 탄창 장전 완료: {effect.range}칸 안의 보이는 적 최대 {effect.targetCount}명에게 특수 기준 피해 {effect.damageFraction:p}%와 밀치기. 재사용 {effect.cooldownSeconds}초 · 사본마다 피해 +{effect.extraCopyDamageFraction:p}%p (최대 {effect.maxCopies}개). 부분 장전·다른 슬롯 장전은 발동하지 않는다.",
    "last_light_pendant": "체력 {effect.healthThreshold:p}% 이하에서 피격 시 보호막 {effect.shield}. {effect.durationSeconds}초 동안 소모량만큼 피해를 막는다. 재사용 {effect.cooldownSeconds}초 · 사본마다 보호막 +{effect.extraCopyShield} (최대 {effect.maxCopies}개). 사망 후 부활은 없다.",
    "recall_spool": "기본 마무리 궤적을 {effect.windowSeconds}초 남긴다. 다음 특수행동으로 회수해 피해 {effect.damageFraction:p}%. 궤적 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초. 벽 차단 · 추가 피해 재발동 없음.",
    "cadence_clasp": "{effect.windowSeconds}초 안에 명중 → 대시 → 특수를 잇는다. 전방 {effect.range}칸에 특수 기준 피해 {effect.damageFraction:p}%와 밀치기. 소비 마나의 {effect.manaRefundFraction:p}%, 최대 {effect.manaLimit} 회복. 재사용 {effect.cooldownSeconds}초.",
    "prism_cage": "특수행동 후 {effect.windowSeconds}초 동안 전방 탄환 {effect.maxProjectiles}개까지 포획. 다음 공격 명중 때 부채꼴 반환. 포획 피해 ×{effect.returnedDamageMultiplier}, 총합은 특수 기준 {effect.damageCapFraction:p}% 한도. 재사용 {effect.cooldownSeconds}초.",
    "trail_spindle": "걸으며 남긴 표식 {effect.requiredMarkCount}개를 다음 공격으로 잇는다. 표식 간격 {effect.markSpacing}칸 · 연결 피해 {effect.damageFraction:p}% · 재사용 {effect.cooldownSeconds}초. 벽 차단 · 추가 피해 재발동 없음.",
    "focus_prism": "같은 적을 {effect.hitCount}번 공격한 뒤 다른 적을 노리면 첫 적에게 연결 피해 {effect.damageFraction:p}%. 연결 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초. 벽 차단 · 추가 피해 재발동 없음.",
    "mana_aegis": "피격 시 마나 {effect.manaCost}를 피해 최대 {effect.absorbDamage} 방어로 바꾼다. 마나가 부족하면 발동하지 않는다. 재사용 {effect.cooldownSeconds}초. 방어 전환은 특수행동 효과를 발동하지 않는다.",
}


def rule_bounds(key, value):
    name = key.split(".")[-1]
    if name == "orbitCount":
        return 2, 2, True  # Two views are a fixed mechanic, not a new topology from data.
    if name in {"maxCopies", "maxShards", "maxProjectiles", "markCount", "requiredMarkCount", "targetCount", "legacyTargetCount", "hitCount", "legacyHitCount", "maxMarks", "tickCount", "pierce", "spreadCount", "maxWakes", "castCount", "lensTargetCount"}:
        return 1, 32, True
    if "Fraction" in name or "Multiplier" in name or name.endswith("Bonus") or name in {"criticalChance", "criticalDamage"}:
        return 0, 5, False
    if name in {"healthThreshold", "legacyHealthThreshold", "facingDot"}:
        return 0, 1, False
    if name.endswith("Seconds") or name == "tickInterval":
        return .01, 60, False
    if name in {"range", "spreadRange", "initialRange", "hopRange", "width", "orbitRadius", "hitRadius", "detectionRadius", "pickupRadius", "catchRadius", "markSpacing", "spawnOffset", "projectileWidth", "projectileHeight"}:
        return .01, 20, False
    return 0, 1000, False


def literal(value):
    return format(value, ".8g") + "f"


def main():
    source = ROOT / "Backend/Content/catalog.campaign-v1.json"
    catalog = json.loads(source.read_text(encoding="utf-8-sig"))
    catalog["version"] = "campaign-v2"
    catalog["accessoryTuningVersion"] = 1
    schema = ["// Frozen campaign-v1 numeric compatibility and schema. New balance values belong in NEW catalog JSON versions.",
              "using System;", "using System.Collections.Generic;", "namespace RuinRunner.Backend", "{",
              "    public readonly struct AccessoryValueRule", "    {", "        public readonly float Legacy, Minimum, Maximum; public readonly bool Integer;",
              "        public AccessoryValueRule(float legacy,float minimum,float maximum,bool integer=false) { Legacy=legacy; Minimum=minimum; Maximum=maximum; Integer=integer; }", "    }",
              "    public static class AccessoryTuningSchema", "    {", "        public const int Version=1;",
              "        public static Dictionary<string,AccessoryValueRule> Rules(string id)", "        {", "            var result=new Dictionary<string,AccessoryValueRule>(StringComparer.Ordinal);", "            switch(id)", "            {"]
    ids = []
    for item in catalog["items"]:
        if item["kind"] != "accessory":
            continue
        id = item["id"]
        ids.append(id)
        stats = {**ATTRIBUTES, **BONUSES.get(id, {})}
        stats["effect"] = {"maxCopies": 3, "legacyStrengthMultiplier": 1.15, **EFFECTS.get(id, {})}
        item["stats"] = stats
        item["description"] = "\n".join([", ".join(BASE_LABELS[k] for k in BONUSES.get(id, {})), DESCRIPTIONS.get(id, "")]).strip()
        flattened = {k: v for k, v in stats.items() if k != "effect"}
        flattened.update({"effect." + k: v for k, v in stats["effect"].items()})
        schema.append('                case "' + id + '":')
        for key, value in flattened.items():
            minimum, maximum, integer = rule_bounds(key, value)
            schema.append('                    result.Add("' + key + '",new AccessoryValueRule(' + ','.join([literal(value), literal(minimum), literal(maximum), str(integer).lower()]) + '));')
        schema.append("                    break;")
    catalog["itemSets"] = []
    for id, pieces, name, bonus, effect, description in SETS:
        stats = {**ATTRIBUTES, **bonus, "effect": effect}
        catalog["itemSets"].append(dict(id=id, pieces=pieces, name=name, description=description, stats=stats))
        flattened = {k: v for k, v in stats.items() if k != "effect"}
        flattened.update({"effect." + k: v for k, v in effect.items()})
        schema.append('                case "set.' + id + '.' + str(pieces) + '":')
        for key, value in flattened.items():
            minimum, maximum, integer = rule_bounds(key, value)
            schema.append('                    result.Add("' + key + '",new AccessoryValueRule(' + ','.join([literal(value), literal(minimum), literal(maximum), str(integer).lower()]) + '));')
        schema.append("                    break;")
    schema += ['                default: throw new ArgumentException("Unknown accessory tuning id: "+id);', "            }", "            return result;", "        }", "    }", "}"]
    # v1 allowed a 2-point segment despite describing 3 marks. v2 explicitly asks for all three.
    next(x for x in catalog["items"] if x["id"] == "trail_spindle")["stats"]["effect"]["requiredMarkCount"] = 3
    assert len(ids) == 30 and len(set(ids)) == 30
    output = ROOT / "Backend/Content/catalog.campaign-v2.json"
    encoded = json.dumps(catalog, ensure_ascii=False, indent=2) + "\n"
    if output.exists() and output.read_text(encoding="utf-8-sig") != encoded:
        raise RuntimeError("Refusing to overwrite an existing catalog version")
    if not output.exists():
        output.write_text(encoded, encoding="utf-8")
    draft = ROOT / "Backend/Validation/TuningDraft"
    draft.mkdir(parents=True, exist_ok=True)
    (draft / "AccessoryTuningSchema.cs").write_text("\n".join(schema) + "\n", encoding="utf-8")
    print("Created immutable campaign-v2 with 30 complete accessory stats and frozen v1 schema draft.")


if __name__ == "__main__":
    main()
