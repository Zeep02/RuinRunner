using System;
using UnityEngine;

namespace RuinRunner
{
    public sealed class ChestRewards
    {
        public WeaponDefinition Weapon;
        public WeaponDefinition BonusWeapon;
        public AccessoryDefinition Accessory;
    }

    /// <summary>Production content. DemoContent is retained only for old art fixtures.</summary>
    public static class RunContent
    {
        public static ChestRewards RollChest(int seed, int encounter, float luck, bool boss)
            => RunRandom.Sample(seed, 17, encounter, () => new ChestRewards
            {
                Weapon = WeaponLootTable.Roll(luck + (boss ? 4 : 0)),
                BonusWeapon = UnityEngine.Random.value < WeaponLootTable.GetDoubleDropChance(luck) ? WeaponLootTable.Roll(luck) : null,
                Accessory = boss || UnityEngine.Random.value < .55f ? AccessoryLootTable.Roll(luck) : null
            });

        public static AccessoryDefinition[] Choices(int seed, int offer, float luck)
            => RunRandom.Sample(seed, 29, offer, () => AccessoryLootTable.CreateChoices(6, luck));

        public static EnemyPlacementData[] Encounters(int seed, int region, bool boss, int stageIndex = 0)
        {
            if (boss) return new[] { new EnemyPlacementData(CampaignStage.Get(stageIndex).BossType, 0, 0) };
            return RunRandom.Sample(seed, 7, stageIndex * 16 + region, () =>
            {
                // Small mixed groups create different approach decisions without long filler waves.
                var groupSize = region == 0 ? 2 : 3;
                var waves = region < 2 ? 2 : 3;
                var result = new EnemyPlacementData[groupSize * waves];
                var pool = stageIndex == 2 ? new[] { EnemyType.Sentinel, EnemyType.Tidecaller, EnemyType.Charger, EnemyType.Shooter, EnemyType.Leaper }
                    : stageIndex == 1 ? new[] { EnemyType.Tidecaller, EnemyType.Sentinel, EnemyType.Leaper, EnemyType.Grunt }
                    : region == 0
                    ? new[] { EnemyType.Grunt, EnemyType.Shooter, EnemyType.Leaper }
                    : new[] { EnemyType.Grunt, EnemyType.Shooter, EnemyType.Charger, EnemyType.Leaper };
                for (var i = 0; i < result.Length; i++)
                {
                    var type = i == 0 ? (stageIndex == 0 ? EnemyType.Grunt : stageIndex == 1 ? EnemyType.Tidecaller : EnemyType.Sentinel) : pool[UnityEngine.Random.Range(0, pool.Length)];
                    result[i] = new EnemyPlacementData(type, 0, 0) { wave = i / groupSize };
                }
                return result;
            });
        }
    }
}
