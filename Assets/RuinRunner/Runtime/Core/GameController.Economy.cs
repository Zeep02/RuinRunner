using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RuinRunner.Backend;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameController
    {
        readonly HashSet<string> pendingLoot = new HashSet<string>();
        readonly Dictionary<string, FieldLootPickup> lootByEvent = new Dictionary<string, FieldLootPickup>();
        readonly Dictionary<string, string> serviceByOperation = new Dictionary<string, string>();
        readonly HashSet<string> processedEconomyReceipts = new HashSet<string>();
        int rewardSequence;
        public long RunGold => BackendEconomy?.gold ?? 0;
        public bool EconomyOperationPending { get; private set; }
        public long ServiceSelectedEquipment { get; set; }
        public ShopView CurrentShop
        {
            get
            {
                if (OpenService?.Kind != FieldServiceKind.Merchant || BackendEconomy?.shops == null)
                    return null;
                foreach (var s in BackendEconomy.shops)
                    if (s.stageIndex == CurrentStageIndex)
                        return s;
                return null;
            }
        }
        async void BeginServerSettlement(bool victory)
        {
            int epoch = sessionEpoch;
            string runId = BackendRun?.runId;
            settlementRunId = runId;
            SessionMessage = "";
            try
            {
                await SettleBackendRunAsync(victory ? "victory" : "defeat", Stats.EnemiesDefeated, priorStageVisitedRegions + (DungeonMap?.VisitedRegionCount ?? 0), StagesCleared);
                if (SessionMatches(epoch, runId: runId))
                    RefreshSettlementProjection();
            }
            catch (Exception e)
            {
                if (SessionMatches(epoch, runId: runId))
                {
                    Debug.LogException(e);
                    SessionMessage = "";
                }
            }
        }
        async void AwardEnemyGold(EnemyController enemy)
        {
            if (legacyFixture || !IsExpedition)
                return;
            DropGold(enemy.transform.position, enemy.IsBoss ? BackendCatalog?.economy?.bossGold ?? 0 : BackendCatalog?.economy?.enemyGold ?? 0);
            var id = "enemy:" + CurrentStageIndex + ":" + CurrentRoom + ":" + CombatTargetIdentity.For(enemy);
            await ClaimGold(id, enemy.IsBoss ? "boss" : "enemy");
        }
        // The server has already counted it; the coins are how the player sees it arrive.
        public long DisplayedGold => Math.Max(0, RunGold - GoldCoin.InFlight);
        void DropGold(Vector2 at, int amount)
        {
            if (legacyFixture || Player == null)
                return;
            GoldCoin.Burst(at, amount, Player.transform, () => EnemiesAlive == 0 && !waveTransition && currentWaveIndex + 1 >= roomWaveCount);
        }
        async Task ClaimGold(string eventId, string source)
        {
            if (legacyFixture || BackendRun == null)
                return;
            try
            {
                await ClaimBackendRewardAsync(new RewardRequest { operationId = Guid.NewGuid().ToString(), eventId = eventId, source = source, stageIndex = CurrentStageIndex, roomId = CurrentRegionId });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        const float PotionHealing = .25f, ChestPotionChance = .3f, EnemyPotionChance = .04f;
        void DropHealthPotion(Vector2 at, int scatter = 0)
        {
            if (legacyFixture || Player == null)
                return;
            FieldLootPickup.Create(at, "회복 포션", new Color(.92f, .2f, .26f), Player, Settings, () =>
            {
                Player.Stats.Heal(Player.MaxHealth * PotionHealing);
                SigFx.Play("FxPotionHeal", Player.FeetPoint, 0, -1, true, false, .5f);
                GameAudio.Play(GameSound.Upgrade, AudioChannel.Interface, .4f);
                return true;
            }, scatter, IndividualArt.Sprite("CombatFX/PickupPotion/00"));
        }
        public string CurrentRegionId => DungeonMap?.Graph?.Regions[CurrentRoom].Id ?? ("region-" + CurrentRoom);
        void ApplyEconomyReceipt(EconomyReceipt receipt)
        {
            if (sessionDisposed || Player == null || receipt == null || receipt.economy == null || BackendRun == null || receipt.economy.runId != BackendRun.runId || !IsExpedition && sessionPhase != GameSessionPhase.Result && sessionPhase != GameSessionPhase.Transition)
                return;
            // A repeated transport receipt is a projection, never a second item or heal.
            var id = receipt.economy.runId + ":" + receipt.economy.revision;
            if (processedEconomyReceipts.Contains(id))
                return;
            if (receipt.removedInstanceIds != null)
                foreach (var instance in receipt.removedInstanceIds)
                {
                    var item = Inventory.FindServerItem(instance);
                    if (item != null)
                        Inventory.TryRemoveConfirmed(item.Identity);
                }
            if (receipt.grantedItems != null)
                foreach (var item in receipt.grantedItems)
                {
                    var kind = item.kind == "weapon" ? InventoryEntryKind.Weapon : InventoryEntryKind.Accessory;
                    if (!Inventory.TryAddConfirmed(item.contentId, kind, item.instanceId, item.origin == "starter"))
                        throw new InvalidOperationException("Confirmed item cannot fit local inventory: " + item.instanceId);
                }
            // Gold receipts and repeated item projections must not cancel an attack,
            // recall a placed weapon, or count as another weapon swap.
            if (Player.EquippedWeapon != Inventory.EquippedWeapon)
                Player.EquipWeapon(Inventory.EquippedWeapon);
            if (receipt.grantedItems != null && receipt.grantedItems.Length > 0)
                CompletePickup("장비를 받았습니다.");
            if (!string.IsNullOrEmpty(receipt.operationId) && serviceByOperation.TryGetValue(receipt.operationId, out var serviceId))
            {
                usedServices.Add(serviceId);
                foreach (var service in RuntimeSceneObjects.FindAll<FieldServiceObject>())
                    if (service.StableId == serviceId)
                        service.Used = true;
                serviceByOperation.Remove(receipt.operationId);
            }
            FieldLootPickup collected = null;
            if (!string.IsNullOrEmpty(receipt.sourceEventId) && lootByEvent.TryGetValue(receipt.sourceEventId, out collected))
            {
                if (collected != null)
                    collected.RequiresStageDecision = false;
                lootByEvent.Remove(receipt.sourceEventId);
            }
            // Inventory and service changes above can be retried by server identity.
            // Commit the non-repeatable heal immediately before the local receipt marker.
            // Presentation failures below cannot apply that heal a second time.
            if (receipt.healAmount > 0)
                Player.Stats.Heal(receipt.healAmount);
            processedEconomyReceipts.Add(id);
            if (collected != null)
            {
                var center = collected.ItemVisualCenter;
                var rarity = collected.Rarity;
                int sortingOrder = 1003 - Mathf.RoundToInt(collected.transform.position.y * 100);
                Destroy(collected.gameObject);
                RewardPresentation.Collect(center, rarity, sortingOrder);
            }
        }
        async void InitializeRunEconomy()
        {
            if (legacyFixture)
                return;
            int epoch = sessionEpoch;
            string runId = BackendRun?.runId;
            if (!await RefreshBackendEconomyAsync())
                return;
            if (!SessionMatches(epoch, runId: runId) || !IsExpedition || BackendEconomy?.runId != runId)
                return;
            // Attach the already-created, free starting copy to its server identity.
            foreach (var item in BackendEconomy.items)
                if (item.origin == "starter" && Inventory.ActiveSlot.Item != null)
                {
                    Inventory.ActiveSlot.Item.ServerInstanceId = item.instanceId;
                    Inventory.ActiveSlot.Item.IsStartingEquipment = true;
                    break;
                }
        }
        public async void BuyShopOffer(string offerId)
        {
            var shop = CurrentShop;
            if (shop == null || EconomyOperationPending || BackendHasPending || EnemiesAlive > 0)
                return;
            var offer = Array.Find(shop.offers, o => o.offerId == offerId);
            if (offer == null || offer.stock <= 0)
                return;
            var kind = offer.kind == "weapon" ? InventoryEntryKind.Weapon : InventoryEntryKind.Accessory;
            bool capacity = Inventory.HasRoomFor(kind);
            if (offer.kind != "heal" && !capacity)
            {
                SessionMessage = "가방을 먼저 비워 주세요.";
                return;
            }
            if (RunGold < offer.price)
            {
                SessionMessage = "골드가 부족합니다.";
                return;
            }
            EconomyOperationPending = true;
            try
            {
                var receipt = await TradeBackendAsync(new TradeRequest { operationId = Guid.NewGuid().ToString(), action = "buy", shopId = shop.shopId, offerId = offerId });
                if (receipt != null)
                {
                    ApplyEconomyReceipt(receipt);
                    SessionMessage = "구입했습니다.";
                }
            }
            finally
            {
                EconomyOperationPending = false;
            }
        }
        public async void SellShopItem(long identity)
        {
            var shop = CurrentShop;
            var item = Inventory.FindOwned(identity);
            if (shop == null || item == null || item.IsStartingEquipment || !Inventory.CanDisassemble(identity) || EconomyOperationPending || BackendHasPending)
                return;
            EconomyOperationPending = true;
            try
            {
                var receipt = await TradeBackendAsync(new TradeRequest { operationId = Guid.NewGuid().ToString(), action = "sell", shopId = shop.shopId, instanceId = item.ServerInstanceId });
                if (receipt != null)
                {
                    ApplyEconomyReceipt(receipt);
                    SessionMessage = "판매했습니다.";
                }
            }
            finally
            {
                EconomyOperationPending = false;
            }
        }
        public int SellPrice(OwnedEquipment item)
        {
            if (item == null || item.IsStartingEquipment || BackendEconomy?.items == null)
                return 0;
            foreach (var server in BackendEconomy.items)
                if (server.instanceId == item.ServerInstanceId)
                    return server.sellPrice;
            return 0;
        }
        async void DiscardConfirmed(OwnedEquipment item)
        {
            if (EconomyOperationPending || BackendHasPending || item == null)
                return;
            if (legacyFixture || IsTown)
            {
                Inventory.TryRemoveConfirmed(item.Identity);
                return;
            }
            EconomyOperationPending = true;
            try
            {
                var receipt = await TradeBackendAsync(new TradeRequest { operationId = Guid.NewGuid().ToString(), action = "discard", instanceId = item.ServerInstanceId });
                if (receipt != null)
                    ApplyEconomyReceipt(receipt);
            }
            finally
            {
                EconomyOperationPending = false;
            }
        }
        FieldLootPickup SpawnEquipmentPickup(Vector2 origin, WeaponDefinition weapon, AccessoryDefinition accessory, int scatter, bool boss = false, string eventId = null)
        {
            var kind = weapon != null ? InventoryEntryKind.Weapon : InventoryEntryKind.Accessory;
            var content = weapon != null ? weapon.Id : accessory.Id;
            var name = weapon != null ? weapon.Name : accessory.Name;
            var color = weapon != null ? weapon.Color : accessory.Color;
            var rarity = weapon != null ? weapon.Rarity : accessory.Rarity;
            string id = eventId ?? ("pickup:" + CurrentStageIndex + ":" + CurrentRoom + ":" + (rewardSequence++));
            FieldLootPickup pickup = null;
            pickup = FieldLootPickup.Create(origin, name, color, Player, Settings, () =>
            {
                if (legacyFixture)
                    return weapon != null ? CollectWeaponPickup(weapon) : CollectAccessoryPickup(accessory);
                if (!Inventory.HasRoomFor(kind))
                {
                    LootMessage = "가방이 가득 찼습니다.";
                    LootMessageUntil = Time.unscaledTime + 2;
                    return false;
                }
                ClaimPickup(id, content, kind, pickup);
                return false;
            }, scatter, InventoryIconFactory.GetWorldSprite(content, color, weapon != null ? InventoryIconKind.Weapon : InventoryIconKind.Accessory), rarity);
            lootByEvent[id] = pickup;
            pickup.InventoryKind = kind;
            pickup.RequiresStageDecision = boss;
            return pickup;
        }
        async void ClaimPickup(string eventId, string contentId, InventoryEntryKind kind, FieldLootPickup pickup)
        {
            if (EconomyOperationPending || BackendHasPending || !pendingLoot.Add(eventId))
                return;
            EconomyOperationPending = true;
            try
            {
                var receipt = await ClaimBackendRewardAsync(new RewardRequest { operationId = Guid.NewGuid().ToString(), eventId = eventId, source = "pickup", contentId = contentId, stageIndex = CurrentStageIndex, roomId = CurrentRegionId });
                if (receipt != null)
                {
                    ApplyEconomyReceipt(receipt);
                    if (pickup != null)
                        Destroy(pickup.gameObject);
                    CompletePickup(kind == InventoryEntryKind.Weapon ? WeaponLootTable.Find(contentId).Name : AccessoryLootTable.Find(contentId).Name);
                }
            }
            finally
            {
                pendingLoot.Remove(eventId);
                EconomyOperationPending = false;
            }
        }
    }
}
