using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace RuinRunner
{
    [Serializable]
    public sealed class PlayerBalanceData
    {
        public float maxHealth = 40f;
        public float maxMana = 80f;
        [FormerlySerializedAs("attackPower"), HideInInspector] public float legacyAttackPower = 24f;
        public float armor;
        public float moveSpeed = 7f;
        public float criticalChance = .08f;
        public float criticalDamage = 1.5f;
        public float luck = 2f;
        public float manaRegeneration = 5f;
    }

    [Serializable]
    public sealed class EnemyBalanceData
    {
        public EnemyType type;
        public float maxHealth;
        public float attackPower;
        public float armor;
        public float moveSpeed;
        public int experienceReward;
        [Range(.6f, 1.5f)] public float hurtboxScale = 1.08f;
        public Vector2 hurtboxOffset;
        [Range(0f, .5f)] public float hurtboxPadding = .08f;
        [Range(.05f, 1f)] public float healthBarGap = .2f;
        public Vector2 movementFootSize = new Vector2(.78f, .46f);
        public Vector2 movementFootOffset = new Vector2(0f, -.2f);

        public EnemyBalanceData(EnemyType enemyType, float health, float attack, float defense, float speed, int experience)
        {
            type = enemyType;
            maxHealth = health;
            attackPower = attack;
            armor = defense;
            moveSpeed = speed;
            experienceReward = experience;
        }
    }

    [Serializable]
    public sealed class WeaponBalanceData
    {
        public string id;
        public float attackPower;
        [HideInInspector] public float damageMultiplier; // v0 JSON migration only
        public float attackDelayMultiplier;
        public float heavyDamageMultiplier;
        [Range(0f, .8f)] public float guardDamageReduction;
        public int magazineSize;
        public float reloadSeconds;
        public float projectileSpeed;
    }

    [CreateAssetMenu(menuName = "Ruin Runner/Game Balance Config", fileName = "GameBalanceConfig")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        [HideInInspector] public int weaponDamageVersion;
        public PlayerBalanceData player = new PlayerBalanceData();
        // Global difficulty knobs on top of the per-type table, so one number retunes every stage.
        [Range(.5f, 3f)] public float enemyHealthMultiplier = 1.35f;
        [Range(.5f, 3f)] public float bossHealthMultiplier = 1.4f;
        [Range(.1f, 2f)] public float experienceGainMultiplier = .7f;
        public EnemyBalanceData[] enemies =
        {
            new EnemyBalanceData(EnemyType.Grunt, 77f, 6f, 0f, 3.4f, 9),
            new EnemyBalanceData(EnemyType.Shooter, 61f, 5f, 0f, 2.5f, 11),
            new EnemyBalanceData(EnemyType.Charger, 115f, 9f, 5f, 3f, 14),
            new EnemyBalanceData(EnemyType.Leaper, 90f, 8f, 2f, 2.7f, 12),
            new EnemyBalanceData(EnemyType.Boss, 1200f, 11f, 12f, 2.8f, 45),
            // Grown primary combos must leave room for both authored patterns.
            // Health changes do not scale player damage or add invulnerability.
            new EnemyBalanceData(EnemyType.Tidekeeper, 1800f, 10f, 0f, 2f, 65),
            new EnemyBalanceData(EnemyType.SealHeart, 2800f, 12f, 0f, 3.2f, 90),
            new EnemyBalanceData(EnemyType.Tidecaller, 104f, 7f, 0f, 2.5f, 14),
            new EnemyBalanceData(EnemyType.Sentinel, 144f, 9f, 0f, 3.3f, 16),
            new EnemyBalanceData(EnemyType.Sporeling, 45f, 8f, 0f, 4.3f, 7),
            new EnemyBalanceData(EnemyType.RuneTotem, 136f, 6f, 3f, .1f, 13),
            new EnemyBalanceData(EnemyType.TideCrab, 141f, 8f, 6f, 2.7f, 15),
            new EnemyBalanceData(EnemyType.Wisp, 83f, 7f, 0f, 3f, 14),
            new EnemyBalanceData(EnemyType.SealKnight, 248f, 11f, 6f, 2.3f, 22),
            new EnemyBalanceData(EnemyType.Hexer, 99f, 8f, 0f, 2.4f, 18),
            new EnemyBalanceData(EnemyType.BoneShield, 95f, 6f, 0f, 2.6f, 12),
            new EnemyBalanceData(EnemyType.VineLasher, 80f, 7f, 0f, 2.2f, 12),
            new EnemyBalanceData(EnemyType.RuinRat, 40f, 6f, 0f, 4.6f, 9),
            new EnemyBalanceData(EnemyType.MossArcher, 50f, 7f, 0f, 2.4f, 12),
            new EnemyBalanceData(EnemyType.PowderBeetle, 60f, 9f, 0f, 3.2f, 12),
            new EnemyBalanceData(EnemyType.Drowned, 150f, 8f, 0f, 2f, 14),
            new EnemyBalanceData(EnemyType.EelSpitter, 85f, 8f, 0f, 2.6f, 14),
            new EnemyBalanceData(EnemyType.CoralGuard, 190f, 9f, 6f, 2.2f, 18),
            new EnemyBalanceData(EnemyType.JellyLamp, 95f, 7f, 0f, 2f, 14),
            new EnemyBalanceData(EnemyType.TideSiren, 90f, 5f, 0f, 2.4f, 18),
            new EnemyBalanceData(EnemyType.InkShade, 110f, 10f, 0f, 4f, 18),
            new EnemyBalanceData(EnemyType.SealGolem, 320f, 13f, 6f, 1.9f, 26),
            new EnemyBalanceData(EnemyType.GildedBolter, 120f, 10f, 0f, 2.4f, 20),
            new EnemyBalanceData(EnemyType.ChainWarden, 170f, 10f, 0f, 2.2f, 22),
            new EnemyBalanceData(EnemyType.SealPriest, 135f, 7f, 0f, 2.3f, 24)
        };
        public WeaponBalanceData[] weapons = new WeaponBalanceData[0];

        // Older authored configs retain all overrides; only absent campaign roles are appended.
        public bool EnsureCampaignEnemies()
        {
            var changed = false;
            if (enemies == null)
                enemies = Array.Empty<EnemyBalanceData>();
            for (var id = (int)EnemyType.Tidekeeper; id <= (int)EnemyType.SealPriest; id++)
            {
                var type = (EnemyType)id;
                if (GetEnemy(type) != null)
                    continue;
                EnemyBalanceData missing;
                switch (type)
                {
                    case EnemyType.Tidekeeper:
                        missing = new EnemyBalanceData(type, 1800, 10, 0, 2, 65);
                        break;
                    case EnemyType.SealHeart:
                        missing = new EnemyBalanceData(type, 2800, 12, 0, 3.2f, 90);
                        break;
                    case EnemyType.Tidecaller:
                        missing = new EnemyBalanceData(type, 104, 7, 0, 2.5f, 14);
                        break;
                    case EnemyType.Sporeling:
                        missing = new EnemyBalanceData(type, 45, 8, 0, 4.3f, 7);
                        break;
                    case EnemyType.RuneTotem:
                        missing = new EnemyBalanceData(type, 136, 6, 3, .1f, 13);
                        break;
                    case EnemyType.TideCrab:
                        missing = new EnemyBalanceData(type, 141, 8, 6, 2.7f, 15);
                        break;
                    case EnemyType.Wisp:
                        missing = new EnemyBalanceData(type, 83, 7, 0, 3f, 14);
                        break;
                    case EnemyType.SealKnight:
                        missing = new EnemyBalanceData(type, 248, 11, 6, 2.3f, 22);
                        break;
                    case EnemyType.Hexer:
                        missing = new EnemyBalanceData(type, 99, 8, 0, 2.4f, 18);
                        break;
                    case EnemyType.BoneShield:
                        missing = new EnemyBalanceData(type, 95, 6, 0, 2.6f, 12);
                        break;
                    case EnemyType.VineLasher:
                        missing = new EnemyBalanceData(type, 80, 7, 0, 2.2f, 12);
                        break;
                    case EnemyType.RuinRat:
                        missing = new EnemyBalanceData(type, 40, 6, 0, 4.6f, 9);
                        break;
                    case EnemyType.MossArcher:
                        missing = new EnemyBalanceData(type, 50, 7, 0, 2.4f, 12);
                        break;
                    case EnemyType.PowderBeetle:
                        missing = new EnemyBalanceData(type, 60, 9, 0, 3.2f, 12);
                        break;
                    case EnemyType.Drowned:
                        missing = new EnemyBalanceData(type, 150, 8, 0, 2f, 14);
                        break;
                    case EnemyType.EelSpitter:
                        missing = new EnemyBalanceData(type, 85, 8, 0, 2.6f, 14);
                        break;
                    case EnemyType.CoralGuard:
                        missing = new EnemyBalanceData(type, 190, 9, 6, 2.2f, 18);
                        break;
                    case EnemyType.JellyLamp:
                        missing = new EnemyBalanceData(type, 95, 7, 0, 2f, 14);
                        break;
                    case EnemyType.TideSiren:
                        missing = new EnemyBalanceData(type, 90, 5, 0, 2.4f, 18);
                        break;
                    case EnemyType.InkShade:
                        missing = new EnemyBalanceData(type, 110, 10, 0, 4f, 18);
                        break;
                    case EnemyType.SealGolem:
                        missing = new EnemyBalanceData(type, 320, 13, 6, 1.9f, 26);
                        break;
                    case EnemyType.GildedBolter:
                        missing = new EnemyBalanceData(type, 120, 10, 0, 2.4f, 20);
                        break;
                    case EnemyType.ChainWarden:
                        missing = new EnemyBalanceData(type, 170, 10, 0, 2.2f, 22);
                        break;
                    case EnemyType.SealPriest:
                        missing = new EnemyBalanceData(type, 135, 7, 0, 2.3f, 24);
                        break;
                    default:
                        missing = new EnemyBalanceData(type, 144, 9, 0, 3.3f, 16);
                        break;
                }
                Array.Resize(ref enemies, enemies.Length + 1);
                enemies[enemies.Length - 1] = missing;
                changed = true;
            }
            return changed;
        }

        // Preserve older authored weapon overrides once, not on every equipment switch.
        public bool MigrateWeaponDamage()
        {
            if (weaponDamageVersion >= 1)
                return false;
            if (weapons != null)
                foreach (var entry in weapons)
                    if (entry != null && entry.attackPower <= 0f)
                        entry.attackPower = Mathf.Max(.1f, entry.damageMultiplier * (player == null ? 24f : player.legacyAttackPower));
            weaponDamageVersion = 1;
            return true;
        }

        public EnemyBalanceData GetEnemy(EnemyType type)
        {
            if (enemies != null)
                foreach (var entry in enemies)
                    if (entry != null && entry.type == type)
                        return entry;
            return null;
        }

        public WeaponBalanceData GetWeapon(string id)
        {
            if (weapons != null)
                foreach (var entry in weapons)
                    if (entry != null && entry.id == id)
                        return entry;
            return null;
        }
    }
}
