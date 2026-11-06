using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;

namespace RuinRunner.Api;

/// <summary>Persistent single-player economy. Combat facts remain explicitly client-reported.</summary>
public sealed class EconomyService(Database db, CatalogStore catalogs)
{
    public static async Task Initialize(NpgsqlConnection c, RunView run, Catalog catalog)
    {
        if (catalog.Economy is not { } rules || catalog.Items is not { Length: > 0 } items)
            return;
        await Database.Exec(c, "INSERT INTO run_economy(run_id,gold) VALUES($1,$2)", run.RunId, (long)rules.StartingGold);
        var starter = items.Single(x => x.Id == run.Loadout.StartingWeaponId && x.Kind == "weapon");
        await Add(c, run.RunId, starter, "starter");
        for (var stage = 0; stage < catalog.Settlement.CampaignStages; stage++)
        {
            var shop = "stage-" + stage;
            await Database.Exec(c, "INSERT INTO run_shops(run_id,shop_id,stage_index) VALUES($1,$2,$3)", run.RunId, shop, stage);
            var choices = ChooseShopItems(run.Seed, stage, items, rules.ShopOffers);
            for (var n = 0; n < choices.Length; n++)
                await Database.Exec(c, "INSERT INTO run_shop_offers(run_id,shop_id,offer_id,content_id,kind,price,stock) VALUES($1,$2,$3,$4,$5,$6,1)", run.RunId, shop, "item-" + n, choices[n].Id, choices[n].Kind, choices[n].Price);
            await Database.Exec(c, "INSERT INTO run_shop_offers(run_id,shop_id,offer_id,content_id,kind,price,stock) VALUES($1,$2,'heal','field_heal','heal',$3,1)", run.RunId, shop, rules.HealPrice);
        }
    }
    public static CatalogItem[] ChooseShopItems(int seed, int stage, CatalogItem[] items, int count)
    {
        var eligible = items.Where(x => x.MinStage <= stage && x.MaxStage >= stage && x.Price > 0 && x.DropWeight > 0).ToArray();
        var weighted = eligible.OrderBy(x => ShopRank(seed, stage, x)).ThenBy(x => x.Id, StringComparer.Ordinal);
        if (stage != 0 || count <= 0 || eligible.Length == 0)
            return weighted.Take(count).ToArray();
        // The first combat leads to this merchant. Reserve a lowest-priced catalog item;
        // choose between price ties by the same seeded rank, leaving the other slots varied.
        var affordable = eligible.OrderBy(x => x.Price).ThenBy(x => ShopRank(seed, stage, x)).ThenBy(x => x.Id, StringComparer.Ordinal).First();
        return new[] { affordable }.Concat(weighted.Where(x => x.Id != affordable.Id).Take(count - 1)).ToArray();
    }
    static double ShopRank(int seed, int stage, CatalogItem item)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{stage}:{item.Id}"));
        var uniform = (BinaryPrimitives.ReadUInt32LittleEndian(hash) + 1d) / 4294967297d;
        return -Math.Log(uniform) / item.DropWeight; // Stable weighted sampling without duplicate offers.
    }
    static async Task<OwnedItem> Add(NpgsqlConnection c, Guid run, CatalogItem item, string origin)
    {
        var owned = new OwnedItem(Guid.NewGuid(), item.Id, item.Kind, origin == "starter" ? 0 : item.SellPrice, origin);
        await Database.Exec(c, "INSERT INTO run_items(id,run_id,content_id,kind,sell_price,origin) VALUES($1,$2,$3,$4,$5,$6)", owned.InstanceId, run, owned.ContentId, owned.Kind, owned.SellPrice, origin);
        return owned;
    }
    static async Task<EconomyView> Load(NpgsqlConnection c, Guid run)
    {
        long revision, gold;
        await using (var cmd = Database.Command(c, "SELECT revision,gold FROM run_economy WHERE run_id=$1", run))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
                throw new ApiFailure(409, "economy_version", "이 원정은 이전 콘텐츠 버전입니다. 정산 후 새로 출정하세요.");
            revision = reader.GetInt64(0);
            gold = reader.GetInt64(1);
        }
        var items = new List<OwnedItem>();
        await using (var cmd = Database.Command(c, "SELECT id,content_id,kind,sell_price,origin FROM run_items WHERE run_id=$1 AND NOT removed ORDER BY created_utc,id", run))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4)));
        var shops = new List<ShopView>();
        await using (var cmd = Database.Command(c, "SELECT shop_id,stage_index,barter_used FROM run_shops WHERE run_id=$1 ORDER BY stage_index", run))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                shops.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetBoolean(2), []));
        for (var i = 0; i < shops.Count; i++)
        {
            var offers = new List<ShopOffer>();
            await using var cmd = Database.Command(c, "SELECT offer_id,content_id,kind,price,stock FROM run_shop_offers WHERE run_id=$1 AND shop_id=$2 ORDER BY offer_id", run, shops[i].ShopId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                offers.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4)));
            shops[i] = shops[i] with { Offers = offers.ToArray() };
        }
        return new(run, revision, gold, items.ToArray(), shops.ToArray());
    }
    public async Task<EconomyView> Get(Guid player, Guid run)
    {
        await using var c = await db.Source.OpenConnectionAsync();
        // Read one consistent economy snapshot, including offers and inventory.
        await using var tx = await c.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await RunService.Load(c, player, run);
        var state = await Load(c, run);
        await tx.CommitAsync();
        return state;
    }
    static void Active(RunView run) => Wire.Require(run.Status == "active", "run_settled", "종료된 원정에는 거래할 수 없습니다.", 409);
    static CatalogItem Definition(Catalog catalog, string id) => catalog.Items?.SingleOrDefault(x => x.Id == id) ?? throw new ApiFailure(404, "content_missing", "아이템을 찾을 수 없습니다.");
    static EconomyRules Rules(Catalog catalog) => catalog.Economy ?? throw new ApiFailure(409, "economy_version", "지원하지 않는 원정 버전입니다.");
    static void Capacity(EconomyView state, EconomyRules rules, CatalogItem item, Guid[]? removed = null)
    {
        var retained = state.Items.Where(x => removed == null || !removed.Contains(x.InstanceId)).ToArray();
        Wire.Require(retained.Length < rules.MaxOwnedItems && (item.Kind != "accessory" || retained.Count(x => x.Kind == "accessory") < rules.MaxAccessories), "inventory_full", "가방의 빈칸을 확인하세요.", 409);
    }
    static OwnedItem Owned(EconomyView state, Guid? id) => state.Items.SingleOrDefault(x => x.InstanceId == id) ?? throw new ApiFailure(404, "item_not_owned", "소유한 아이템을 확인하세요.");
    static void Removable(EconomyView state, OwnedItem item, bool sells)
    {
        Wire.Require(!sells || item.Origin != "starter", "starter_not_for_sale", "시작 장비는 팔거나 교환할 수 없습니다.", 409);
        Wire.Require(item.Kind != "weapon" || state.Items.Count(x => x.Kind == "weapon") > 1, "last_weapon", "마지막 무기는 남겨 두세요.", 409);
    }
    static Task Remove(NpgsqlConnection c, Guid run, Guid id) => Database.Exec(c, "UPDATE run_items SET removed=true WHERE id=$1 AND run_id=$2 AND NOT removed", id, run);
    static async Task<EconomyReceipt> Finish(NpgsqlConnection c, Guid run, Guid op, EconomyView before, long delta, string reason, OwnedItem[] granted, Guid[] removed, int heal = 0, string? sourceEventId = null)
    {
        var gold = checked(before.Gold + delta);
        Wire.Require(gold >= 0, "insufficient_gold", "골드가 부족합니다.", 409);
        await Database.Exec(c, "UPDATE run_economy SET gold=$1,revision=revision+1,updated_utc=now() WHERE run_id=$2", gold, run);
        await Database.Exec(c, "INSERT INTO run_gold_ledger(run_id,operation_id,reason,delta,balance_after) VALUES($1,$2,$3,$4,$5)", run, op, reason, delta, gold);
        return new(await Load(c, run), granted, removed, delta, heal, op, sourceEventId);
    }
    public Task<string> Trade(Guid player, Guid id, TradeRequest request) => db.Mutate(player, request.OperationId, "run.trade", new { id, request }, async c =>
    {
        var run = await RunService.Load(c, player, id);
        Active(run);
        var catalog = await catalogs.Get(c, run.ContentVersion);
        var rules = Rules(catalog);
        var state = await Load(c, id);
        Wire.Require(state.Revision == request.ExpectedRevision, "revision_conflict", "거래 상태가 바뀌었습니다. 다시 확인하세요.", 409);
        var grants = new List<OwnedItem>();
        var removed = new List<Guid>();
        long delta = 0;
        var heal = 0;
        if (request.Action is "buy" or "sell" or "barter")
        {
            var shop = state.Shops.SingleOrDefault(x => x.ShopId == request.ShopId) ?? throw new ApiFailure(404, "shop_missing", "상점을 찾을 수 없습니다.");
            if (request.Action is "sell" or "barter")
            {
                var input = Owned(state, request.InstanceId);
                Removable(state, input, true);
                removed.Add(input.InstanceId);
                delta = input.SellPrice;
            }
            if (request.Action is "buy" or "barter")
            {
                var offer = shop.Offers.SingleOrDefault(x => x.OfferId == request.OfferId) ?? throw new ApiFailure(404, "offer_missing", "상품을 찾을 수 없습니다.");
                Wire.Require(offer.Stock > 0, "out_of_stock", "이미 판매된 상품입니다.", 409);
                if (request.Action == "barter")
                {
                    Wire.Require(run.Loadout.ActiveTalentIds.Contains("merchant_barter") && !shop.BarterUsed, "barter_unavailable", "이 상점에서는 교환할 수 없습니다.", 409);
                    Wire.Require(offer.Kind != "heal" && offer.Price <= delta * 2, "barter_value", "비슷한 가치의 장비와 교환하세요.", 409);
                    delta = 0;
                    await Database.Exec(c, "UPDATE run_shops SET barter_used=true WHERE run_id=$1 AND shop_id=$2", id, shop.ShopId);
                }
                else
                    delta = -offer.Price;
                Wire.Require(state.Gold + delta >= 0, "insufficient_gold", "골드가 부족합니다.", 409);
                if (offer.Kind == "heal")
                    heal = rules.HealAmount;
                else
                {
                    var item = Definition(catalog, offer.ContentId);
                    Capacity(state, rules, item, removed.ToArray());
                    grants.Add(await Add(c, id, item, "shop"));
                }
                await Database.Exec(c, "UPDATE run_shop_offers SET stock=stock-1 WHERE run_id=$1 AND shop_id=$2 AND offer_id=$3", id, shop.ShopId, offer.OfferId);
            }
        }
        else if (request.Action == "discard")
        {
            var item = Owned(state, request.InstanceId);
            Removable(state, item, false);
            removed.Add(item.InstanceId);
        }
        else if (request.Action == "exchange")
        {
            var recipe = catalog.ExchangeRecipes?.SingleOrDefault(x => x.Id == request.RecipeId) ?? throw new ApiFailure(404, "recipe_missing", "교환식을 찾을 수 없습니다.");
            var ids = request.InstanceIds ?? [];
            Wire.Require(ids.Length == recipe.InputIds.Length && ids.Distinct().Count() == ids.Length, "recipe_inputs", "교환 재료를 확인하세요.");
            var inputs = ids.Select(x => Owned(state, x)).ToArray();
            Wire.Require(inputs.Select(x => x.ContentId).Order().SequenceEqual(recipe.InputIds.Order()), "recipe_inputs", "교환 재료가 다릅니다.");
            Wire.Require(!string.IsNullOrWhiteSpace(request.ObjectId) && request.ObjectId.Length <= 128 && !request.ObjectId.Any(char.IsControl), "object_id", "제단 식별자를 확인하세요.");
            Wire.Require(await Database.Scalar(c, "SELECT 1 FROM run_used_objects WHERE run_id=$1 AND object_id=$2", id, request.ObjectId!) == null, "object_used", "이미 사용한 제단입니다.", 409);
            Wire.Require(inputs.All(x => x.Origin != "starter"), "starter_not_for_sale", "시작 장비는 교환 재료가 아닙니다.", 409);
            var output = Definition(catalog, recipe.OutputId);
            Wire.Require(output.Kind == "weapon" || state.Items.Count(x => x.Kind == "weapon" && !ids.Contains(x.InstanceId)) > 0, "last_weapon", "마지막 무기는 남겨 두세요.", 409);
            Capacity(state, rules, output, ids);
            removed.AddRange(ids);
            grants.Add(await Add(c, id, output, "exchange"));
            await Database.Exec(c, "INSERT INTO run_used_objects(run_id,object_id,kind) VALUES($1,$2,'exchange')", id, request.ObjectId!);
        }
        else
            throw new ApiFailure(400, "trade_action", "거래 종류를 확인하세요.");
        foreach (var item in removed)
            await Remove(c, id, item);
        return await Finish(c, id, request.OperationId, state, delta, "trade." + request.Action, grants.ToArray(), removed.ToArray(), heal);
    });
    public Task<string> Reward(Guid player, Guid id, RewardRequest request) => db.Mutate(player, request.OperationId, "run.reward", new { id, request }, async c =>
    {
        var run = await RunService.Load(c, player, id);
        Active(run);
        var catalog = await catalogs.Get(c, run.ContentVersion);
        var rules = Rules(catalog);
        var state = await Load(c, id);
        Wire.Require(!string.IsNullOrWhiteSpace(request.EventId) && request.EventId.Length <= 160 && !request.EventId.Any(char.IsControl), "event_id", "보상 식별자가 필요합니다.");
        Wire.Require(!string.IsNullOrWhiteSpace(request.RoomId) && request.RoomId.Length <= 96 && request.StageIndex >= 0 && request.StageIndex < catalog.Settlement.CampaignStages, "reward_location", "보상 지역을 확인하세요.");
        // A different operation UUID may never claim the same world event twice.
        Wire.Require(await Database.Scalar(c, "SELECT 1 FROM run_reward_events WHERE run_id=$1 AND event_id=$2", id, request.EventId) == null, "event_claimed", "이미 받은 보상입니다.", 409);
        var delta = request.Source switch { "enemy" => rules.EnemyGold, "combat" => rules.CombatGold, "chest" => rules.ChestGold, "boss" => rules.BossGold, "pickup" => 0, _ => throw new ApiFailure(400, "reward_source", "보상 종류를 확인하세요.") };
        var count = (long)(await Database.Scalar(c, "SELECT count(*) FROM run_reward_events WHERE run_id=$1", id) ?? 0L);
        Wire.Require(count < catalog.Settlement.MaxKills + catalog.Settlement.MaxRooms * 8, "reward_limit", "원정의 보상 수를 확인하세요.", 409);
        var grants = new List<OwnedItem>();
        if (!string.IsNullOrEmpty(request.ContentId))
        {
            Wire.Require(request.Source is "pickup" or "chest" or "boss", "reward_content", "이 보상은 아이템을 지급하지 않습니다.");
            var item = Definition(catalog, request.ContentId);
            Wire.Require(item.DropWeight > 0 && item.MinStage <= request.StageIndex && item.MaxStage >= request.StageIndex, "drop_unavailable", "이 지역의 보상 아이템이 아닙니다.");
            Capacity(state, rules, item);
            grants.Add(await Add(c, id, item, "reward." + request.Source));
        }
        else
            Wire.Require(request.Source != "pickup", "reward_content", "받을 아이템을 확인하세요.");
        var receipt = await Finish(c, id, request.OperationId, state, delta, "reward." + request.Source, grants.ToArray(), [], sourceEventId: request.EventId);
        await Database.Exec(c, "INSERT INTO run_reward_events(run_id,event_id,request_hash,operation_id,receipt) VALUES($1,$2,$3,$4,$5::jsonb)", id, request.EventId, Database.Hash(Wire.Encode(new { request.EventId, request.Source, request.StageIndex, request.RoomId, request.ContentId })), request.OperationId, Wire.Encode(receipt));
        return receipt;
    });
}
