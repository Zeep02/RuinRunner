using System;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Authored demonstration pool. Full catalogs remain intact for save compatibility.</summary>
    public static class DemoContent
    {
        public static readonly string[] WeaponIds = { "rust_sword", "hand_crossbow" };
        public static readonly string[] AccessoryIds = {
            "ruby_heart", "storm_feather", "guardian_seal", "crimson_ring", "mana_prism",
            "berserker_emblem", "sapphire_flask", "guardian_plate", "winged_boots", "vampire_fang"
        };
        public static WeaponDefinition Weapon(string id)
        {
            foreach (var weapon in WeaponLootTable.All) if (weapon.Id == id) return weapon;
            throw new InvalidOperationException("Missing demo weapon: " + id);
        }
        public static bool IsAccessory(string id) => Array.IndexOf(AccessoryIds, id) >= 0;
        public static bool IsWeapon(string id) => Array.IndexOf(WeaponIds, id) >= 0;
        public static WeaponDefinition ChestWeapon(int region) => Weapon(WeaponIds[region % 2 == 0 ? 1 : 0]);
        public static AccessoryDefinition[] Choices(int offer, int count = 6)
        {
            count = Mathf.Clamp(count, 0, AccessoryIds.Length);
            var result = new AccessoryDefinition[count];
            for (var i = 0; i < count; i++)
                result[i] = AccessoryLootTable.Find(AccessoryIds[(Mathf.Max(0, offer) * 3 + i) % AccessoryIds.Length]);
            return result;
        }
        public static EnemyPlacementData[] Enemies(int region)
        {
            // Each pair is a wave; StageLayoutDefinition supplies safe fixed positions.
            EnemyType[] types;
            switch (region)
            {
                case 0: types = new[] { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Leaper }; break;
                case 1: types = new[] { EnemyType.Grunt, EnemyType.Leaper, EnemyType.Leaper, EnemyType.Grunt }; break;
                case 2: types = new[] { EnemyType.Grunt, EnemyType.Leaper, EnemyType.Charger, EnemyType.Leaper, EnemyType.Grunt, EnemyType.Grunt }; break;
                default: types = new[] { EnemyType.Boss }; break;
            }
            var result = new EnemyPlacementData[types.Length];
            for (var i = 0; i < types.Length; i++) result[i] = new EnemyPlacementData(types[i], 0, 0) { wave = i / 2 };
            return result;
        }
    }
}
