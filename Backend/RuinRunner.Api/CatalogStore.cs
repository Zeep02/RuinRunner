using Npgsql;
using System.Text;

namespace RuinRunner.Api;

public sealed class CatalogStore(Database db, IConfiguration configuration)
{
    public string CurrentVersion { get; private set; } = configuration["RR_CONTENT_VERSION"] ?? "campaign-v1";
    public async Task PublishDirectory(string directory)
    {
        await using var c = await db.Source.OpenConnectionAsync();
        foreach (var path in Directory.GetFiles(directory, "*.json").Order())
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var catalog = Wire.Decode<Catalog>(json);
            var hash = Database.Hash(json);
            CatalogTuningValidation.Validate(catalog);
            Wire.Require(catalog.StartingLegacy >= 0 && catalog.Settlement.KillsPerShard > 0, "catalog_invalid", "카탈로그 규칙이 유효하지 않습니다.");
            Wire.Require(catalog.Talents.All(x => x.UnlockCost >= 0 && x.RequiredRuns >= 0) && catalog.StarterWeapons.All(x => x.UnlockCost >= 0), "catalog_invalid", "해금 조건을 확인하세요.");
            Wire.Require(catalog.Talents.Select(x => x.Id).Distinct().Count() == catalog.Talents.Length && catalog.StarterWeapons.Select(x => x.Id).Distinct().Count() == catalog.StarterWeapons.Length, "catalog_invalid", "중복 콘텐츠 식별자가 있습니다.");
            if (catalog.Economy is { } economy)
            {
                var items = catalog.Items ?? [];
                Wire.Require(items.Length > 0 && items.Select(x => x.Id).Distinct().Count() == items.Length && items.All(x => x.Kind is "weapon" or "accessory" && x.Price >= 0 && x.SellPrice >= 0 && x.SellPrice <= x.Price && x.DropWeight >= 0 && x.MinStage >= 0 && x.MaxStage >= x.MinStage && x.MaxStage < catalog.Settlement.CampaignStages), "catalog_invalid", "아이템 규칙을 확인하세요.");
                Wire.Require(catalog.StarterWeapons.All(s => items.Any(x => x.Id == s.Id && x.Kind == "weapon")), "catalog_invalid", "시작 장비 정의가 없습니다.");
                Wire.Require(economy.StartingGold >= 0 && economy.EnemyGold >= 0 && economy.CombatGold >= 0 && economy.ChestGold >= 0 && economy.BossGold >= 0 && economy.HealPrice > 0 && economy.HealAmount > 0 && economy.ShopOffers is > 0 and <= 10 && economy.MaxOwnedItems > 0 && economy.MaxAccessories > 0, "catalog_invalid", "경제 규칙을 확인하세요.");
                Wire.Require((catalog.DefaultUnlockedTalentIds ?? []).All(t => catalog.Talents.Any(x => x.Id == t && x.UnlockCost == 0)), "catalog_invalid", "기본 재능 정의를 확인하세요.");
                ValidateExchangeRecipes(catalog);
            }
            await Database.Exec(c, "INSERT INTO content_catalogs(version,sha256,payload) VALUES($1,$2,$3::jsonb) ON CONFLICT(version) DO NOTHING", catalog.Version, hash, json);
            var actual = await Database.Scalar(c, "SELECT sha256 FROM content_catalogs WHERE version=$1", catalog.Version);
            if (!Equals(actual, hash))
                throw new InvalidOperationException("Published content version is immutable: " + catalog.Version);
        }
        if (await Database.Scalar(c, "SELECT 1 FROM content_catalogs WHERE version=$1", CurrentVersion) == null)
            throw new InvalidOperationException("Configured catalog version is not published");
    }
    public static void ValidateExchangeRecipes(Catalog catalog)
    {
        var items = catalog.Items ?? [];
        var recipes = catalog.ExchangeRecipes ?? [];
        Wire.Require(recipes.All(r => r is not null && !string.IsNullOrWhiteSpace(r.Id) && r.InputIds is { Length: 2 }
            && r.InputIds.Distinct().Count() == 2 && r.InputIds.All(id => items.Any(x => x.Id == id && x.Kind == "accessory"))
            && items.Any(x => x.Id == r.OutputId && x.Kind == "accessory"))
            && recipes.Select(r => r.Id).Distinct().Count() == recipes.Length, "catalog_invalid", "교환 재료 정의를 확인하세요.");
    }
    public async Task<Catalog> Get(NpgsqlConnection c, string version)
    {
        var payload = await Database.Scalar(c, "SELECT payload::text FROM content_catalogs WHERE version=$1", version);
        return payload is string json ? Wire.Decode<Catalog>(json) : throw new ApiFailure(409, "catalog_missing", "콘텐츠 버전을 불러올 수 없습니다.");
    }
}
