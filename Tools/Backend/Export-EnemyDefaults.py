"""Write the admin tool's monster table from the game's code defaults.

Reads Assets/RuinRunner/Runtime/Data/GameBalanceConfig.cs (the per-type table and the global
multipliers) and writes Backend/RuinRunner.Api/Admin/enemy-defaults.json. Run it after changing
those defaults so the admin page starts from the numbers the game actually uses.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/RuinRunner/Runtime/Data/GameBalanceConfig.cs"
TARGET = ROOT / "Backend/RuinRunner.Api/Admin/enemy-defaults.json"

# Admin-facing labels only; the game itself never shows these.
LABELS = {
    "Grunt": ("돌격병", 1), "Shooter": ("사수", 1), "Charger": ("돌진수", 1), "Leaper": ("도약수", 1),
    "Boss": ("유적의 파수꾼 (1보스)", 1), "Tidekeeper": ("물길의 수문장 (2보스)", 2), "SealHeart": ("봉인의 심장 (3보스)", 3),
    "Tidecaller": ("조수 부름꾼", 2), "Sentinel": ("감시자", 2), "Sporeling": ("포자충", 1), "RuneTotem": ("룬 토템", 1),
    "TideCrab": ("조수 게", 2), "Wisp": ("도깨비불", 2), "SealKnight": ("봉인 기사", 3), "Hexer": ("주술사", 3),
    "BoneShield": ("뼈 방패병", 1), "VineLasher": ("덩굴 채찍", 1), "RuinRat": ("유적 쥐", 1), "MossArcher": ("이끼 궁수", 1),
    "PowderBeetle": ("화약 딱정벌레", 1), "Drowned": ("익사체", 2), "EelSpitter": ("뱀장어 사수", 2), "CoralGuard": ("산호 수호병", 2),
    "JellyLamp": ("해파리 등불", 2), "TideSiren": ("조수 세이렌", 2), "InkShade": ("먹그림자", 3), "SealGolem": ("봉인 골렘", 3),
    "GildedBolter": ("금박 석궁병", 3), "ChainWarden": ("사슬 간수", 3), "SealPriest": ("봉인 사제", 3),
}
BOSSES = {"Boss", "Tidekeeper", "SealHeart"}


def main() -> int:
    text = SOURCE.read_text(encoding="utf-8")
    table = text[text.index("public EnemyBalanceData[] enemies"):text.index("public WeaponBalanceData[] weapons")]
    rows = []
    for m in re.finditer(r"new EnemyBalanceData\(EnemyType\.(\w+),\s*([\d.]+)f?,\s*([\d.]+)f?,\s*([\d.]+)f?,\s*([\d.]+)f?,\s*(\d+)\)", table):
        kind = m.group(1)
        label, stage = LABELS.get(kind, (kind, 0))
        rows.append({
            "type": kind, "label": label, "stage": stage, "boss": kind in BOSSES,
            "maxHealth": float(m.group(2)), "attackPower": float(m.group(3)), "armor": float(m.group(4)),
            "moveSpeed": float(m.group(5)), "experienceReward": int(m.group(6)),
        })

    def knob(name: str) -> float:
        found = re.search(r"public float " + name + r" = ([\d.]+)f;", text)
        if not found:
            raise SystemExit("missing " + name)
        return float(found.group(1))

    data = {
        "enemyHealthMultiplier": knob("enemyHealthMultiplier"),
        "bossHealthMultiplier": knob("bossHealthMultiplier"),
        "experienceGainMultiplier": knob("experienceGainMultiplier"),
        "bossBurstThreshold": knob("bossBurstThreshold"),
        "bossBurstSeconds": knob("bossBurstSeconds"),
        "bossItemDamageMultiplier": knob("bossItemDamageMultiplier"),
        "enemies": rows,
    }
    if len(rows) != len(LABELS):
        raise SystemExit(f"expected {len(LABELS)} monster rows, read {len(rows)}")
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(f"wrote {len(rows)} monsters to {TARGET.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
