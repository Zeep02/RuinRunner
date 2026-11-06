using System;
using System.Collections.Generic;

namespace RuinRunner
{
    [Serializable]
    public sealed class SaveSlotData
    {
        public int version = 1;
        public int slotNumber;
        public string createdUtc = string.Empty;
        public string lastPlayedUtc = string.Empty;
        public int totalRuns;
        public int victories;
        // Additive fields: historical one-stage victories stay historical.
        public int campaignVictories;
        public int bestCampaignStage;
        public int deepestRoom;
        public int totalKills;
        public int legacyShards = 3;
        public List<SaveUpgradeRankData> upgrades = new List<SaveUpgradeRankData>();
        // Server-confirmed projection only; these fields never turn this file into a second authority.
        public string serverProfileId = string.Empty;
        public long serverRevision;
        public int legacyMigrationVersion;
        public int legacyRefundVersion;
        public string startingWeaponId = "trainee_blade";
        public string[] unlockedWeaponIds = Array.Empty<string>();
        public string[] unlockedTalentIds = Array.Empty<string>();
        public string[] activeTalentIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class SaveUpgradeRankData
    {
        public MetaUpgradeType type;
        public int rank;
    }

    public sealed class SaveSlotSummary
    {
        public int SlotNumber { get; set; }
        public bool Exists { get; set; }
        public string LastPlayed { get; set; } = "—";
        public int TotalRuns { get; set; }
        public int Victories { get; set; }
        public int CampaignVictories { get; set; }
        public int BestCampaignStage { get; set; }
        public int DeepestRoom { get; set; }
        public int TotalKills { get; set; }
        public int LegacyShards { get; set; }
        public string ProfileId { get; set; }
        public string Name { get; set; }
        public bool IsServerProfile { get; set; }
    }
}
