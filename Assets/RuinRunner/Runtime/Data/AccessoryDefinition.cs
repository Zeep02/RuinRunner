using UnityEngine;

namespace RuinRunner
{
    public enum AccessoryType
    {
        CrimsonRing,
        StormFeather,
        GuardianSeal,
        ManaPrism,
        LuckyDice,
        BerserkerEmblem,
        BulwarkBrooch,
        SageSigil,
        // Appended; preserve original accessory values and legacy passive mapping.
        RubyHeart = 8,
        SapphireFlask,
        AssassinMark,
        ExecutionersCoin,
        GuardianPlate,
        FourLeafClover,
        WingedBoots,
        ArcaneLens,
        VampireFang,
        SoulCapacitor,
        ManaThread,
        GuardBell,
        RushSpur,
        SteadyLocket,
        PowderSatchel,
        LastLightPendant,
        RecallSpool,
        CadenceClasp,
        PrismCage,
        TrailSpindle,
        FocusPrism,
        ManaAegis,
        RetributionThorn,
        MirrorShard,
        CinderSeed,
        ThunderCharm,
        PoisonCenser,
        ChainSpark,
        SoulLantern,
        VineSnare,
        ShadowTwinRing,
        QuakeBell,
        FrostWaveNecklace,
        GuardianFeather,
        BloodPact,
        SwapFlash,
        MeteorCompass,
        GravityPearl,
        TimeHourglass,
        HolyTithe,
        StormCore,
        PhoenixAsh
    }

    public sealed class AccessoryDefinition
    {
        public string Id { get; }
        public AccessoryType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public WeaponRarity Rarity { get; }
        public ItemSetType SetType { get; }
        public Color Color { get; }
        public AccessoryTuning Tuning { get; }
        public bool UsesLegacyPassiveIcon => Type >= AccessoryType.RubyHeart && Type <= AccessoryType.SoulCapacitor && !DemoContent.IsAccessory(Id);
        public string IconId => UsesLegacyPassiveIcon ? ((PassiveItemType)((int)Type - 8)).ToString() : Id;
        public InventoryIconKind IconKind => UsesLegacyPassiveIcon ? InventoryIconKind.Passive : InventoryIconKind.Accessory;

        public AccessoryDefinition(string id, AccessoryType type, string name, string description, WeaponRarity rarity, ItemSetType setType, Color color, AccessoryTuning tuning = null)
        {
            Id = id;
            Type = type;
            Name = name;
            Tuning = tuning ?? AccessoryTuning.Legacy(id);
            Description = Tuning.Format(description);
            Rarity = rarity;
            SetType = setType;
            Color = color;
        }
    }
}
