using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using RuinRunner;

public static class OwnedEquipmentCore20261001
{
    static readonly List<string> checks = new List<string>();
    static void Check(bool okay, string label) { if (!okay) throw new Exception(label); checks.Add("PASS " + label); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    public static string Main()
    {
        string folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-equipment-upgrades/core-" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder); checks.Clear();
        try { Run(); File.WriteAllText(folder + "/result.txt", "PASS owned-state/stat/material checks; not UI, normal campaign or build proof\n" + string.Join("\n", checks)); }
        catch (Exception error) { File.WriteAllText(folder + "/result.txt", "FAIL " + error + "\n" + string.Join("\n", checks)); throw; }
        return folder;
    }
    static void Run()
    {
        var definition = WeaponLootTable.Find("scrap_pistol"); float basePower = definition.AttackPower;
        var bag = new InventorySystem(); for (int i = 0; i < 4; i++) bag.AddWeapon(definition);
        var first = bag.QuickSlots[0].Item; var second = bag.QuickSlots[1].Item; var stored = bag.OwnedWeapons[0];
        Check(first.Identity != second.Identity && second.Identity != stored.Identity, "same definition has distinct owned identities");
        Check(bag.AwardMaterials(100, "room0") && !bag.AwardMaterials(100, "room0") && bag.UpgradeMaterials == 100, "material reward token is paid once");
        bag.TryConsumeAmmo(); bag.StartReload(); bag.Tick(.2f); int ammo = first.Ammo; float reload = first.ReloadRemaining;
        Check(bag.TryUpgrade(first.Identity, 0), "owned ranged upgrade succeeds");
        int wallet = bag.UpgradeMaterials;
        Check(!bag.TryUpgrade(first.Identity, 0) && bag.UpgradeMaterials == wallet, "repeated stale upgrade cannot pay twice");
        Check(first.UpgradeLevel == 1 && second.UpgradeLevel == 0 && stored.UpgradeLevel == 0 && Near(definition.AttackPower, basePower), "upgrade changes only selected copy, not duplicate or drop definition");
        Check(first.Ammo == ammo && Near(first.ReloadRemaining, Mathf.Min(reload, first.Weapon.ReloadSeconds)), "upgrade does not refill ammo or restart reload");
        bag.AssignToQuickSlot(0, 0);
        Check(bag.OwnedWeapons[0] == first && bag.QuickSlots[0].Item == stored, "bag/quick swap transfers the same owned instances");
        bag.MoveBagItem(0, 10);
        Check(bag.GetOwnedCell(10) == first && bag.GetCell(0) == null && first.UpgradeLevel == 1 && first.Ammo == ammo, "bag layout movement retains identity, upgrade and ammo");
        bag.UnequipQuickSlot(1, 11); bag.AssignToQuickSlot(0, 1); bag.SwapQuickSlots(1, 2);
        Check(bag.QuickSlots[2].Item == first && first.UpgradeLevel == 1 && first.Ammo == ammo, "unequip/re-equip/quick swap do not clone the copy");
        Check(bag.TryDisassemble(first.Identity, 1, out int yield) && yield == 4 && bag.FindOwned(first.Identity) == null, "selected upgraded copy is removed and refunds stated material yield");
        wallet = bag.UpgradeMaterials;
        Check(!bag.TryDisassemble(first.Identity, 1, out _) && bag.UpgradeMaterials == wallet, "same owned identity cannot be dismantled twice");
        while (bag.CanDisassemble(bag.ActiveSlot.Item.Identity)) bag.TryDisassemble(bag.ActiveSlot.Item.Identity, bag.ActiveSlot.Item.UpgradeLevel, out _);
        Check(!bag.TryDisassemble(bag.ActiveSlot.Item.Identity, 0, out _) && bag.EquippedWeapon != null, "final owned weapon cannot leave an unarmed soft lock");

        bag = new InventorySystem(); bag.AddWeapon(WeaponLootTable.Find("rust_sword"));
        using (var stats = new PlayerStats())
        {
            stats.BindInventory(bag); stats.ReceiveDamage(10); stats.TrySpendMana(10);
            float hp = stats.Health, mp = stats.Mana, max = stats.MaxHealth;
            bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.RubyHeart)); var ruby = bag.OwnedAccessories[0];
            Check(Near(stats.MaxHealth, max + 18) && Near(stats.Health, hp + 18), "max-HP acquisition restores its stated increase once");
            hp = stats.Health;
            for (int i = 0; i < 20; i++) { stats.Recalculate(); bag.RefreshWeaponBalance(); stats.ApplyAccessory(AccessoryType.RubyHeart); }
            Check(Near(stats.Health, hp) && Near(stats.MaxHealth, max + 18) && Near(stats.Mana, mp), "rebuild/compatibility pickup/refresh cannot duplicate health, mana or stats");
            bag.AwardMaterials(100, "test"); bag.TryUpgrade(ruby.Identity, 0);
            Check(Near(stats.MaxHealth, max + 21.6f) && Near(stats.Health, hp), "accessory upgrade changes max without free healing");
            bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.RubyHeart));
            Check(Near(stats.MaxHealth, max + 39.6f) && bag.GetSetPieceCount(ItemSetType.BloodMoon) == 1, "duplicate copy grows stats but not distinct set count");
            bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.CrimsonRing)); bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.BerserkerEmblem));
            Check(bag.GetSetPieceCount(ItemSetType.BloodMoon) == 3 && Near(stats.WeaponDamageMultiplier, 1.1f * 1.15f), "live three-ID set is applied once");
            var emblem = bag.OwnedAccessories[3]; bag.TryDisassemble(emblem.Identity, 0, out _);
            Check(bag.GetSetPieceCount(ItemSetType.BloodMoon) == 2 && Near(stats.WeaponDamageMultiplier, 1) && Near(stats.LifeOnKill, 1.5f), "removal immediately releases high-tier set and item bonus");
            bag.TryDisassemble(ruby.Identity, 1, out _);
            Check(Near(stats.MaxHealth, max + 18) && stats.Health <= stats.MaxHealth && bag.GetOwnedCell(1) != null, "removing one upgraded duplicate preserves other copy and repairs cell indices");
            bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.SapphireFlask)); float mana = stats.Mana;
            bag.MoveBagItem(0, 15); stats.Recalculate();
            Check(Near(stats.Mana, mana), "mana-bearing item movement never grants another pickup refund");
            stats.ApplyTrait(UpgradeType.PowerTraining); float damage = stats.WeaponDamageMultiplier;
            bag.Reset();
            Check(Near(stats.WeaponDamageMultiplier, damage) && Near(stats.MaxHealth, max) && bag.UpgradeMaterials == 0 && bag.GetSetPieceCount(ItemSetType.BloodMoon) == 0, "reset clears equipment/material/set contributions while separate trait contribution remains");
        }
        bag = new InventorySystem(); using (var stats = new PlayerStats())
        {
            stats.BindInventory(bag);
            for (int i = 0; i < InventorySystem.Capacity; i++) Check(bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.LuckyDice)), "fill real bag cell " + i);
            float luck = stats.Luck;
            Check(!bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.LuckyDice)) && Near(stats.Luck, luck), "full-bag acquisition is atomic and leaves stats unchanged");
            var victim = bag.OwnedAccessories[10]; bag.TryDisassemble(victim.Identity, 0, out _);
            Check(bag.GetCell(10) == null && bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.LuckyDice)) && Near(stats.Luck, luck), "full bag can free and refill the exact cell without leftover stats");
        }
        foreach (var weapon in WeaponLootTable.All)
        {
            bag = new InventorySystem(); bag.AddWeapon(weapon); bag.AwardMaterials(100, "all"); var item = bag.ActiveSlot.Item;
            float original = weapon.AttackPower;
            for (int i = 0; i < 3; i++) Check(bag.TryUpgrade(item.Identity, i), "owned +" + (i + 1) + " " + weapon.Id);
            Check(!bag.TryUpgrade(item.Identity, 3) && item.UpgradeLevel == 3 && Near(weapon.AttackPower, original) && Near(item.Weapon.AttackPower, original * 1.36f), "catalog identity and +3 limit " + weapon.Id);
        }
    }
}
