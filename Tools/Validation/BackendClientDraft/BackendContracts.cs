using System;

namespace RuinRunner.Backend
{
    [Serializable] public sealed class BackendClientOptions
    {
        public string ApiBaseUri = "http://127.0.0.1:5186/";
        public string StorageDirectory;
        public int TimeoutSeconds = 10;
    }
    [Serializable] public sealed class GuestIdentity { public string installationId; public string guestSecret; }
    [Serializable] public sealed class GuestSession { public string playerId; public string accessToken; public string expiresUtc; }
    [Serializable] public sealed class OperationRequest { public string operationId; }
    [Serializable] public sealed class CreateProfileRequest { public string operationId; public int slot; public string name; }
    [Serializable] public sealed class ConfigurationRequest { public string operationId; public long expectedRevision; public string startingWeaponId; public string[] activeTalentIds = Array.Empty<string>(); }
    [Serializable] public sealed class UnlockRequest { public string operationId; public long expectedRevision; public string kind; public string contentId; public string contentVersion; }
    [Serializable] public sealed class LegacyImportRequest { public string operationId; public long expectedRevision; public int migrationVersion = 1; public string sourceSha256; public string sourceJson; }
    [Serializable] public sealed class StartRunRequest
    {
        public string operationId; public string profileId; public long expectedProfileRevision; public int? requestedSeed;
        public string contentVersion; public string generatorVersion; public string buildId;
    }
    [Serializable] public sealed class SettleRunRequest
    { public string operationId; public long expectedRunRevision; public string outcome; public int enemiesDefeated; public int roomsReached; public int stagesCleared; }
    [Serializable] public sealed class ProfileList { public ProfileView[] profiles = Array.Empty<ProfileView>(); }
    [Serializable] public sealed class ProfileView
    { public string profileId; public string playerId; public int slot; public string name; public long revision; public long legacyShards; public ProfileState state; }
    [Serializable] public sealed class ProfileState
    {
        public int totalRuns; public int victories; public int campaignVictories; public int bestCampaignStage; public int deepestRoom; public int totalKills;
        public string startingWeaponId; public string[] unlockedWeaponIds; public string[] unlockedTalentIds; public string[] activeTalentIds;
        public string trustOrigin; public int legacyMigrationVersion;
    }
    [Serializable] public sealed class RunView
    {
        public string runId; public string profileId; public long revision; public string status; public int seed; public string contentVersion;
        public string generatorVersion; public string buildId; public ProfileState loadout; public long legacyReward; public string trustOrigin;
    }
    [Serializable] public sealed class SettlementReceipt { public RunView run; public ProfileView profile; }
    [Serializable] public sealed class ActiveRunResponse { public RunView run; }
    [Serializable] internal sealed class PlayerBinding { public string playerId = string.Empty; public string apiOrigin = string.Empty; }
    [Serializable] public sealed class ImportReceipt
    { public ProfileView profile; public long refundedLegacy; public int migrationVersion; public string sourceSha256; public string trustOrigin; }
    [Serializable] public sealed class StarterWeapon { public string id; public int unlockCost; }
    [Serializable] public sealed class TalentDefinition { public string id; public string branch; public int unlockCost; }
    [Serializable] public sealed class SettlementRules
    { public int minimumReward; public int killsPerShard; public int rewardPerRoom; public int victoryBonus; public int campaignStages; public int maxKills; public int maxRooms; }
    [Serializable] public sealed class ContentCatalog
    {
        public string version; public string status; public string[] generatorVersions; public int startingLegacy; public int maxActiveTalents;
        public StarterWeapon[] starterWeapons; public TalentDefinition[] talents; public SettlementRules settlement;
    }
    [Serializable] public sealed class CatalogSnapshot { public string apiOrigin; public string version; public string sha256; public string json; }
    [Serializable] public sealed class BackendErrorBody { public string code; public string message; public string traceId; }
    [Serializable] public sealed class PendingOperation
    {
        public string operationId; public string playerId; public string apiOrigin; public string method; public string path; public string payload;
        public string status = "pending"; public string createdUtc; public string responseJson; public string lastErrorCode; public int lastStatusCode;
    }
    [Serializable] public sealed class LegacyBackup
    {
        public string backupId; public string originalSha256; public string importSha256; public string sourceJson;
        public string encryptedOriginalPath; public string encryptedRecoveryPath; public string createdUtc;
    }
    public sealed class BackendClientException : Exception
    {
        public int StatusCode { get; }
        public string Code { get; }
        public bool CanRetry { get; }
        public bool OutcomeUnknown { get; }
        public BackendClientException(int status, string code, string message, bool retry = false, bool unknown = false, Exception inner = null)
            : base(message, inner) { StatusCode = status; Code = code; CanRetry = retry; OutcomeUnknown = unknown; }
    }
}
