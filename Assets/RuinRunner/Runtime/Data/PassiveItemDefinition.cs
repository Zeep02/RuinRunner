using UnityEngine;

namespace RuinRunner
{
    public enum PassiveItemType
    {
        RubyHeart,
        SapphireFlask,
        AssassinMark,
        ExecutionersCoin,
        GuardianPlate,
        FourLeafClover,
        WingedBoots,
        ArcaneLens,
        VampireFang,
        SoulCapacitor
    }

    public sealed class PassiveItemDefinition
    {
        public PassiveItemType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public WeaponRarity Rarity { get; }
        public Color Color { get; }
        public ItemSetType SetType { get; }

        public PassiveItemDefinition(PassiveItemType type, string name, string description, WeaponRarity rarity, Color color, ItemSetType setType = ItemSetType.None)
        {
            Type = type;
            Name = name;
            Description = description;
            Rarity = rarity;
            Color = color;
            SetType = setType;
        }
    }
}
