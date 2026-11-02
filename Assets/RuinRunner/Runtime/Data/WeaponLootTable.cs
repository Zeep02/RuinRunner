using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public static partial class WeaponLootTable
    {
        private static WeaponDefinition[] Weapons =
        {
            Melee("worn_dagger", "닳은 단검", WeaponRarity.Common, WeaponFamily.Dagger, 21.12f, .68f, WeaponSpecialType.Nova, 24f, "짧은 연속 베기"),
            Melee("rust_sword", "낡은 장검", WeaponRarity.Common, WeaponFamily.Longsword, 25.92f, 1f, WeaponSpecialType.ArcWave, 28f, "짧게 받아내고 반격"),
            Melee("iron_mace", "철제 메이스", WeaponRarity.Common, WeaponFamily.Mace, 29.28f, 1.15f, WeaponSpecialType.Nova, 32f, "작은 충격파를 방출"),
            Melee("training_katana", "수련용 도", WeaponRarity.Common, WeaponFamily.Katana, 24.00f, .86f, WeaponSpecialType.ArcWave, 27f, "빠른 발도 검기"),
            Ranged("hand_crossbow", "손쇠뇌", WeaponRarity.Common, WeaponFamily.Crossbow, 27.36f, 1.2f, 5, 1.35f, 15f, new Vector2(.5f, .2f), 1, WeaponSpecialType.ArcWave, 30f, "넓은 철제 볼트를 발사"),
            Ranged("scrap_pistol", "고철 권총", WeaponRarity.Common, WeaponFamily.Pistol, 24.00f, .82f, 10, 1.25f, 18f, new Vector2(.38f, .18f), 1, WeaponSpecialType.PiercingBolt, 26f, "정밀 관통탄"),
            Ranged("scrap_shotgun", "고철 산탄총", WeaponRarity.Common, WeaponFamily.Shotgun, 31.68f, 1.3f, 4, 1.7f, 13f, new Vector2(.72f, .38f), 1, WeaponSpecialType.Nova, 33f, "근거리 폭발 사격"),

            Melee("wind_dagger", "바람 단검", WeaponRarity.Uncommon, WeaponFamily.Dagger, 25.20f, .72f, WeaponSpecialType.Nova, 24f, "주변을 연속 베기", ItemSetType.Storm),
            Melee("knight_sword", "기사의 장검", WeaponRarity.Uncommon, WeaponFamily.Longsword, 30.00f, .96f, WeaponSpecialType.ArcWave, 29f, "받아내기 성공 시 강한 반격", ItemSetType.Guardian),
            Melee("iron_hammer", "철벽 망치", WeaponRarity.Uncommon, WeaponFamily.Mace, 34.08f, 1.2f, WeaponSpecialType.Nova, 34f, "충격파를 방출", ItemSetType.Guardian),
            Melee("iron_greatsword", "무쇠 대검", WeaponRarity.Uncommon, WeaponFamily.Greatsword, 37.92f, 1.32f, WeaponSpecialType.ArcWave, 36f, "무거운 지면 가르기"),
            Ranged("repeater", "연발 쇠뇌", WeaponRarity.Uncommon, WeaponFamily.Crossbow, 18.72f, .58f, 14, 1.7f, 16f, new Vector2(.46f, .14f), 1, WeaponSpecialType.PiercingBolt, 28f, "고속 관통 볼트"),
            Ranged("flintlock", "유적 화승총", WeaponRarity.Uncommon, WeaponFamily.Pistol, 35.52f, 1.25f, 6, 1.55f, 17f, new Vector2(.42f, .24f), 1, WeaponSpecialType.Nova, 32f, "폭발성 탄환"),
            Ranged("scattergun", "분쇄 산탄총", WeaponRarity.Uncommon, WeaponFamily.Shotgun, 37.20f, 1.35f, 5, 1.75f, 14f, new Vector2(.82f, .42f), 2, WeaponSpecialType.Nova, 34f, "분쇄 충격탄"),

            Melee("quake_mace", "대지진 철퇴", WeaponRarity.Rare, WeaponFamily.Mace, 41.28f, 1.15f, WeaponSpecialType.Nova, 32f, "대지 충격파", ItemSetType.Guardian),
            Melee("moon_blade", "월광도", WeaponRarity.Rare, WeaponFamily.Katana, 37.20f, .86f, WeaponSpecialType.ArcWave, 26f, "강화된 월광 검기", ItemSetType.Arcanist, "moon_iai", 2.05f),
            Melee("dragon_slayer", "용살 대검", WeaponRarity.Rare, WeaponFamily.Greatsword, 46.08f, 1.38f, WeaponSpecialType.ArcWave, 38f, "거대한 용살 참격"),
            Ranged("siege_crossbow", "공성 쇠뇌", WeaponRarity.Rare, WeaponFamily.Crossbow, 42.72f, 1.25f, 7, 1.65f, 17f, new Vector2(.86f, .22f), 3, WeaponSpecialType.PiercingBolt, 31f, "중장갑 관통 볼트"),
            Ranged("twin_pistols", "쌍둥이 권총", WeaponRarity.Rare, WeaponFamily.Pistol, 26.40f, .45f, 20, 1.4f, 20f, new Vector2(.38f, .18f), 1, WeaponSpecialType.PiercingBolt, 27f, "교차 속사"),
            Ranged("storm_rifle", "폭풍 소총", WeaponRarity.Rare, WeaponFamily.Rifle, 30.72f, .55f, 18, 1.6f, 19f, new Vector2(.5f, .2f), 1, WeaponSpecialType.PiercingBolt, 25f, "번개 관통탄", ItemSetType.Storm),

            Melee("shadow_fang", "그림자 송곳니", WeaponRarity.Epic, WeaponFamily.Dagger, 39.60f, .58f, WeaponSpecialType.Nova, 25f, "그림자 난무", ItemSetType.BloodMoon, "shadow_dance", 1.95f),
            Melee("blood_moon_katana", "혈월도", WeaponRarity.Epic, WeaponFamily.Katana, 45.60f, .72f, WeaponSpecialType.ArcWave, 28f, "핏빛 발도", ItemSetType.BloodMoon, "moon_iai", 2.15f),
            Melee("blood_reaver", "핏빛 약탈자", WeaponRarity.Epic, WeaponFamily.Greatsword, 46.80f, .78f, WeaponSpecialType.Nova, 30f, "대형 혈풍 참격", ItemSetType.BloodMoon),
            Ranged("void_revolver", "공허 리볼버", WeaponRarity.Epic, WeaponFamily.Pistol, 51.60f, .88f, 6, 1.25f, 22f, new Vector2(.48f, .24f), 3, WeaponSpecialType.PiercingBolt, 24f, "공허 관통탄"),
            Ranged("dragon_cannon", "용숨결 대포", WeaponRarity.Epic, WeaponFamily.Cannon, 56.40f, 1.5f, 3, 2f, 11f, new Vector2(1.05f, .55f), 3, WeaponSpecialType.Nova, 38f, "화염 폭풍을 방출"),

            Melee("sun_oath", "태양의 맹세", WeaponRarity.Legendary, WeaponFamily.Longsword, 56.40f, .72f, WeaponSpecialType.ArcWave, 22f, "넓은 받아내기 반격", ItemSetType.Arcanist, "sun_oath", 2.4f),
            Ranged("starbreaker", "별을 부수는 자", WeaponRarity.Legendary, WeaponFamily.Rifle, 60.00f, .68f, 24, 1.1f, 22f, new Vector2(.75f, .3f), 4, WeaponSpecialType.PiercingBolt, 22f, "별빛 관통포"),

            // Distinct native silhouettes and handling choices expand the normal reward pool.
            // Elemental-looking materials do not promise unimplemented elemental damage.
            Melee("cinder_tanto", "붉은 단도", WeaponRarity.Common, WeaponFamily.Dagger, 23.4f, .79f, WeaponSpecialType.Nova, 24f, "짧은 연격과 전진 찌르기"),
            Melee("azure_dirk", "청강 단검", WeaponRarity.Rare, WeaponFamily.Dagger, 30.8f, .65f, WeaponSpecialType.Nova, 23f, "빠른 연격과 전진 찌르기", ItemSetType.Storm),
            Melee("tide_sword", "해무 장검", WeaponRarity.Uncommon, WeaponFamily.Longsword, 27.6f, .86f, WeaponSpecialType.ArcWave, 27f, "빠른 연속 베기와 받아내기 반격", ItemSetType.Guardian),
            Melee("ember_sword", "화문 장검", WeaponRarity.Rare, WeaponFamily.Longsword, 38.4f, 1.04f, WeaponSpecialType.ArcWave, 30f, "묵직한 연속 베기와 받아내기 반격"),
            Melee("sentinel_cleaver", "파수꾼 절단검", WeaponRarity.Uncommon, WeaponFamily.Greatsword, 34.6f, 1.12f, WeaponSpecialType.ArcWave, 33f, "넓은 베기와 충전 내려찍기", ItemSetType.Guardian),
            Melee("glacier_greatsword", "빙정 대검", WeaponRarity.Rare, WeaponFamily.Greatsword, 48f, 1.46f, WeaponSpecialType.ArcWave, 37f, "느리지만 강한 충전 내려찍기"),
            Ranged("copper_revolver", "황동 리볼버", WeaponRarity.Common, WeaponFamily.Pistol, 28.8f, 1f, 6, 1.05f, 19f, new Vector2(.38f,.18f), 1, WeaponSpecialType.PiercingBolt, 26f, "짧은 장전과 3연발 사격"),
            Ranged("spark_pistol", "점화 권총", WeaponRarity.Uncommon, WeaponFamily.Pistol, 25.2f, .65f, 12, 1.55f, 20f, new Vector2(.38f,.18f), 1, WeaponSpecialType.PiercingBolt, 27f, "빠른 단발과 3연발 사격"),
            Ranged("signal_pistol", "신호 권총", WeaponRarity.Rare, WeaponFamily.Pistol, 42f, 1.12f, 4, 1.2f, 17f, new Vector2(.48f,.24f), 2, WeaponSpecialType.Nova, 30f, "강한 한 발과 집중 사격"),
            Ranged("scout_rifle", "정찰 소총", WeaponRarity.Uncommon, WeaponFamily.Rifle, 25.8f, .62f, 14, 1.4f, 22f, new Vector2(.46f,.18f), 1, WeaponSpecialType.PiercingBolt, 28f, "빠른 탄속과 직선 관통 사격"),
            Ranged("coil_rifle", "코일 소총", WeaponRarity.Rare, WeaponFamily.Rifle, 36f, .76f, 10, 1.55f, 24f, new Vector2(.5f,.2f), 2, WeaponSpecialType.PiercingBolt, 29f, "적을 꿰뚫는 묵직한 단발"),
            Ranged("breach_shotgun", "돌파 산탄총", WeaponRarity.Uncommon, WeaponFamily.Shotgun, 42f, 1.6f, 2, 1.05f, 14f, new Vector2(.72f,.32f), 1, WeaponSpecialType.Nova, 32f, "두 발 뒤 빠르게 재장전"),
            Ranged("twin_bore", "쌍열 산탄총", WeaponRarity.Rare, WeaponFamily.Shotgun, 35.4f, .95f, 2, 1.7f, 15f, new Vector2(.68f,.32f), 2, WeaponSpecialType.Nova, 33f, "빠른 두 발과 넓은 집중 산탄"),
            Ranged("siege_mortar", "공성포", WeaponRarity.Uncommon, WeaponFamily.Cannon, 43.2f, 1.75f, 1, 1.15f, 10f, new Vector2(.8f,.45f), 1, WeaponSpecialType.Nova, 36f, "한 발씩 장전하는 폭발 포탄"),
            Ranged("sun_cannon", "황동 연사포", WeaponRarity.Rare, WeaponFamily.Cannon, 39.6f, 1.1f, 5, 2.25f, 12f, new Vector2(.82f,.46f), 1, WeaponSpecialType.Nova, 35f, "연속 포격 뒤 긴 재장전"),
            Melee("returning_edge", "되돌아오는 칼날", WeaponRarity.Epic, WeaponFamily.Dagger, 34f, .66f, WeaponSpecialType.ArcWave, 14, "왕복 칼날", ItemSetType.Storm),
            Melee("gravity_maul", "끌어당기는 철퇴", WeaponRarity.Epic, WeaponFamily.Mace, 43f, 1.12f, WeaponSpecialType.Nova, 22, "중력 우물", ItemSetType.Guardian),
            Ranged("tether_carbine", "연결의 기병총", WeaponRarity.Epic, WeaponFamily.Rifle, 32f, .67f, 12, 1.5f, 20, new Vector2(.42f,.18f), 1, WeaponSpecialType.PiercingBolt, 18, "작살 연결", ItemSetType.Storm),
            Ranged("bastion_shotgun", "보루 산탄총", WeaponRarity.Legendary, WeaponFamily.Shotgun, 45f, 1.15f, 4, 1.65f, 14, new Vector2(.5f,.24f), 1, WeaponSpecialType.Nova, 20, "포획 방패", ItemSetType.Guardian)
        };

        public static int Count => Weapons.Length;
        public static IReadOnlyList<WeaponDefinition> All => Weapons;

        public static WeaponDefinition Find(string id)
        {
            foreach (var weapon in Weapons) if (weapon.Id == id) return weapon;
            throw new System.ArgumentException("Unknown weapon id: " + id, nameof(id));
        }

        public static void ApplyBalance(GameBalanceConfig config)
        {
            if(!string.IsNullOrEmpty(PinnedCatalogVersion))return;
            foreach (var weapon in Weapons)
                weapon.ApplyBalance(config == null ? null : config.GetWeapon(weapon.Id));
        }

        public static WeaponDefinition FindFirst(WeaponAttackType attackType)
        {
            foreach (var weapon in Weapons)
                if (weapon.AttackType == attackType) return weapon;
            return null;
        }

        public static WeaponDefinition FindFirst(WeaponFamily family)
        {
            foreach (var weapon in Weapons)
                if (weapon.Family == family) return weapon;
            return null;
        }

        private static WeaponDefinition Melee(string id, string name, WeaponRarity rarity, WeaponFamily family, float damage, float delay, WeaponSpecialType special, float mana, string description, ItemSetType setType = ItemSetType.None, string comboProfileId = "", float heavyMultiplier = 1f)
        {
            return new WeaponDefinition(id, name, rarity, damage, delay, RarityColor(rarity), WeaponAttackType.Melee, 0, 0f, 1f, Vector2.one, 1, special, mana, description, setType, family, comboProfileId, heavyMultiplier, GuardReduction(family));
        }

        private static WeaponDefinition Ranged(string id, string name, WeaponRarity rarity, WeaponFamily family, float damage, float delay, int magazine, float reload, float speed, Vector2 size, int pierce, WeaponSpecialType special, float mana, string description, ItemSetType setType = ItemSetType.None)
        {
            return new WeaponDefinition(id, name, rarity, damage, delay, RarityColor(rarity), WeaponAttackType.Ranged, magazine, reload, speed, size, pierce, special, mana, description, setType, family);
        }

        private static float GuardReduction(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Dagger: return .3f;
                case WeaponFamily.Mace: return .5f;
                case WeaponFamily.Greatsword: return .55f;
                case WeaponFamily.Katana: return .38f;
                default: return .44f;
            }
        }

        private static Color RarityColor(WeaponRarity rarity)
        {
            switch (rarity)
            {
                case WeaponRarity.Uncommon: return new Color(.25f, .9f, .42f);
                case WeaponRarity.Rare: return new Color(.25f, .55f, 1f);
                case WeaponRarity.Epic: return new Color(.75f, .25f, 1f);
                case WeaponRarity.Legendary: return new Color(1f, .62f, .12f);
                default: return new Color(.75f, .75f, .75f);
            }
        }

        public static WeaponDefinition Roll(float luck = 0f)
        {
            if(!string.IsNullOrEmpty(PinnedCatalogVersion))return WeightedWeapon(EligibleWeapons(),luck);
            var rarity = RollRarity(luck);
            var candidates = new List<WeaponDefinition>();
            foreach (var weapon in Weapons)
            {
                if (weapon.Rarity == rarity) candidates.Add(weapon);
            }

            if (candidates.Count == 0) return Weapons[0];
            return WeightedWeapon(candidates);
        }

        public static WeaponRarity RollRarity(float luck)
        {
            var roll = Mathf.Clamp(Random.value * 100f - Mathf.Max(0f, luck) * .75f, 0f, 100f);
            if (roll < 2f) return WeaponRarity.Legendary;
            if (roll < 8f) return WeaponRarity.Epic;
            if (roll < 22f) return WeaponRarity.Rare;
            if (roll < 50f) return WeaponRarity.Uncommon;
            return WeaponRarity.Common;
        }

        public static float GetDoubleDropChance(float luck)
        {
            return Mathf.Clamp01(.05f + Mathf.Max(0f, luck) * .015f);
        }
    }
}
