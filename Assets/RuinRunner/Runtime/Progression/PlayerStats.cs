using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Rebuildable contributions. Only an explicit acquisition/trait may restore resources.</summary>
    public sealed class PlayerStats : IDisposable
    {
        public float MaxHealth { get; private set; } = 40;
        public float Health { get; private set; } = 40;
        public float MaxMana { get; private set; } = 80;
        public float Mana { get; private set; } = 80;
        public float WeaponDamageMultiplier { get; private set; } = 1;
        public float CriticalChance { get; private set; } = .08f;
        public float CriticalDamage { get; private set; } = 1.5f;
        public float Armor { get; private set; }
        public float Luck { get; private set; } = 2;
        public float MoveSpeed { get; private set; } = 7;
        public float ManaRegeneration { get; private set; } = 5;
        public float SpecialDamageMultiplier { get; private set; } = 1;
        public float LifeOnKill { get; private set; }
        public float ManaOnKill { get; private set; } = 2;
        float baseHealth = 40, baseMana = 80, baseArmor, baseSpeed = 7, baseCritical = .08f;
        float baseCriticalDamage = 1.5f, baseLuck = 2, baseManaRegen = 5, criticalProgress;
        InventorySystem inventory;
        readonly Dictionary<UpgradeType, int> traits = new Dictionary<UpgradeType, int>();
        readonly Dictionary<AccessoryType, int> legacyAccessories = new Dictionary<AccessoryType, int>();
        readonly HashSet<string> legacySets = new HashSet<string>();
        readonly SetBonusDefinition[] setDefinitions = ItemSetCatalog.Capture();

        public PlayerStats()
        {
            ApplyBalance(GameBalance.Current.player);
            Health = MaxHealth;
            Mana = MaxMana;
        }
        public void BindInventory(InventorySystem source)
        {
            if (inventory != null)
                inventory.Changed -= InventoryChanged;
            inventory = source;
            if (inventory != null)
                inventory.Changed += InventoryChanged;
            Recalculate();
        }
        public void Dispose()
        {
            if (inventory != null)
                inventory.Changed -= InventoryChanged;
            inventory = null;
        }
        void InventoryChanged(InventoryChangeKind kind)
        {
            float previousHealth = MaxHealth, previousMana = MaxMana;
            Recalculate();
            // Preserve the old one-time pickup benefit. Moving, refreshing, upgrading and
            // dismantling never heal; a removed max merely clamps the current resource.
            if (kind == InventoryChangeKind.Acquired)
            {
                Heal(Mathf.Max(0, MaxHealth - previousHealth));
                RestoreMana(Mathf.Max(0, MaxMana - previousMana));
            }
        }
        public void ApplyBalance(PlayerBalanceData balance)
        {
            if (balance == null)
                return;
            baseHealth = balance.maxHealth;
            baseMana = balance.maxMana;
            baseArmor = balance.armor;
            baseSpeed = balance.moveSpeed;
            baseCritical = balance.criticalChance;
            baseCriticalDamage = balance.criticalDamage;
            baseLuck = balance.luck;
            baseManaRegen = balance.manaRegeneration;
            Recalculate();
        }
        public float TalentHealthScale { get; set; } = 1;
        public float TalentHealScale { get; set; } = 1;
        public float TalentManaScale { get; set; } = 1;
        public float TalentRegenScale { get; set; } = 1;
        // The permanent tree, added before anything that scales.
        public float MasteryHealth { get; set; }
        public float MasteryMana { get; set; }
        public float MasteryDamage { get; set; }
        public float MasterySkill { get; set; }
        public float MasteryCritical { get; set; }
        public float MasteryCriticalDamage { get; set; }
        public float MasterySpeed { get; set; }
        public void Recalculate()
        {
            MaxHealth = baseHealth;
            MaxMana = baseMana;
            Armor = baseArmor;
            MoveSpeed = baseSpeed;
            CriticalChance = baseCritical;
            CriticalDamage = baseCriticalDamage;
            Luck = baseLuck;
            ManaRegeneration = baseManaRegen;
            WeaponDamageMultiplier = 1;
            SpecialDamageMultiplier = 1;
            LifeOnKill = 0;
            ManaOnKill = 2;
            MaxHealth += MasteryHealth;
            MaxMana += MasteryMana;
            MoveSpeed += MasterySpeed;
            CriticalChance += MasteryCritical;
            CriticalDamage += MasteryCriticalDamage;
            WeaponDamageMultiplier += MasteryDamage;
            SpecialDamageMultiplier += MasterySkill;
            foreach (var pair in traits)
                for (int i = 0; i < pair.Value; i++)
                    ApplyTraitValues(pair.Key);
            if (inventory != null)
            {
                foreach (var item in inventory.OwnedAccessories)
                    ApplyTuningValues(item.Definition.Tuning, item.Strength);
                foreach (var bonus in setDefinitions)
                    if (inventory.GetSetPieceCount(bonus.SetType) >= bonus.RequiredPieces)
                        ApplySetValues(bonus.SetType, bonus.RequiredPieces);
            }
            else
            {
                foreach (var pair in legacyAccessories)
                    for (int i = 0; i < pair.Value; i++)
                        ApplyTuningValues(AccessoryTuning.Legacy(AccessoryLootTable.Find(pair.Key).Id), 1);
                foreach (var bonus in setDefinitions)
                    if (legacySets.Contains(bonus.SetType + ":" + bonus.RequiredPieces))
                        ApplySetValues(bonus.SetType, bonus.RequiredPieces);
            }
            MaxHealth = Mathf.Max(1, MaxHealth * TalentHealthScale);
            MaxMana = Mathf.Max(0, MaxMana * TalentManaScale);
            ManaRegeneration *= TalentRegenScale;
            Health = Mathf.Clamp(Health, 0, MaxHealth);
            Mana = Mathf.Clamp(Mana, 0, MaxMana);
        }
        public void Tick(float deltaTime, bool outOfCombat = false) => RestoreMana(ManaRegeneration * (outOfCombat ? 4 : 1) * deltaTime);
        public float ReceiveDamage(float rawDamage)
        {
            float damage = Mathf.Max(1, rawDamage * 100 / (100 + Mathf.Max(0, Armor)));
            Health = Mathf.Max(0, Health - damage);
            return damage;
        }
        public bool TrySpendMana(float amount)
        {
            if (amount < 0 || Mana < amount)
                return false;
            Mana -= amount;
            return true;
        }
        public bool RollCritical()
        {
            criticalProgress += Mathf.Clamp(CriticalChance, 0, .85f);
            if (criticalProgress < 1)
                return false;
            criticalProgress--;
            return true;
        }
        public void RestoreAfterKill()
        {
            Heal(LifeOnKill);
            RestoreMana(ManaOnKill);
        }
        public void Heal(float amount) => Health = Mathf.Min(MaxHealth, Health + Mathf.Max(0, amount) * TalentHealScale);
        public void PayHealth(float amount) => Health = Mathf.Max(1, Health - Mathf.Max(0, amount));
        public void RestoreMana(float amount) => Mana = Mathf.Min(MaxMana, Mana + Mathf.Max(0, amount));
        public void ApplyTrait(UpgradeType type)
        {
            traits.TryGetValue(type, out int count);
            traits[type] = count + 1;
            Recalculate();
            if (type == UpgradeType.VitalHeart)
                Heal(35);
            if (type == UpgradeType.ManaCore)
                RestoreMana(30);
        }
        void ApplyTraitValues(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.PowerTraining:
                    WeaponDamageMultiplier *= 1.18f;
                    break;
                case UpgradeType.VitalHeart:
                    MaxHealth += 25;
                    break;
                case UpgradeType.Haste:
                    MoveSpeed += .75f;
                    break;
                case UpgradeType.ManaCore:
                    MaxMana += 24;
                    break;
                case UpgradeType.ManaFlow:
                    ManaRegeneration += 2.2f;
                    break;
                case UpgradeType.CriticalEye:
                    CriticalChance += .08f;
                    break;
                case UpgradeType.Executioner:
                    CriticalDamage += .35f;
                    break;
                case UpgradeType.IronSkin:
                    Armor += 8;
                    break;
                case UpgradeType.LuckyCharm:
                    Luck += 4;
                    break;
                case UpgradeType.ArcaneMastery:
                    SpecialDamageMultiplier *= 1.25f;
                    break;
                case UpgradeType.BloodHarvest:
                    LifeOnKill += 2.5f;
                    break;
                case UpgradeType.SoulBattery:
                    ManaOnKill += 4;
                    break;
            }
        }
        // Definition-only methods remain for standalone legacy validations. A bound player
        // always reads the owned inventory; pickup callbacks cannot apply the same item twice.
        public void ApplyItem(PassiveItemType type) => ApplyAccessory((AccessoryType)(8 + (int)type));
        public void ApplyAccessory(AccessoryType type)
        {
            if (inventory != null)
            {
                Recalculate();
                return;
            }
            float health = MaxHealth, mana = MaxMana;
            legacyAccessories.TryGetValue(type, out int count);
            legacyAccessories[type] = count + 1;
            Recalculate();
            Heal(Mathf.Max(0, MaxHealth - health));
            RestoreMana(Mathf.Max(0, MaxMana - mana));
        }
        void ApplyTuningValues(AccessoryTuning tuning, float strength)
        {
            MaxHealth += tuning.Number("maxHealth") * strength;
            MaxMana += tuning.Number("maxMana") * strength;
            Armor += tuning.Number("armor") * strength;
            MoveSpeed += tuning.Number("moveSpeed") * strength;
            CriticalChance += tuning.Number("criticalChance") * strength;
            CriticalDamage += tuning.Number("criticalDamage") * strength;
            Luck += tuning.Number("luck") * strength;
            ManaRegeneration += tuning.Number("manaRegeneration") * strength;
            LifeOnKill += tuning.Number("lifeOnKill") * strength;
            ManaOnKill += tuning.Number("manaOnKill") * strength;
            WeaponDamageMultiplier *= 1 + tuning.Number("weaponDamageBonus") * strength;
            SpecialDamageMultiplier *= 1 + tuning.Number("specialDamageBonus") * strength;
        }
        public void ApplySetBonus(ItemSetType setType, int requiredPieces)
        {
            if (inventory != null)
            {
                Recalculate();
                return;
            }
            float health = MaxHealth, mana = MaxMana;
            legacySets.Add(setType + ":" + requiredPieces);
            Recalculate();
            Heal(Mathf.Max(0, MaxHealth - health));
            RestoreMana(Mathf.Max(0, MaxMana - mana));
        }
        void ApplySetValues(ItemSetType type, int count)
        {
            foreach (var definition in setDefinitions)
                if (definition.SetType == type && definition.RequiredPieces == count)
                {
                    ApplyTuningValues(definition.Tuning, 1);
                    return;
                }
        }
    }
}
