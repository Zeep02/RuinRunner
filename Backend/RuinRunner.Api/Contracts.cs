using System.Text.Json;

namespace RuinRunner.Api;

public sealed record GuestRequest(Guid InstallationId, string GuestSecret);
public sealed record GuestResponse(Guid PlayerId, string AccessToken, DateTimeOffset ExpiresUtc);
public sealed record CreateProfileRequest(Guid OperationId, int Slot, string Name);
public sealed record BeginProfileResetRequest(Guid OperationId, long ExpectedRevision);
public sealed record ConfirmProfileResetRequest(Guid OperationId, long ExpectedRevision, Guid ChallengeId);
public sealed record ProfileResetChallenge(Guid ChallengeId, Guid ProfileId, int Slot, string Name, long ProfileRevision, DateTimeOffset ExpiresUtc);
public sealed record ProfileResetReceipt(ProfileView Profile, Guid ChallengeId, Guid OperationId);
public sealed record ConfigurationRequest(Guid OperationId, long ExpectedRevision, string StartingWeaponId, string[] ActiveTalentIds);
public sealed record UnlockRequest(Guid OperationId, long ExpectedRevision, string Kind, string ContentId, string ContentVersion);
public sealed record LegacyRank(int Type, int Rank);
public sealed record LegacySnapshot(int Version, int SlotNumber, string CreatedUtc, string LastPlayedUtc,
    int TotalRuns, int Victories, int CampaignVictories, int BestCampaignStage, int DeepestRoom, int TotalKills,
    long LegacyShards, LegacyRank[] Upgrades);
// Exact UTF-8 source is retained. Its hash is computed by the server, not trusted from a client field.
public sealed record ImportRequest(Guid OperationId, long ExpectedRevision, int MigrationVersion, string SourceSha256, string SourceJson);
public sealed record StartRunRequest(Guid OperationId, Guid ProfileId, long ExpectedProfileRevision, int? RequestedSeed,
    string ContentVersion, string GeneratorVersion, string BuildId);
public sealed record SettleRequest(Guid OperationId, long ExpectedRunRevision, string Outcome, int EnemiesDefeated,
    int RoomsReached, int StagesCleared);
public sealed record StarterWeapon(string Id, int UnlockCost, string Name = "");
public sealed record Talent(string Id, string Branch, int UnlockCost, int RequiredRuns = 0, string Name = "", string Description = "");
public sealed record SettlementRules(int MinimumReward, int KillsPerShard, int RewardPerRoom, int VictoryBonus,
    int CampaignStages, int MaxKills, int MaxRooms);
public sealed record Catalog(string Version, string Status, string[] GeneratorVersions, int StartingLegacy,
    int MaxActiveTalents, StarterWeapon[] StarterWeapons, Talent[] Talents, SettlementRules Settlement,
    string[]? DefaultUnlockedTalentIds = null, CatalogItem[]? Items = null, EconomyRules? Economy = null,
    ExchangeRecipe[]? ExchangeRecipes = null, int AccessoryTuningVersion = 0, CatalogItemSet[]? ItemSets = null);
public sealed record CatalogItem(string Id, string Kind, string Name, string Description, int Rarity,
    int Price, int SellPrice, int DropWeight, int MinStage, int MaxStage, JsonElement Stats);
public sealed record CatalogItemSet(string Id, int Pieces, string Name, string Description, JsonElement Stats);
public sealed record EconomyRules(int StartingGold, int EnemyGold, int CombatGold, int ChestGold, int BossGold,
    int HealPrice, int HealAmount, int ShopOffers, int MaxOwnedItems, int MaxAccessories);
public sealed record ExchangeRecipe(string Id, string[] InputIds, string OutputId, string Name);
public sealed record TradeRequest(Guid OperationId, long ExpectedRevision, string Action, string? ShopId = null,
    string? OfferId = null, Guid? InstanceId = null, Guid[]? InstanceIds = null, string? RecipeId = null, string? ObjectId = null);
// Reward facts are client-reported in this single-player build, never claimed as anti-cheat authority.
public sealed record RewardRequest(Guid OperationId, string EventId, string Source, int StageIndex, string RoomId, string? ContentId = null);
public sealed record OwnedItem(Guid InstanceId, string ContentId, string Kind, int SellPrice, string Origin);
public sealed record ShopOffer(string OfferId, string ContentId, string Kind, int Price, int Stock);
public sealed record ShopView(string ShopId, int StageIndex, bool BarterUsed, ShopOffer[] Offers);
public sealed record EconomyView(Guid RunId, long Revision, long Gold, OwnedItem[] Items, ShopView[] Shops);
public sealed record EconomyReceipt(EconomyView Economy, OwnedItem[] GrantedItems, Guid[] RemovedInstanceIds, long GoldDelta, int HealAmount, Guid OperationId, string? SourceEventId = null);
public sealed record ProfileView(Guid ProfileId, Guid PlayerId, int Slot, string Name, long Revision, long LegacyShards, ProfileState State);
public sealed class ProfileState
{
    public int TotalRuns { get; set; }
    public int Victories { get; set; }
    public int CampaignVictories { get; set; }
    public int BestCampaignStage { get; set; }
    public int DeepestRoom { get; set; }
    public int TotalKills { get; set; }
    public string StartingWeaponId { get; set; } = "rust_sword";
    public string[] UnlockedWeaponIds { get; set; } = ["rust_sword"];
    public string[] UnlockedTalentIds { get; set; } = [];
    public string[] ActiveTalentIds { get; set; } = [];
    public string TrustOrigin { get; set; } = "server-created";
    public int LegacyMigrationVersion { get; set; }
    public int LegacyRefundVersion { get; set; }
    public int ProgressionVersion { get; set; } = 2;
    public bool LegacyImportBlocked { get; set; }
    public int ResetCount { get; set; }
}
public sealed record RunView(Guid RunId, Guid ProfileId, long Revision, string Status, int Seed, string ContentVersion,
    string GeneratorVersion, string BuildId, ProfileState Loadout, long LegacyReward, string TrustOrigin, RunGenerationView? Generation = null);
public sealed class ApiFailure(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json, Json) ?? throw new ApiFailure(400,"invalid_json","요청을 읽을 수 없습니다.");
    public static void Require(bool condition, string code, string message, int status = 400)
    { if (!condition) throw new ApiFailure(status, code, message); }
}
