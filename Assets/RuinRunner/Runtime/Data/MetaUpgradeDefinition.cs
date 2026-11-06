namespace RuinRunner
{
    public enum MetaUpgradeType
    {
        VitalLegacy,
        ArcaneWellspring,
        WarriorInstinct,
        LuckyBloodline,
        NimbleFootwork,
        ReinforcedSoul,
        InventoryExpansion = 6
    }

    public sealed class MetaUpgradeDefinition
    {
        public MetaUpgradeType Type { get; }
        public string Name { get; }
        public string DescriptionPerRank { get; }
        public int BaseCost { get; }
        public int MaxRank { get; }

        public MetaUpgradeDefinition(MetaUpgradeType type, string name, string descriptionPerRank, int baseCost, int maxRank)
        {
            Type = type;
            Name = name;
            DescriptionPerRank = descriptionPerRank;
            BaseCost = baseCost;
            MaxRank = maxRank;
        }

    }

    public static class MetaUpgradeCatalog
    {
        public static readonly MetaUpgradeDefinition[] Definitions =
        {
            new MetaUpgradeDefinition(MetaUpgradeType.VitalLegacy, "생명의 유산", "원정 시작 시 최대 체력 +10", 3, 5),
            new MetaUpgradeDefinition(MetaUpgradeType.ArcaneWellspring, "마력의 샘", "원정 시작 시 최대 마나 +10", 3, 5),
            new MetaUpgradeDefinition(MetaUpgradeType.WarriorInstinct, "전사의 기억", "무기 피해량 +4%", 4, 5),
            new MetaUpgradeDefinition(MetaUpgradeType.LuckyBloodline, "행운의 혈통", "원정 시작 시 운 +1", 4, 5),
            new MetaUpgradeDefinition(MetaUpgradeType.NimbleFootwork, "숙련된 발놀림", "원정 시작 시 이동 속도 +0.2", 4, 5),
            new MetaUpgradeDefinition(MetaUpgradeType.ReinforcedSoul, "강인한 영혼", "원정 시작 시 방어력 +2", 4, 5),
            new MetaUpgradeDefinition(MetaUpgradeType.InventoryExpansion, "여행자의 가방", "가방 +4칸 (최대 32칸)", 4, 3)
        };
    }
}
