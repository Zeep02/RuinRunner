using UnityEngine;

namespace RuinRunner
{
    public static class GameBalance
    {
        private const string ResourcePath = "Balance/GameBalanceConfig";
        private static GameBalanceConfig current;

        public static GameBalanceConfig Current
        {
            get
            {
                if (current == null)
                {
                    current = Resources.Load<GameBalanceConfig>(ResourcePath);
                    if (current == null)
                        current = ScriptableObject.CreateInstance<GameBalanceConfig>();
                }
                current.MigrateWeaponDamage();
                current.EnsureCampaignEnemies();
                return current;
            }
        }

        // Monster numbers from the expedition's pinned catalog. A catalog without them restores the shipped table.
        public static void BindCatalog(RuinRunner.Backend.ContentCatalog catalog)
        {
            if (shipped == null)
                shipped = Current;
            var rules = catalog?.enemyBalance;
            if (rules == null)
            {
                current = shipped;
                return;
            }
            var config = Object.Instantiate(shipped);
            config.enemyHealthMultiplier = Mathf.Clamp(rules.enemyHealthMultiplier, .1f, 10f);
            config.bossHealthMultiplier = Mathf.Clamp(rules.bossHealthMultiplier, .1f, 10f);
            config.experienceGainMultiplier = Mathf.Clamp(rules.experienceGainMultiplier, 0f, 10f);
            config.bossBurstThreshold = Mathf.Clamp(rules.bossBurstThreshold, .005f, 1f);
            config.bossBurstSeconds = Mathf.Clamp(rules.bossBurstSeconds, .1f, 30f);
            config.bossItemDamageMultiplier = Mathf.Clamp(rules.bossItemDamageMultiplier, 0f, 2f);
            var applied = 0;
            if (rules.enemies != null)
                foreach (var row in rules.enemies)
                {
                    if (row == null || !System.Enum.TryParse(row.type, out EnemyType type))
                        continue;
                    var entry = config.GetEnemy(type);
                    if (entry == null)
                        continue;
                    entry.maxHealth = Mathf.Max(1f, row.maxHealth);
                    entry.attackPower = Mathf.Max(0f, row.attackPower);
                    entry.armor = Mathf.Max(0f, row.armor);
                    entry.moveSpeed = Mathf.Max(.05f, row.moveSpeed);
                    entry.experienceReward = Mathf.Max(0, row.experienceReward);
                    applied++;
                }
            current = config;
            Debug.Log("[Balance] catalog " + catalog.version + ": monster HP x" + config.enemyHealthMultiplier + ", boss HP x" + config.bossHealthMultiplier +
                ", XP x" + config.experienceGainMultiplier + ", " + applied + " monster rows");
        }
        private static GameBalanceConfig shipped;

        public static void UseConfig(GameBalanceConfig config)
        {
            if (config != null)
            {
                config.MigrateWeaponDamage();
                config.EnsureCampaignEnemies();
                current = config;
            }
        }

        public static void ApplyLive()
        {
            WeaponLootTable.ApplyBalance(Current);
            foreach (var player in RuntimeSceneObjects.FindAll<PlayerController>())
                player.ApplyBalance(Current.player);
            foreach (var enemy in RuntimeSceneObjects.FindAll<EnemyController>())
                enemy.ApplyBalance(Current.GetEnemy(enemy.Type));
        }
    }
}
