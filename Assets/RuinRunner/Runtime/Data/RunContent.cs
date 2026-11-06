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

        sealed class Squad
        {
            public EnemyType[] Front, Back, Mobile, Flank, Support;
        }
        static readonly Squad[] Squads =
        {
            new Squad { Front = new[] { EnemyType.Grunt, EnemyType.BoneShield }, Back = new[] { EnemyType.Shooter, EnemyType.MossArcher, EnemyType.RuneTotem },
                Mobile = new[] { EnemyType.Leaper, EnemyType.Charger, EnemyType.VineLasher, EnemyType.PowderBeetle }, Flank = new[] { EnemyType.RuinRat, EnemyType.Sporeling }, Support = new[] { EnemyType.RuneTotem } },
            new Squad { Front = new[] { EnemyType.Drowned, EnemyType.CoralGuard, EnemyType.Sentinel }, Back = new[] { EnemyType.Tidecaller, EnemyType.EelSpitter },
                Mobile = new[] { EnemyType.TideCrab, EnemyType.JellyLamp, EnemyType.Leaper }, Flank = new[] { EnemyType.Wisp, EnemyType.Sporeling }, Support = new[] { EnemyType.TideSiren } },
            new Squad { Front = new[] { EnemyType.SealKnight, EnemyType.SealGolem, EnemyType.Sentinel }, Back = new[] { EnemyType.Hexer, EnemyType.GildedBolter, EnemyType.RuneTotem },
                Mobile = new[] { EnemyType.Charger, EnemyType.ChainWarden }, Flank = new[] { EnemyType.InkShade, EnemyType.Wisp, EnemyType.Sporeling }, Support = new[] { EnemyType.SealPriest } },
        };

        public static EnemyPlacementData[] Encounters(int seed, int region, bool boss, int stageIndex = 0)
        {
            if (boss)
                return new[] { new EnemyPlacementData(CampaignStage.Get(stageIndex).BossType, 0, 0) };
            return RunRandom.Sample(seed, 7, stageIndex * 16 + region, () =>
            {
                // Small mixed groups create different approach decisions without long filler waves.
                var groupSize = region == 0 ? 3 : 4;
                var waves = region < 2 ? 2 : 3;
                var result = new EnemyPlacementData[groupSize * waves];
                var roles = Squads[Mathf.Clamp(stageIndex, 0, Squads.Length - 1)];
                for (var i = 0; i < result.Length; i++)
                {
                    var seat = i % groupSize;
                    // A wave is a squad: someone to hold the front, someone behind them, and the rest to get around you.
                    var role = seat == 0 ? roles.Front : seat == 1 ? roles.Back : UnityEngine.Random.value < .5f ? roles.Mobile : stageIndex > 0 && waves > 2 && UnityEngine.Random.value < .5f ? roles.Support : roles.Flank;
                    var type = i == 0 ? (stageIndex == 0 ? EnemyType.Grunt : stageIndex == 1 ? EnemyType.Tidecaller : EnemyType.Sentinel) : role[UnityEngine.Random.Range(0, role.Length)];
                    result[i] = new EnemyPlacementData(type, 0, 0) { wave = i / groupSize };
                }
                return result;
            });
        }
    }
}
