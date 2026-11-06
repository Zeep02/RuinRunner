using UnityEngine;

namespace RuinRunner
{
    public enum WeaponRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum WeaponSpecialType
    {
        ArcWave,
        Nova,
        PiercingBolt
    }

    public enum WeaponAttackType
    {
        Melee,
        Ranged
    }

    public enum WeaponFamily
    {
        Dagger,
        Longsword,
        Mace,
        Katana,
        Greatsword,
        Bow,
        Crossbow,
        Pistol,
        Rifle,
        Shotgun,
        Cannon,
        Spear,
        Staff,
        Orb
    }

    public static class WeaponFamilyCatalog
    {
        public static string GetName(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Dagger: return "단검";
                case WeaponFamily.Longsword: return "롱소드";
                case WeaponFamily.Mace: return "철퇴";
                case WeaponFamily.Katana: return "도";
                case WeaponFamily.Greatsword: return "대검";
                case WeaponFamily.Bow: return "활";
                case WeaponFamily.Crossbow: return "석궁";
                case WeaponFamily.Pistol: return "권총";
                case WeaponFamily.Rifle: return "소총";
                case WeaponFamily.Shotgun: return "산탄총";
                case WeaponFamily.Spear: return "창";
                case WeaponFamily.Staff: return "지팡이";
                case WeaponFamily.Orb: return "오브";
                default: return "대포";
            }
        }
    }

    public sealed class WeaponDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public WeaponRarity Rarity { get; }
        public float AttackPower { get; private set; }
        public float AttackDelayMultiplier { get; private set; }
        public Color Color { get; }
        public WeaponAttackType AttackType { get; }
        public int MagazineSize { get; private set; }
        public float ReloadSeconds { get; private set; }
        public float ProjectileSpeed { get; private set; }
        public Vector2 ProjectileSize { get; }
        public int ProjectilePierce { get; }
        public ItemSetType SetType { get; }
        public WeaponSpecialType SpecialType { get; }
        public float SpecialManaCost => WeaponActionCatalog.Get(this).Mana;
        public string SpecialDescription => WeaponActionCatalog.Get(this).Description;
        internal float CatalogSpecialMana=-1,CatalogSpecialCooldown=-1;
        public WeaponFamily Family { get; }
        public string ComboProfileId { get; }
        public float HeavyDamageMultiplier { get; private set; }
        public float GuardDamageReduction { get; private set; }

        public float BaseAttackPower { get; }
        public float BaseAttackDelayMultiplier { get; }
        public int BaseMagazineSize { get; }
        public float BaseReloadSeconds { get; }
        public float BaseProjectileSpeed { get; }
        public float BaseHeavyDamageMultiplier { get; }
        public float BaseGuardDamageReduction { get; }

        public float PowerScore => AttackPower / Mathf.Max(.25f, AttackDelayMultiplier);

        public WeaponDefinition(
            string id,
            string name,
            WeaponRarity rarity,
            float attackPower,
            float attackDelayMultiplier,
            Color color,
            WeaponAttackType attackType,
            int magazineSize,
            float reloadSeconds,
            float projectileSpeed,
            Vector2 projectileSize,
            int projectilePierce,
            WeaponSpecialType specialType,
            float specialManaCost,
            string specialDescription,
            ItemSetType setType = ItemSetType.None,
            WeaponFamily family = WeaponFamily.Longsword,
            string comboProfileId = "",
            float heavyDamageMultiplier = 1f,
            float guardDamageReduction = .4f)
        {
            Id = id;
            Name = name;
            Rarity = rarity;
            AttackPower = Mathf.Max(.1f, attackPower);
            AttackDelayMultiplier = attackDelayMultiplier;
            Color = color;
            AttackType = attackType;
            MagazineSize = attackType == WeaponAttackType.Ranged ? Mathf.Max(1, magazineSize) : 0;
            ReloadSeconds = attackType == WeaponAttackType.Ranged ? Mathf.Max(.2f, reloadSeconds) : 0f;
            ProjectileSpeed = Mathf.Max(1f, projectileSpeed);
            ProjectileSize = projectileSize;
            ProjectilePierce = Mathf.Max(1, projectilePierce);
            SetType = setType;
            SpecialType = specialType;
            Family = family;
            ComboProfileId = comboProfileId ?? string.Empty;
            HeavyDamageMultiplier = Mathf.Max(1f, heavyDamageMultiplier);
            GuardDamageReduction = attackType == WeaponAttackType.Melee ? Mathf.Clamp(guardDamageReduction, 0f, .8f) : 0f;
            BaseAttackPower = AttackPower;
            BaseAttackDelayMultiplier = AttackDelayMultiplier;
            BaseMagazineSize = MagazineSize;
            BaseReloadSeconds = ReloadSeconds;
            BaseProjectileSpeed = ProjectileSpeed;
            BaseHeavyDamageMultiplier = HeavyDamageMultiplier;
            BaseGuardDamageReduction = GuardDamageReduction;
        }

        public void ApplyBalance(WeaponBalanceData balance)
        {
            AttackPower = balance == null ? BaseAttackPower : Mathf.Max(.1f, balance.attackPower);
            AttackDelayMultiplier = balance == null ? BaseAttackDelayMultiplier : Mathf.Max(.1f, balance.attackDelayMultiplier);
            HeavyDamageMultiplier = balance == null ? BaseHeavyDamageMultiplier : Mathf.Max(1f, balance.heavyDamageMultiplier);
            GuardDamageReduction = AttackType == WeaponAttackType.Melee
                ? balance == null ? BaseGuardDamageReduction : Mathf.Clamp(balance.guardDamageReduction, 0f, .8f)
                : 0f;
            MagazineSize = AttackType == WeaponAttackType.Ranged
                ? balance == null ? BaseMagazineSize : Mathf.Max(1, balance.magazineSize)
                : 0;
            ReloadSeconds = AttackType == WeaponAttackType.Ranged
                ? balance == null ? BaseReloadSeconds : Mathf.Max(.1f, balance.reloadSeconds)
                : 0f;
            ProjectileSpeed = balance == null ? BaseProjectileSpeed : Mathf.Max(1f, balance.projectileSpeed);
        }
    }
}
