using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public static partial class WeaponLootTable
    {
        // The 70 signature weapons are what a run can hold. Everything rolled, sold or listed comes from here.
        private static WeaponDefinition[] Weapons = SignatureWeaponCatalog.All;

        public static int Count => Weapons.Length;
        public static IReadOnlyList<WeaponDefinition> All => Weapons;

        public static WeaponDefinition Find(string id)
        {
            foreach (var weapon in Weapons) if (weapon.Id == id) return weapon;
            if (SignatureWeaponCatalog.Has(id)) return SignatureWeaponCatalog.Find(id);
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
            return WeightedWeapon(EligibleNearest(RollRarity(luck)));
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
