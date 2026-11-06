using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public static class RunContentValidation
    {
        public static bool Validate()
        {
            var before = UnityEngine.Random.state;
            try
            {
                var weapons = new HashSet<string>();
                var accessories = new HashSet<string>();
                var signatures = new HashSet<string>();
                for (var seed = 9100; seed < 9612; seed++)
                {
                    var a = RunContent.RollChest(seed, 2, 2, false);
                    // Content must not change with render rate or unrelated effects.
                    for (var noise = 0; noise < 11; noise++) _ = UnityEngine.Random.value;
                    var b = RunContent.RollChest(seed, 2, 2, false);
                    Require(a.Weapon == b.Weapon && a.BonusWeapon == b.BonusWeapon && a.Accessory == b.Accessory, "reward reproducibility " + seed);
                    weapons.Add(a.Weapon.Id);
                    if (a.BonusWeapon != null) weapons.Add(a.BonusWeapon.Id);
                    var first = RunContent.Choices(seed, 0, 2);
                    var second = RunContent.Choices(seed, 0, 2);
                    var unique = new HashSet<string>();
                    Require(first.Length == 6, "six choices");
                    for (var i = 0; i < first.Length; i++)
                    {
                        Require(first[i] == second[i] && unique.Add(first[i].Id), "unique deterministic choices");
                        accessories.Add(first[i].Id);
                    }
                    var encounter = RunContent.Encounters(seed, 2, false);
                    var signature = "";
                    foreach (var enemy in encounter) signature += enemy.type + ":" + enemy.wave + ",";
                    signatures.Add(signature);
                    Require(RunContent.RollChest(seed, 3, 2, true).Accessory != null, "boss accessory guarantee");
                }
                // With 14 legendaries at 2 percent between them, seeing each one takes far more rolls than the checks above make.
                for (var seed = 20000; seed < 60000 && weapons.Count < WeaponLootTable.Count; seed++)
                    weapons.Add(RunContent.RollChest(seed, 2, 2, false).Weapon.Id);
                Require(weapons.Count == WeaponLootTable.Count, "all weapons reachable " + weapons.Count);
                Require(accessories.Count == AccessoryLootTable.Count, "all accessories reachable " + accessories.Count);
                Require(signatures.Count > 100, "encounter variation");
                foreach (var weapon in WeaponLootTable.All)
                {
                    Require(InventoryIconFactory.Get(weapon.Id, weapon.Color, InventoryIconKind.Weapon) != null, "weapon icon " + weapon.Id);
                    Require(PixelArtAtlas.GetHeldWeaponSprite(weapon) != null, "held weapon " + weapon.Id);
                }
                foreach (var accessory in AccessoryLootTable.All)
                    Require(InventoryIconFactory.Get(accessory.Id, accessory.Color, InventoryIconKind.Accessory) != null, "accessory icon " + accessory.Id);

                UnityEngine.Random.state = before;
                var expected = UnityEngine.Random.value;
                UnityEngine.Random.state = before;
                RunContent.RollChest(123, 1, 2, false);
                Require(UnityEngine.Random.value == expected, "content roll must preserve global random state");
                Debug.Log("[Run Content] PASS seeds 9100..9611; " + weapons.Count + " weapons / " + accessories.Count +
                    " accessories reachable; deterministic independent rewards, six unique choices, mixed encounters, all icons and held sprites. Combat behavior is not covered by this data test.");
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError("[Run Content] FAIL " + error);
                return false;
            }
            finally { UnityEngine.Random.state = before; }
        }
        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
        }
    }
}
