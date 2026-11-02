using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum UpgradeType
    {
        PowerTraining,
        VitalHeart,
        Haste,
        ManaCore,
        ManaFlow,
        CriticalEye,
        Executioner,
        IronSkin,
        LuckyCharm,
        ArcaneMastery,
        BloodHarvest,
        SoulBattery
    }

    public sealed class UpgradeDefinition
    {
        public UpgradeType Type { get; }
        public string Name { get; }
        public string Description { get; }

        public UpgradeDefinition(UpgradeType type, string name, string description)
        {
            Type = type;
            Name = name;
            Description = description;
        }
    }

    public static class UpgradeCatalog
    {
        private static readonly UpgradeDefinition[] Definitions =
        {
            new UpgradeDefinition(UpgradeType.PowerTraining, "힘의 단련", "공격력 +18%"),
            new UpgradeDefinition(UpgradeType.VitalHeart, "생명의 심장", "최대 체력 +25\n체력 35 회복"),
            new UpgradeDefinition(UpgradeType.Haste, "질주의 본능", "이동 속도 +0.75"),
            new UpgradeDefinition(UpgradeType.ManaCore, "마나 코어", "최대 마나 +24\n마나 30 회복"),
            new UpgradeDefinition(UpgradeType.ManaFlow, "마력 순환", "초당 마나 회복 +2.2"),
            new UpgradeDefinition(UpgradeType.CriticalEye, "예리한 눈", "치명타 확률 +8%"),
            new UpgradeDefinition(UpgradeType.Executioner, "처형자의 맹세", "치명타 피해 +35%"),
            new UpgradeDefinition(UpgradeType.IronSkin, "강철 피부", "방어력 +8"),
            new UpgradeDefinition(UpgradeType.LuckyCharm, "행운의 부적", "운 +4\n고등급·추가 보상 확률 증가"),
            new UpgradeDefinition(UpgradeType.ArcaneMastery, "비전 숙련", "특수공격 피해 +25%"),
            new UpgradeDefinition(UpgradeType.BloodHarvest, "피의 수확", "처치 시 체력 2.5 회복"),
            new UpgradeDefinition(UpgradeType.SoulBattery, "영혼 전지", "처치 시 마나 회복 +4")
        };

        public static UpgradeDefinition[] CreateRandomChoices(int count)
        {
            var pool = new List<UpgradeDefinition>(Definitions);
            var choices = new List<UpgradeDefinition>(Mathf.Min(count, pool.Count));
            while (choices.Count < count && pool.Count > 0)
            {
                var index = Random.Range(0, pool.Count);
                choices.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return choices.ToArray();
        }
    }
}
