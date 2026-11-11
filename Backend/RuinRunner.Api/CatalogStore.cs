using Npgsql;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RuinRunner.Api;

public sealed class CatalogStore(Database db, IConfiguration configuration)
{
    // The package's shipped version. An admin activation only overrides it while the package still ships the same one.
    public string ConfiguredVersion { get; } = configuration["RR_CONTENT_VERSION"] ?? "campaign-v1";
    volatile string currentVersion = configuration["RR_CONTENT_VERSION"] ?? "campaign-v1";
    public string CurrentVersion => currentVersion;
    public HashSet<string> KnownEnemyTypes { get; } = new(StringComparer.Ordinal);

    public async Task PublishDirectory(string directory)
    {
        await using var c = await db.Source.OpenConnectionAsync();
        foreach (var path in Directory.GetFiles(directory, "*.json").Order())
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var catalog = Wire.Decode<Catalog>(json);
            var hash = Database.Hash(json);
            Validate(catalog);
            await Database.Exec(c, "INSERT INTO content_catalogs(version,sha256,payload) VALUES($1,$2,$3::jsonb) ON CONFLICT(version) DO NOTHING", catalog.Version, hash, json);
            var actual = await Database.Scalar(c, "SELECT sha256 FROM content_catalogs WHERE version=$1", catalog.Version);
            if (!Equals(actual, hash))
                throw new InvalidOperationException("Published content version is immutable: " + catalog.Version);
        }
        if (await Database.Scalar(c, "SELECT 1 FROM content_catalogs WHERE version=$1", ConfiguredVersion) == null)
            throw new InvalidOperationException("Configured catalog version is not published");
    }

    public void LoadEnemyTypes(string path)
    {
        if (!File.Exists(path))
            return;
        using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        foreach (var row in document.RootElement.GetProperty("enemies").EnumerateArray())
            KnownEnemyTypes.Add(row.GetProperty("type").GetString()!);
    }

    // Restores the admin's live choice after a restart; a missing table (older schema) simply keeps the shipped version.
    public async Task RestoreActivation()
    {
        await using var c = await db.Source.OpenConnectionAsync();
        try
        {
            await using var cmd = Database.Command(c, "SELECT version, configured_version FROM catalog_activation WHERE id=1");
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync() && reader.GetString(1) == ConfiguredVersion)
                currentVersion = reader.GetString(0);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable)
        {
        }
    }

    public void Validate(Catalog catalog)
    {
        CatalogTuningValidation.Validate(catalog);
        Wire.Require(catalog.StartingLegacy >= 0 && catalog.Settlement.KillsPerShard > 0, "catalog_invalid", "카탈로그 규칙이 유효하지 않습니다.");
        Wire.Require(catalog.Talents.All(x => x.UnlockCost >= 0 && x.RequiredRuns >= 0) && catalog.StarterWeapons.All(x => x.UnlockCost >= 0), "catalog_invalid", "해금 조건을 확인하세요.");
        Wire.Require(catalog.Talents.Select(x => x.Id).Distinct().Count() == catalog.Talents.Length && catalog.StarterWeapons.Select(x => x.Id).Distinct().Count() == catalog.StarterWeapons.Length, "catalog_invalid", "중복 콘텐츠 식별자가 있습니다.");
        if (catalog.Economy is { } economy)
        {
            var items = catalog.Items ?? [];
            Wire.Require(items.Length > 0, "catalog_invalid", "아이템이 없습니다.");
            var duplicate = items.GroupBy(x => x.Id).FirstOrDefault(g => g.Count() > 1);
            Wire.Require(duplicate == null, "catalog_invalid", "아이템 식별자가 겹칩니다: " + duplicate?.Key);
            foreach (var x in items)
            {
                Wire.Require(x.Kind is "weapon" or "accessory", "catalog_invalid", "아이템 종류를 확인하세요: " + x.Id);
                Wire.Require(x.Price >= 0 && x.SellPrice >= 0 && x.SellPrice <= x.Price, "catalog_invalid", "판매가는 0 이상, 가격 이하여야 합니다: " + x.Name + " (" + x.Id + ")");
                Wire.Require(x.DropWeight >= 0, "catalog_invalid", "드롭 가중치는 0 이상이어야 합니다: " + x.Name + " (" + x.Id + ")");
                Wire.Require(x.MinStage >= 0 && x.MaxStage >= x.MinStage && x.MaxStage < catalog.Settlement.CampaignStages, "catalog_invalid", "등장 지역의 시작이 끝보다 늦습니다: " + x.Name + " (" + x.Id + ")");
            }
            Wire.Require(catalog.StarterWeapons.All(s => items.Any(x => x.Id == s.Id && x.Kind == "weapon" && x.BaseId == null)), "catalog_invalid", "시작 장비 정의가 없습니다.");
            Wire.Require(economy.StartingGold >= 0 && economy.EnemyGold >= 0 && economy.CombatGold >= 0 && economy.ChestGold >= 0 && economy.BossGold >= 0 && economy.HealPrice > 0 && economy.HealAmount > 0 && economy.ShopOffers is > 0 and <= 10 && economy.MaxOwnedItems > 0 && economy.MaxAccessories > 0, "catalog_invalid", "경제 규칙을 확인하세요.");
            Wire.Require((catalog.DefaultUnlockedTalentIds ?? []).All(t => catalog.Talents.Any(x => x.Id == t && x.UnlockCost == 0)), "catalog_invalid", "기본 재능 정의를 확인하세요.");
            foreach (var variant in items.Where(x => x.BaseId != null))
            {
                Wire.Require(variant.Kind == "weapon" && VariantId.IsMatch(variant.Id), "catalog_invalid", "새 무기 식별자는 영문 소문자·숫자·밑줄 3~48자입니다.");
                Wire.Require(items.Any(x => x.Id == variant.BaseId && x.Kind == "weapon" && x.BaseId == null), "catalog_invalid", "새 무기의 기반 무기를 확인하세요.");
                Wire.Require(!string.IsNullOrWhiteSpace(variant.Name), "catalog_invalid", "새 무기 이름이 필요합니다.");
            }
            ValidateExchangeRecipes(catalog);
        }
        if (catalog.EnemyBalance is { } balance)
        {
            static bool In(double v, double min, double max) => double.IsFinite(v) && v >= min && v <= max;
            Wire.Require(In(balance.EnemyHealthMultiplier, .1, 10) && In(balance.BossHealthMultiplier, .1, 10) && In(balance.ExperienceGainMultiplier, 0, 10)
                && In(balance.BossBurstThreshold, .005, 1) && In(balance.BossBurstSeconds, .1, 30) && In(balance.BossItemDamageMultiplier, 0, 2), "catalog_invalid", "몬스터 배율 범위를 확인하세요.");
            var rows = balance.Enemies ?? [];
            Wire.Require(rows.Select(x => x.Type).Distinct().Count() == rows.Length, "catalog_invalid", "몬스터가 중복되었습니다.");
            Wire.Require(rows.All(x => (KnownEnemyTypes.Count == 0 || KnownEnemyTypes.Contains(x.Type)) && In(x.MaxHealth, 1, 1_000_000) && In(x.AttackPower, 0, 10_000)
                && In(x.Armor, 0, 10_000) && In(x.MoveSpeed, .05, 50) && x.ExperienceReward is >= 0 and <= 100_000), "catalog_invalid", "몬스터 수치 범위를 확인하세요.");
        }
    }
    static readonly Regex VariantId = new("^[a-z0-9_]{3,48}$", RegexOptions.CultureInvariant);

    // The game's own load-time limits (CombatCatalogBinding). Anything outside them would stop the client binding the catalog.
    public static readonly Dictionary<string, (double Min, double Max, bool Integer)> WeaponStatRules = new()
    {
        ["attackPower"] = (.1, 1000, false), ["attackDelayMultiplier"] = (.1, 5, false), ["magazineSize"] = (0, 100, true),
        ["reloadSeconds"] = (0, 10, false), ["projectileSpeed"] = (1, 80, false), ["projectilePierce"] = (1, 16, true),
        ["heavyDamageMultiplier"] = (1, 4, false), ["guardDamageReduction"] = (0, .8, false), ["specialManaCost"] = (0, 100, false),
        ["specialCooldown"] = (.25, 15, false)
    };

    // Stricter checks for admin edits than for shipped history, which already loads in the game as it is.
    void ValidateAdmin(Catalog catalog)
    {
        Validate(catalog);
        var items = catalog.Items ?? [];
        Wire.Require(catalog.Economy != null && items.Length > 0, "catalog_invalid", "아이템과 경제 규칙이 있는 버전만 편집할 수 있습니다.");
        foreach (var item in items)
        {
            Wire.Require(item.Rarity is >= 0 and <= 4 && item.DropWeight <= 1_000_000 && !string.IsNullOrWhiteSpace(item.Name), "catalog_invalid", "등급·드롭 가중치·이름을 확인하세요: " + item.Id);
            if (item.Kind != "weapon")
                continue;
            Wire.Require(item.Stats.ValueKind == JsonValueKind.Object, "catalog_invalid", "무기 수치 형식을 확인하세요: " + item.Id);
            foreach (var field in item.Stats.EnumerateObject())
            {
                var value = 0d;
                Wire.Require(WeaponStatRules.TryGetValue(field.Name, out var rule) && field.Value.ValueKind == JsonValueKind.Number && field.Value.TryGetDouble(out value), "catalog_invalid", "알 수 없는 무기 수치입니다: " + item.Id + "." + field.Name);
                Wire.Require(double.IsFinite(value) && value >= rule.Min && value <= rule.Max && (!rule.Integer || value == Math.Truncate(value)), "catalog_invalid", "무기 수치 범위를 확인하세요: " + item.Id + "." + field.Name + " (" + rule.Min + " ~ " + rule.Max + ")");
            }
        }
        for (int stage = 0; stage < catalog.Settlement.CampaignStages; stage++)
            foreach (var kind in new[] { "weapon", "accessory" })
                Wire.Require(items.Any(x => x.Kind == kind && x.DropWeight > 0 && x.MinStage <= stage && x.MaxStage >= stage), "catalog_invalid", (stage + 1) + "지역에 나올 " + (kind == "weapon" ? "무기" : "장신구") + "가 하나도 없습니다.");
        Wire.Require(catalog.Talents.All(t => t.UnlockCost <= 100_000 && t.RequiredRuns <= 10_000) && catalog.StartingLegacy <= 100_000, "catalog_invalid", "해금 비용을 확인하세요.");
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

    // A new immutable version derived from the edited catalog; active for every expedition started after this returns.
    public async Task<string> PublishAdmin(AdminPublishRequest request, string remote)
    {
        Wire.Require(request.Catalog is not null, "invalid_request", "카탈로그가 필요합니다.");
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await Database.Exec(c, "SELECT pg_advisory_xact_lock(86269420261111)");
        var baseCatalog = await Get(c, request.BaseVersion);
        var version = ConfiguredVersion + "-admin-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        // Generator support and the tuning schema come from the base, never from the edited copy.
        var catalog = request.Catalog! with
        {
            Version = version,
            Status = "published",
            GeneratorVersions = baseCatalog.GeneratorVersions,
            AccessoryTuningVersion = baseCatalog.AccessoryTuningVersion
        };
        ValidateAdmin(catalog);
        var json = JsonSerializer.Serialize(catalog, Wire.CatalogJson);
        await Database.Exec(c, "INSERT INTO content_catalogs(version,sha256,payload) VALUES($1,$2,$3::jsonb)", version, Database.Hash(json), json);
        await Database.Exec(c, "INSERT INTO admin_catalog_history(version,base_version,note) VALUES($1,$2,$3)", version, request.BaseVersion, (request.Note ?? "").Trim());
        await Activate(c, version, "publish", remote);
        await tx.CommitAsync();
        currentVersion = version;
        return version;
    }

    public async Task ActivateAdmin(string version, string remote)
    {
        await using var c = await db.Source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        Wire.Require(await Database.Scalar(c, "SELECT 1 FROM content_catalogs WHERE version=$1", version) != null, "catalog_missing", "없는 버전입니다.", 404);
        var catalog = await Get(c, version);
        Wire.Require(catalog.Economy != null && catalog.Items is { Length: > 0 }, "catalog_invalid", "현재 게임에서 쓸 수 없는 이전 형식 버전입니다.");
        await Activate(c, version, "activate", remote);
        await tx.CommitAsync();
        currentVersion = version;
    }

    async Task Activate(NpgsqlConnection c, string version, string action, string remote)
    {
        await Database.Exec(c, "INSERT INTO catalog_activation(id,version,configured_version,activated_utc) VALUES(1,$1,$2,now()) ON CONFLICT(id) DO UPDATE SET version=EXCLUDED.version, configured_version=EXCLUDED.configured_version, activated_utc=now()", version, ConfiguredVersion);
        await Database.Exec(c, "INSERT INTO admin_audit(action,version,remote_address) VALUES($1,$2,$3)", action, version, remote);
    }
}
