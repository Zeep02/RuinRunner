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
}
