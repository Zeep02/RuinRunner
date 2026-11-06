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
