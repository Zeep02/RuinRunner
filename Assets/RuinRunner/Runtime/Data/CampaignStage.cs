using System;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Three authored stages. Index is a run-local value, not a saved enum.</summary>
    public sealed class CampaignStage
    {
        public const int Count = 3;
        public readonly int Index;
        public readonly string Name;
        public readonly string Resource;
        public readonly string BossName;
        public readonly EnemyType BossType;
        public readonly Color FloorTint;
        public bool IsFinal => Index == Count - 1;
        public const float ClearHealingFraction = .30f;
        private CampaignStage(int index, string name, string resource, string bossName, EnemyType boss, Color floor)
        { Index = index; Name = name; Resource = resource; BossName = bossName; BossType = boss; FloorTint = floor; }
        private static readonly CampaignStage[] Stages =
        {
            new CampaignStage(0, "외곽 유적", "outer_ruins", "유적의 파수꾼", EnemyType.Boss, new Color(.95f, .88f, .68f)),
            new CampaignStage(1, "침수된 회랑", "flooded_halls", "물길의 수문장", EnemyType.Tidekeeper, new Color(.70f, .80f, .68f)),
            new CampaignStage(2, "봉인의 심부", "seal_core", "봉인의 심장", EnemyType.SealHeart, new Color(.64f, .59f, .64f))
        };
        public static CampaignStage Get(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return Stages[index];
        }
    }
}
