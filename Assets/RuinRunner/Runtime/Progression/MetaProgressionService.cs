using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed class MetaProgressionService
    {
        private readonly Dictionary<MetaUpgradeType, int> ranks = new Dictionary<MetaUpgradeType, int>();
        private readonly SaveSlotService saveSlots;

        public int LegacyShards { get; private set; }

        public MetaProgressionService(SaveSlotService slotService)
        {
            saveSlots = slotService;
            Load();
        }

        public int AwardRun(int enemiesDefeated, int roomsReached, bool victory, int stagesCleared = 0)
        {
            var reward = Mathf.Max(1, enemiesDefeated / 4 + roomsReached + (victory ? 6 : 0));
            LegacyShards += reward;
            saveSlots.SaveRunResult(LegacyShards, ranks, enemiesDefeated, roomsReached, victory, stagesCleared);
            return reward;
        }

        private void Load()
        {
            var data = saveSlots.ActiveData;
            LegacyShards = data == null ? 3 : Mathf.Max(0, data.legacyShards);
            ranks.Clear();
            if (data == null || data.upgrades == null) return;
            foreach (var upgrade in data.upgrades)
                ranks[upgrade.type] = Mathf.Max(0, upgrade.rank);
        }
    }
}
