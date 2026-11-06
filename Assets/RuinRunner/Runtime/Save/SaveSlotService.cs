using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RuinRunner.Backend;
using UnityEngine;

namespace RuinRunner
{
    public sealed class SaveSlotService : IProfileStore
    {
        private const int SlotCount = 3;
        private readonly string saveDirectory;
        private readonly SaveSlotSummary[] summaries = new SaveSlotSummary[SlotCount];

        public int ActiveSlot { get; private set; }
        public SaveSlotData ActiveData { get; private set; }
        public string SaveDirectory => saveDirectory;
        public bool IsValidationProfile { get; }
        public bool ServerBound { get; private set; }

        public SaveSlotService(string isolatedSaveDirectory = null)
        {
            IsValidationProfile = isolatedSaveDirectory != null || HasCommandLineArgument("--validation-profile");
            var profileName = "ManualProfile";
            var args = Environment.GetCommandLineArgs();
            var profileIndex = Array.IndexOf(args, "--validation-profile-name");
            if (IsValidationProfile && profileIndex >= 0)
            {
                if (profileIndex + 1 >= args.Length)
                    throw new ArgumentException("Validation profile name required");
                profileName = args[profileIndex + 1];
                if (profileName.Length == 0 || profileName.Length > 80)
                    throw new ArgumentException("Invalid validation profile name");
                foreach (var character in profileName)
                    if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                        throw new ArgumentException("Validation profile name may not contain path components");
            }
            saveDirectory = isolatedSaveDirectory != null ? Path.GetFullPath(isolatedSaveDirectory) : IsValidationProfile
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Logs", "Validation", profileName, "Saves"))
                : Path.Combine(Application.persistentDataPath, "RuinRunner", "Saves");
            RefreshSummaries();
        }

        public SaveSlotSummary GetSummary(int slotNumber)
        {
            return IsValidSlot(slotNumber) ? summaries[slotNumber - 1] : new SaveSlotSummary { SlotNumber = slotNumber };
        }

        public bool OpenSlot(int slotNumber)
        {
            if (ServerBound)
                return false; // Normal profiles must await the authenticated API selection.
            if (!IsValidSlot(slotNumber))
                return false;

            var data = LoadSlot(slotNumber);
            var isNew = data == null;
            if (isNew)
            {
                data = CreateNewData(slotNumber);
                if (slotNumber == 1 && !IsValidationProfile)
                    TryImportLegacyProgression(data);
            }

            ActiveSlot = slotNumber;
            ActiveData = data;
            ActiveData.slotNumber = slotNumber;
            ActiveData.lastPlayedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            SaveActive();
            return true;
        }

        public void OpenTransientForTests()
        {
            ServerBound = false;
            ActiveSlot = 0;
            ActiveData = CreateNewData(0);
        }

        public bool DeleteSlot(int slotNumber)
        {
            if (ServerBound)
                return false; // Never delete the legacy source behind a server profile.
            if (!IsValidSlot(slotNumber))
                return false;
            try
            {
                DeleteIfPresent(GetPath(slotNumber));
                DeleteIfPresent(GetBackupPath(slotNumber));
                DeleteIfPresent(GetTemporaryPath(slotNumber));
                if (ActiveSlot == slotNumber)
                {
                    ActiveSlot = 0;
                    ActiveData = null;
                }
                RefreshSummaries();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Save Slots] Delete failed: " + exception.Message);
                return false;
            }
        }

        private void UpdateProgression(int legacyShards, IReadOnlyDictionary<MetaUpgradeType, int> ranks)
        {
            ActiveData.legacyShards = Mathf.Max(0, legacyShards);
            ActiveData.upgrades.Clear();
            foreach (var rank in ranks)
                ActiveData.upgrades.Add(new SaveUpgradeRankData { type = rank.Key, rank = Mathf.Max(0, rank.Value) });
        }

        public void SaveRunResult(int legacyShards, IReadOnlyDictionary<MetaUpgradeType, int> ranks,
            int enemiesDefeated, int roomsReached, bool victory, int stagesCleared = 0)
        {
            if (ServerBound)
                throw new InvalidOperationException("Run settlement must be confirmed by the backend.");
            if (ActiveData == null)
                return;
            // Progression and statistics are one logical result. Persist one
            // complete snapshot, never a reward with the previous run counters.
            UpdateProgression(legacyShards, ranks);
            ActiveData.totalRuns++;
            ActiveData.totalKills += Mathf.Max(0, enemiesDefeated);
            ActiveData.deepestRoom = Mathf.Max(ActiveData.deepestRoom, roomsReached);
            if (victory)
                ActiveData.victories++;
            if (victory && stagesCleared == CampaignStage.Count)
                ActiveData.campaignVictories++;
            ActiveData.bestCampaignStage = Mathf.Max(ActiveData.bestCampaignStage, Mathf.Clamp(stagesCleared, 0, CampaignStage.Count));
            SaveActive();
        }

        public void CloseActiveSlot()
        {
            ActiveSlot = 0;
            ActiveData = null;
            RefreshSummaries();
        }

        private void SaveActive()
        {
            if (ServerBound)
                return;
            if (ActiveData == null || ActiveSlot == 0)
                return;
            try
            {
                Directory.CreateDirectory(saveDirectory);
                var path = GetPath(ActiveSlot);
                var temporaryPath = GetTemporaryPath(ActiveSlot);
                var backupPath = GetBackupPath(ActiveSlot);
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(ActiveData, true), new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(temporaryPath, path, backupPath);
                else
                    File.Move(temporaryPath, path);
                RefreshSummaries();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Save Slots] Save failed: " + exception.Message);
            }
        }

        private SaveSlotData LoadSlot(int slotNumber)
        {
            var data = LoadFromPath(GetPath(slotNumber));
            return data ?? LoadFromPath(GetBackupPath(slotNumber));
        }

        private static SaveSlotData LoadFromPath(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var data = JsonUtility.FromJson<SaveSlotData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null)
                    return null;
                data.totalRuns = Mathf.Max(0, data.totalRuns);
                data.victories = Mathf.Clamp(data.victories, 0, data.totalRuns);
                data.campaignVictories = Mathf.Clamp(data.campaignVictories, 0, data.victories);
                data.bestCampaignStage = Mathf.Clamp(data.bestCampaignStage, 0, CampaignStage.Count);
                data.totalKills = Mathf.Max(0, data.totalKills);
                data.deepestRoom = Mathf.Max(0, data.deepestRoom);
                data.legacyShards = Mathf.Max(0, data.legacyShards);
                var ranks = new Dictionary<MetaUpgradeType, int>();
                if (data.upgrades != null)
                    foreach (var upgrade in data.upgrades)
                    {
                        if (upgrade == null)
                            continue;
                        var definition = Array.Find(MetaUpgradeCatalog.Definitions, item => item.Type == upgrade.type);
                        if (definition == null)
                            continue;
                        var rank = Mathf.Clamp(upgrade.rank, 0, definition.MaxRank);
                        if (!ranks.TryGetValue(upgrade.type, out var previous) || rank > previous)
                            ranks[upgrade.type] = rank;
                    }
                data.upgrades = new List<SaveUpgradeRankData>();
                foreach (var rank in ranks)
                    data.upgrades.Add(new SaveUpgradeRankData { type = rank.Key, rank = rank.Value });
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Save Slots] Load failed: " + exception.Message);
                return null;
            }
        }

        private void RefreshSummaries()
        {
            if (ServerBound)
                return;
            for (var slot = 1; slot <= SlotCount; slot++)
            {
                var data = LoadSlot(slot);
                summaries[slot - 1] = CreateSummary(slot, data);
            }
        }

        private static SaveSlotSummary CreateSummary(int slotNumber, SaveSlotData data)
        {
            var summary = new SaveSlotSummary { SlotNumber = slotNumber, Exists = data != null };
            if (data == null)
                return summary;

            DateTime playedUtc;
            if (DateTime.TryParse(data.lastPlayedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out playedUtc))
                summary.LastPlayed = playedUtc.ToLocalTime().ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture);
            summary.TotalRuns = Mathf.Max(0, data.totalRuns);
            summary.Victories = Mathf.Max(0, data.victories);
            summary.CampaignVictories = Mathf.Max(0, data.campaignVictories);
            summary.BestCampaignStage = Mathf.Clamp(data.bestCampaignStage, 0, CampaignStage.Count);
            summary.DeepestRoom = Mathf.Max(0, data.deepestRoom);
            summary.TotalKills = Mathf.Max(0, data.totalKills);
            summary.LegacyShards = Mathf.Max(0, data.legacyShards);
            return summary;
        }

        private static SaveSlotData CreateNewData(int slotNumber)
        {
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return new SaveSlotData { slotNumber = slotNumber, createdUtc = now, lastPlayedUtc = now };
        }

        private static bool HasCommandLineArgument(string expected)
        {
            foreach (var argument in Environment.GetCommandLineArgs())
                if (string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        [Serializable]
        private sealed class LegacyMetaData
        {
            public int legacyShards = 3;
            public List<SaveUpgradeRankData> upgrades = new List<SaveUpgradeRankData>();
        }

        private void TryImportLegacyProgression(SaveSlotData data)
        {
            try
            {
                var legacyPath = Path.Combine(Application.persistentDataPath, "RuinRunner", "meta-progression.json");
                if (!File.Exists(legacyPath))
                    return;
                var legacy = JsonUtility.FromJson<LegacyMetaData>(File.ReadAllText(legacyPath, Encoding.UTF8));
                if (legacy == null)
                    return;
                data.legacyShards = Mathf.Max(0, legacy.legacyShards);
                data.upgrades = legacy.upgrades ?? new List<SaveUpgradeRankData>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Save Slots] Legacy import failed: " + exception.Message);
            }
        }

        private string GetPath(int slotNumber) => Path.Combine(saveDirectory, "save" + slotNumber + ".json");
        private string GetBackupPath(int slotNumber) => GetPath(slotNumber) + ".bak";
        private string GetTemporaryPath(int slotNumber) => GetPath(slotNumber) + ".tmp";
        private static bool IsValidSlot(int slotNumber) => slotNumber >= 1 && slotNumber <= SlotCount;

        public string ExistingLegacyPath(int slotNumber)
        {
            if (!IsValidSlot(slotNumber))
                return null;
            if (File.Exists(GetPath(slotNumber)))
                return GetPath(slotNumber);
            if (File.Exists(GetBackupPath(slotNumber)))
                return GetBackupPath(slotNumber);
            var old = Path.Combine(Path.GetDirectoryName(saveDirectory), "meta-progression.json");
            return slotNumber == 1 && File.Exists(old) ? old : null;
        }
        public void BindServerProfiles(ProfileView[] profiles)
        {
            ServerBound = true;
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                var profile = Array.Find(profiles ?? Array.Empty<ProfileView>(), p => p.slot == slot);
                var summary = CreateSummary(slot, profile == null ? LoadSlot(slot) : Project(profile));
                summary.ProfileId = profile?.profileId;
                summary.Name = profile?.name;
                summary.IsServerProfile = profile != null;
                summaries[slot - 1] = summary;
            }
        }
        public void ApplyServerProfile(ProfileView profile)
        {
            if (profile == null || !IsValidSlot(profile.slot))
                throw new ArgumentException("Valid confirmed profile required.");
            ServerBound = true;
            ActiveSlot = profile.slot;
            ActiveData = Project(profile);
            var summary = CreateSummary(profile.slot, ActiveData);
            summary.ProfileId = profile.profileId;
            summary.Name = profile.name;
            summary.IsServerProfile = true;
            summaries[profile.slot - 1] = summary;
        }
        static SaveSlotData Project(ProfileView p)
        {
            var s = p.state ?? throw new ArgumentException("Profile state missing.");
            return new SaveSlotData
            {
                slotNumber = p.slot,
                serverProfileId = p.profileId,
                serverRevision = p.revision,
                totalRuns = s.totalRuns,
                victories = s.victories,
                campaignVictories = s.campaignVictories,
                bestCampaignStage = s.bestCampaignStage,
                deepestRoom = s.deepestRoom,
                totalKills = s.totalKills,
                legacyShards = checked((int)p.legacyShards),
                legacyMigrationVersion = s.legacyMigrationVersion,
                legacyRefundVersion = s.legacyRefundVersion,
                startingWeaponId = s.startingWeaponId,
                unlockedWeaponIds = s.unlockedWeaponIds ?? Array.Empty<string>(),
                unlockedTalentIds = s.unlockedTalentIds ?? Array.Empty<string>(),
                activeTalentIds = s.activeTalentIds ?? Array.Empty<string>()
            };
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
