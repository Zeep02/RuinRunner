using System.Threading;
using UnityEngine;

namespace RuinRunner
{
    public abstract class OwnedEquipment
    {
        static long nextIdentity;
        public long Identity { get; } = Interlocked.Increment(ref nextIdentity);
        public int UpgradeLevel { get; internal set; }
        public string ServerInstanceId { get; internal set; } = "";
        public bool IsStartingEquipment { get; internal set; }
        public abstract InventoryEntryKind Kind { get; }
        public abstract WeaponRarity Rarity { get; }
        public abstract string Name { get; }
        public string DisplayName => Name;
        public int UpgradeCost => 4 + (int)Rarity + UpgradeLevel * 3;
        public int DisassemblyYield => 2 + (int)Rarity + UpgradeLevel * 2;
    }

    /// <summary>One owned copy. Definition, ammo and reload travel together; slots never clone it.</summary>
    public sealed class OwnedWeapon : OwnedEquipment
    {
        public WeaponDefinition Definition { get; }
        public override InventoryEntryKind Kind => InventoryEntryKind.Weapon;
        public override WeaponRarity Rarity => Definition.Rarity;
        public override string Name => Definition.Name;
        public int Ammo { get; private set; }
        public float ReloadRemaining { get; private set; }
        // Presentation identity follows this owned copy; it never changes ammo timing.
        public int ReloadSequence { get; private set; }
        public bool ReloadStartedFromEmpty { get; private set; }
        public EquipmentMutation Mutation { get; internal set; }
        public EquipmentOath Oath { get; internal set; }
        public int OathProgress { get; internal set; }
        public bool Awakened { get; internal set; }
        bool chamberedDuringReload;
        public WeaponDefinition Weapon => Definition;
        public bool IsReloading => Weapon.AttackType == WeaponAttackType.Ranged && ReloadRemaining > 0;
        public OwnedWeapon(WeaponDefinition definition, int? ammo = null, float reload = 0)
        {
            Definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
            Ammo = definition.AttackType == WeaponAttackType.Ranged ? Mathf.Clamp(ammo ?? definition.MagazineSize, 0, definition.MagazineSize) : 0;
            ReloadRemaining = definition.AttackType == WeaponAttackType.Ranged ? Mathf.Clamp(reload, 0, definition.ReloadSeconds) : 0;
            ReloadSequence = ReloadRemaining > 0 ? 1 : 0;
            ReloadStartedFromEmpty = Ammo == 0;
        }
        public bool TryConsumeAmmo()
        {
            if (Weapon.AttackType == WeaponAttackType.Melee)
                return true;
            if (IsReloading && chamberedDuringReload && Ammo > 0)
            {
                chamberedDuringReload = false;
                Ammo--;
                return true;
            }
            if (IsReloading || Ammo <= 0)
            {
                StartReload();
                return false;
            }
            Ammo--;
            if (Ammo == 0)
                StartReload();
            return true;
        }
        public bool StartReload()
        {
            if (Weapon.AttackType != WeaponAttackType.Ranged || IsReloading || Ammo >= Weapon.MagazineSize)
                return false;
            ReloadStartedFromEmpty = Ammo == 0;
            chamberedDuringReload = false;
            ReloadSequence++;
            ReloadRemaining = Weapon.ReloadSeconds;
            return true;
        }
        public void Tick(float delta)
        {
            if (!IsReloading)
                return;
            ReloadRemaining = Mathf.Max(0, ReloadRemaining - Mathf.Max(0, delta));
            if (ReloadRemaining <= 0)
            {
                Ammo = Weapon.MagazineSize;
                chamberedDuringReload = false;
            }
        }
        public void RefreshBalance()
        {
            if (Weapon.AttackType != WeaponAttackType.Ranged)
                return;
            Ammo = Mathf.Clamp(Ammo, 0, Weapon.MagazineSize);
            ReloadRemaining = Mathf.Clamp(ReloadRemaining, 0, Weapon.ReloadSeconds);
        }
    }

    public sealed class OwnedAccessory : OwnedEquipment
    {
        public AccessoryDefinition Definition { get; }
        public override InventoryEntryKind Kind => InventoryEntryKind.Accessory;
        public override WeaponRarity Rarity => Definition.Rarity;
        public override string Name => Definition.Name;
        public float Strength => 1;
        public OwnedAccessory(AccessoryDefinition definition)
        {
            Definition = definition ?? throw new System.ArgumentNullException(nameof(definition));
        }
    }
}
