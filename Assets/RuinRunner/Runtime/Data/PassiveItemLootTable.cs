using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    // Legacy API adapter. Names, effects and rarity now have one source: AccessoryLootTable.
    public static class PassiveItemLootTable
    {
        public static PassiveItemDefinition Roll(float luck)
        {
            var rarity = WeaponLootTable.RollRarity(luck);
            var candidates = new List<AccessoryDefinition>();
            foreach (var item in AccessoryLootTable.All)
                if (item.UsesLegacyPassiveIcon && item.Rarity == rarity) candidates.Add(item);
            var chosen = candidates.Count == 0 ? AccessoryLootTable.Find(AccessoryType.RubyHeart)
                : candidates[Random.Range(0, candidates.Count)];
            return new PassiveItemDefinition((PassiveItemType)((int)chosen.Type - 8), chosen.Name,
                chosen.Description, chosen.Rarity, chosen.Color, chosen.SetType);
        }
    }
}
