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
    [Serializable] public sealed class CreateProfileRequest { public string operationId; public int slot; public string name; }
    [Serializable] public sealed class BeginProfileResetRequest { public string operationId; public long expectedRevision; }
    [Serializable] public sealed class ConfirmProfileResetRequest { public string operationId; public long expectedRevision; public string challengeId; }
    [Serializable] public sealed class ProfileResetChallenge
    { public string challengeId; public string profileId; public int slot; public string name; public long profileRevision; public string expiresUtc; }
    [Serializable] public sealed class ProfileResetReceipt { public ProfileView profile; public string challengeId; public string operationId; }
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
        public string trustOrigin; public int legacyMigrationVersion; public int legacyRefundVersion;
        public bool legacyImportBlocked; public int resetCount;
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
    [Serializable] public sealed class StarterWeapon { public string id; public int unlockCost; public string name; }
    [Serializable] public sealed class TalentDefinition { public string id; public string branch; public int unlockCost; public int requiredRuns; public string name; public string description; }
    [Serializable] public sealed class SettlementRules
    { public int minimumReward; public int killsPerShard; public int rewardPerRoom; public int victoryBonus; public int campaignStages; public int maxKills; public int maxRooms; }
    [Serializable] public sealed class ContentCatalog
    {
        public string version; public string status; public string[] generatorVersions; public int startingLegacy; public int maxActiveTalents;
        public StarterWeapon[] starterWeapons; public TalentDefinition[] talents; public SettlementRules settlement;
        public string[] defaultUnlockedTalentIds; public CatalogItem[] items; public EconomyRules economy; public ExchangeRecipe[] exchangeRecipes;
        public int accessoryTuningVersion; public CatalogItemSet[] itemSets;
    }
    [Serializable] public sealed class CatalogItem
    { public string id; public string kind; public string name; public string description; public int rarity; public int price; public int sellPrice; public int dropWeight; public int minStage; public int maxStage; public Newtonsoft.Json.Linq.JObject stats; }
    [Serializable] public sealed class CatalogItemSet
    { public string id; public int pieces; public string name; public string description; public Newtonsoft.Json.Linq.JObject stats; }
    [Serializable] public sealed class EconomyRules
    { public int startingGold; public int enemyGold; public int combatGold; public int chestGold; public int bossGold; public int healPrice; public int healAmount; public int shopOffers; public int maxOwnedItems; public int maxAccessories; }
    [Serializable] public sealed class ExchangeRecipe { public string id; public string[] inputIds; public string outputId; public string name; }
    [Serializable] public sealed class TradeRequest
    { public string operationId; public long expectedRevision; public string action; public string shopId; public string offerId; public string instanceId; public string[] instanceIds; public string recipeId; public string objectId; }
    [Serializable] public sealed class RewardRequest
    { public string operationId; public string eventId; public string source; public int stageIndex; public string roomId; public string contentId; }
    [Serializable] public sealed class OwnedItem
    { public string instanceId; public string contentId; public string kind; public int sellPrice; public string origin; }
    [Serializable] public sealed class ShopOffer { public string offerId; public string contentId; public string kind; public int price; public int stock; }
    [Serializable] public sealed class ShopView { public string shopId; public int stageIndex; public bool barterUsed; public ShopOffer[] offers; }
    [Serializable] public sealed class EconomyView { public string runId; public long revision; public long gold; public OwnedItem[] items; public ShopView[] shops; }
    [Serializable] public sealed class EconomyReceipt
    { public EconomyView economy; public OwnedItem[] grantedItems; public string[] removedInstanceIds; public long goldDelta; public int healAmount; public string operationId; public string sourceEventId; }
    [Serializable] internal sealed class QueuedEconomyOperation
    { public string apiOrigin; public string runId; public string operationId; public string createdUtc; public string status = "pending"; public TradeRequest trade; public RewardRequest reward; public EconomyReceipt receipt; public bool prepared; }
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
