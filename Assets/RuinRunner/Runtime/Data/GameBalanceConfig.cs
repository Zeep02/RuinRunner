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
        public EnemyBalanceData[] enemies =
        {
            new EnemyBalanceData(EnemyType.Grunt, 48f, 6f, 0f, 3.4f, 9),
            new EnemyBalanceData(EnemyType.Shooter, 38f, 5f, 0f, 2.5f, 11),
            new EnemyBalanceData(EnemyType.Charger, 72f, 9f, 5f, 3f, 14),
            new EnemyBalanceData(EnemyType.Leaper, 56f, 8f, 2f, 2.7f, 12),
            new EnemyBalanceData(EnemyType.Boss, 430f, 11f, 12f, 2.8f, 45),
            // Grown primary combos must leave room for both authored patterns.
            // Health changes do not scale player damage or add invulnerability.
            new EnemyBalanceData(EnemyType.Tidekeeper, 1800f, 10f, 0f, 2f, 65),
            new EnemyBalanceData(EnemyType.SealHeart, 2800f, 12f, 0f, 3.2f, 90),
            new EnemyBalanceData(EnemyType.Tidecaller, 65f, 7f, 0f, 2.5f, 14),
            new EnemyBalanceData(EnemyType.Sentinel, 90f, 9f, 0f, 3.3f, 16)
        };
        public WeaponBalanceData[] weapons = new WeaponBalanceData[0];

        // Older authored configs retain all overrides; only absent campaign roles are appended.
        public bool EnsureCampaignEnemies()
        {
            var changed=false;
            if(enemies==null)enemies=Array.Empty<EnemyBalanceData>();
            for(var id=(int)EnemyType.Tidekeeper;id<=(int)EnemyType.Sentinel;id++)
            {
                var type=(EnemyType)id;
                if(GetEnemy(type)!=null)continue;
                EnemyBalanceData missing;
                switch(type)
                {
                    case EnemyType.Tidekeeper: missing=new EnemyBalanceData(type,1800,10,0,2,65);break;
                    case EnemyType.SealHeart: missing=new EnemyBalanceData(type,2800,12,0,3.2f,90);break;
                    case EnemyType.Tidecaller: missing=new EnemyBalanceData(type,65,7,0,2.5f,14);break;
                    default: missing=new EnemyBalanceData(type,90,9,0,3.3f,16);break;
                }
                Array.Resize(ref enemies,enemies.Length+1);enemies[enemies.Length-1]=missing;changed=true;
            }
            return changed;
        }

        // Preserve older authored weapon overrides once, not on every equipment switch.
        public bool MigrateWeaponDamage()
        {
            if (weaponDamageVersion >= 1) return false;
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
                    if (entry != null && entry.type == type) return entry;
            return null;
        }

        public WeaponBalanceData GetWeapon(string id)
        {
            if (weapons != null)
                foreach (var entry in weapons)
                    if (entry != null && entry.id == id) return entry;
            return null;
        }
    }
}
