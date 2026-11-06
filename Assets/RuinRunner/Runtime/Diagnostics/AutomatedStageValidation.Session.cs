using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class AutomatedStageValidation
    {
        bool backendJourney, preferMobility, journeyWon;
        int journeyStep, purchases, sales, mutations, oaths, exchanges, slotEntryAttempts;
        int purchasesConfirmed, salesConfirmed, exchangesConfirmed, pendingStock;
        long pendingRevision;
        string pendingFacilityAction, pendingOffer, pendingInstance;
        float journeyActionAt, trainingUntil;
        string activeFacility;
        int facilityStep;
        readonly HashSet<string> visitedFacilities = new HashSet<string>();
        readonly Dictionary<string, string> deferredFacilities = new Dictionary<string, string>();
        readonly HashSet<long> awakenedCopies = new HashSet<long>();
        readonly HashSet<long> awakenedCopiesUsed = new HashSet<long>();
        readonly List<string> journeyEvents = new List<string>();

        void JourneyEvent(string value)
        {
            journeyEvents.Add(value);
            Debug.Log("[Server Journey] " + value);
            if (captureScreenshots && !string.IsNullOrEmpty(captureDirectory))
                ScreenCapture.CaptureScreenshot(Path.Combine(captureDirectory, "journey-" + journeyEvents.Count.ToString("00") + ".png"));
        }
        void AdvanceJourney(int step, string value = null)
        {
            StageValidationInput.ReleaseAll();
            attackHeld = false;
            journeyStep = step;
            journeyActionAt = Time.unscaledTime + MenuPause(.25f);
            if (value != null)
                JourneyEvent(value);
        }
        // Menu/service actions call the exact public button handlers. Movement, combat,
        // interaction, pickup and inventory use GameAction input. No state is supplied.
        // Reports retain this UI-input limitation instead of calling it manual play.
        bool DriveSessionJourney()
        {
            if (journeyStep == 9 && !game.IsGameOver)
            {
                if (game.IsExpedition && game.Player != null && game.DungeonMap != null)
                    return false;
                StageValidationInput.ReleaseAll();
                if (game.IsStageTransitioning || game.Phase == GameSessionPhase.Transition)
                    return true;
                if (!game.SessionOperationPending && !game.BackendBusy && Time.unscaledTime > journeyActionAt + 2f)
                    Finish(false, "departure did not reach expedition: " + game.BackendStatus);
                return true;
            }
            currentIntent = "server journey step " + journeyStep;
            if (game.SessionOperationPending || game.BackendBusy || game.EconomyOperationPending || Time.unscaledTime < journeyActionAt)
            {
                StageValidationInput.ReleaseAll();
                return true;
            }
            if (game.BackendHasPending)
            {
                game.RetrySessionConnection();
                journeyActionAt = Time.unscaledTime + 3f;
                return true;
            }
            switch (journeyStep)
            {
                case 0:
                    if (!game.SaveSlots.IsValidationProfile)
                    {
                        Finish(false, "isolated profile required");
                        return true;
                    }
                    game.OpenSaveSlots();
                    AdvanceJourney(1, "main menu → profile list");
                    break;
                case 1:
                    if (!game.BackendReady)
                    {
                        game.RetrySessionConnection();
                        journeyActionAt = Time.unscaledTime + 3f;
                        break;
                    }
                    slotEntryAttempts = 1;
                    game.StartSlot(1);
                    AdvanceJourney(2);
                    break;
                case 2:
                    if (!game.IsTown)
                    {
                        // A button handler can reject a click while connection
                        // completion is handing off. Repeat the same ordinary
                        // button only after all session work is idle; never
                        // enter Town or provide a profile state directly.
                        if (Time.unscaledTime > journeyActionAt + 2f)
                        {
                            if (slotEntryAttempts >= 3)
                                Finish(false, "profile did not enter town after ordinary slot-button retries: " + game.BackendStatus);
                            else
                            {
                                slotEntryAttempts++;
                                game.StartSlot(1);
                                journeyActionAt = Time.unscaledTime + .25f;
                                JourneyEvent("idle slot-button retry " + slotEntryAttempts + "; phase=" + game.Phase + "; status=" + game.BackendStatus);
                            }
                        }
                        break;
                    }
                    if (game.BackendProfile.state.totalRuns != 0)
                    {
                        Finish(false, "new profile already has run history");
                        break;
                    }
                    AdvanceJourney(3, "fresh server profile enters town");
                    break;
                case 3:
                    if (WalkToService(FieldServiceKind.StartingGear))
                        AdvanceJourney(4, "walked to equipment NPC and interacted");
                    break;
                case 4:
                    // A fresh mobility build spends its normal starting legacy
                    // on RazorWind; dagger/katana copies are earned in the run.
                    string weapon = preferSkills || preferRanged ? "scrap_revolver" : "trainee_blade";
                    if (Array.IndexOf(game.SaveSlots.ActiveData.unlockedWeaponIds, weapon) < 0)
                    {
                        game.UnlockTownChoice("weapon", weapon);
                        journeyActionAt = Time.unscaledTime + MenuPause(.5f);
                        break;
                    }
                    if (game.SaveSlots.ActiveData.startingWeaponId != weapon)
                    {
                        game.ChooseStartingWeapon(weapon);
                        journeyActionAt = Time.unscaledTime + MenuPause(.5f);
                        break;
                    }
                    game.CloseService();
                    trainingUntil = Time.unscaledTime + (demoRun ? 1.5f : 5f);
                    AdvanceJourney(5, "starting weapon selected with normal legacy budget: " + weapon);
                    break;
                case 5:
                    var delta = TownWorld.TrainingPosition - (Vector2)game.Player.transform.position;
                    SetMove(delta.magnitude > 1.7f ? delta.normalized : Vector2.zero);
                    StageValidationInput.SetAim(delta);
                    if (delta.magnitude <= 2f && Time.unscaledTime >= nextAttackAt)
                    {
                        TapAttack(.1f);
                        nextAttackAt = Time.unscaledTime + .4f;
                    }
                    if (Time.unscaledTime >= trainingUntil)
                        AdvanceJourney(6, "training area approached and normal attack input used");
                    break;
                case 6:
                    if (WalkToService(FieldServiceKind.Talents))
                        AdvanceJourney(7, "walked to talent NPC and interacted");
                    break;
                case 7:
                    if (preferMobility && !game.ActiveTalents.Has(TalentId.RazorWind))
                    {
                        if (Array.IndexOf(game.SaveSlots.ActiveData.unlockedTalentIds, "razor_wind") < 0)
                        {
                            var definition = Array.Find(game.BackendCatalog.talents, t => t.id == "razor_wind");
                            if (definition == null || definition.unlockCost > game.BackendProfile.legacyShards)
                            {
                                JourneyEvent("razor_wind unlock deferred; starting weapon spent the normal legacy budget");
                                game.CloseService();
                                AdvanceJourney(8, "talent loadout checked: " + string.Join(",", game.ActiveTalents.Ids));
                                break;
                            }
                            game.UnlockTownChoice("talent", "razor_wind");
                        }
                        else if (game.ActiveTalents.Ids.Length >= TalentLoadout.MaximumActive)
                            game.ToggleTalent("ghost_mine");
                        else
                            game.ToggleTalent("razor_wind");
                        journeyActionAt = Time.unscaledTime + MenuPause(.5f);
                        break;
                    }
                    game.CloseService();
                    AdvanceJourney(8, "talent loadout checked: " + string.Join(",", game.ActiveTalents.Ids));
                    break;
                case 8:
                    if (WalkToService(FieldServiceKind.Expedition))
                    {
                        game.DepartTown();
                        AdvanceJourney(9, "walked to departure gate; server-created expedition requested");
                    }
                    break;
                case 9:
                    if (!game.IsVictory && game.Player != null && game.Player.DeathProgress < 1f)
                        break;
                    if (!game.ResultSettlementConfirmed)
                    {
                        game.RetrySessionConnection();
                        journeyActionAt = Time.unscaledTime + 2f;
                        break;
                    }
                    if (HoldAdversityDefeatResult() || HoldDemoResult())
                        break;
                    journeyWon = game.IsVictory && game.StagesCleared == CampaignStage.Count;
                    JourneyEvent((journeyWon ? "three guardians and ending" : "normal defeat") + "; server settlement confirmed, legacy=" + game.BackendProfile.legacyShards);
                    game.ReturnToTown();
                    AdvanceJourney(10);
                    break;
                case 10:
                    if (!game.IsTown)
                    {
                        Finish(false, "settled result failed to return to town");
                        break;
                    }
                    JourneyEvent("returned to town with totalRuns=" + game.BackendProfile.state.totalRuns);
                    if (demoRun)
                    {
                        Finish(journeyWon, "presentation run: invulnerable, not evidence of balance");
                        break;
                    }
                    if (ContinueAdversityAfterTown())
                        break;
                    AdvanceJourney(11);
                    break;
                case 11:
                    if (WalkToService(FieldServiceKind.Expedition))
                    {
                        game.DepartTown();
                        AdvanceJourney(12, "re-departure through same gate");
                    }
                    break;
                case 12:
                    if (!game.IsExpedition || game.Player == null || game.BackendRun?.status != "active")
                        break;
                    game.ReturnToMainMenu();
                    AdvanceJourney(13, "interrupted second expedition by returning to menu");
                    break;
                case 13:
                    game.OpenSaveSlots();
                    game.StartSlot(1);
                    AdvanceJourney(14);
                    break;
                case 14:
                    if (!game.IsTown || !game.BackendHasUnfinishedRun)
                    {
                        Finish(false, "interrupted expedition was not exposed in town");
                        break;
                    }
                    if (WalkToService(FieldServiceKind.Expedition))
                    {
                        game.ResolveInterruptedExpedition();
                        AdvanceJourney(15);
                    }
                    break;
                case 15:
                    if (game.BackendHasUnfinishedRun)
                    {
                        Finish(false, "interrupted expedition failed to settle");
                        break;
                    }
                    JourneyEvent("interrupted expedition explicitly cleaned up; next departure available");
                    game.CloseService();
                    Finish(journeyWon, journeyWon ? "server campaign → confirmed settlement → town → retry → interrupted cleanup" : "normal defeat and retry verified; campaign completion still required");
                    break;
            }
            return true;
        }

        bool WalkToService(FieldServiceKind kind)
        {
            if (game.OpenService?.Kind == kind)
            {
                ReleaseMovement();
                return true;
            }
            FieldServiceObject target = null;
            foreach (var service in RuntimeSceneObjects.FindAll<FieldServiceObject>())
                if (service.Kind == kind && (game.IsTown || service.RegionIndex == game.CurrentRoom))
                {
                    target = service;
                    break;
                }
            if (target == null)
            {
                Finish(false, "missing accessible service " + kind);
                return false;
            }
            Vector2 delta = (Vector2)target.transform.position - (Vector2)game.Player.transform.position;
            currentIntent = "walk to " + target.Kind;
            if (delta.magnitude > 1.4f)
            {
                if (game.IsTown)
                    SetMove(delta.normalized);
                else
                    MoveTo(target.transform.position, true);
            }
            else
            {
                ReleaseMovement();
                Tap(GameAction.Interact);
            }
            return false;
        }

        bool DriveFieldFacility()
        {
            if (game.IsTown || game.DungeonMap == null || game.EnemiesAlive > 0 || game.IsEncounterTransitioning)
                return false;
            FieldServiceObject service = game.OpenService;
            if (service == null)
                foreach (var candidate in RuntimeSceneObjects.FindAll<FieldServiceObject>())
                    if (candidate.RegionIndex == game.CurrentRoom && !visitedFacilities.Contains(candidate.StableId) &&
                        (!deferredFacilities.TryGetValue(candidate.StableId, out var state) || state != FacilityInventoryState()))
                    {
                        service = candidate;
                        break;
                    }
            if (service == null && !demoRun)
                foreach (var candidate in RuntimeSceneObjects.FindAll<FieldServiceObject>())
                    if (game.DungeonMap.Regions[candidate.RegionIndex].Visited && !visitedFacilities.Contains(candidate.StableId) &&
                        deferredFacilities.TryGetValue(candidate.StableId, out var state) && state != FacilityInventoryState() && CanUseDeferredFacility(candidate))
                    {
                        service = candidate;
                        break;
                    }
            if (service == null)
                return false;
            if (service.RegionIndex != game.CurrentRoom)
            {
                currentIntent = "return to ready facility " + service.Kind;
                MoveTo(service.transform.position, true);
                return true;
            }
            if (activeFacility != service.StableId)
            {
                activeFacility = service.StableId;
                facilityStep = 0;
            }
            if (game.OpenService == null)
            {
                WalkToService(service.Kind);
                return true;
            }
            StageValidationInput.ReleaseAll();
            attackHeld = false;
            if (game.BackendBusy || game.EconomyOperationPending || game.BackendHasPending || Time.unscaledTime < journeyActionAt)
                return true;
            if (demoRun && demoFacilityShown != service.StableId)
            {
                demoFacilityShown = service.StableId;
                journeyActionAt = Time.unscaledTime + DemoMenuSeconds;
                return true;
            }
            ConfirmFacilityAction(service);
            if (service.Used)
            {
                EndFacility(service, "already used");
                return true;
            }
            switch (service.Kind)
            {
                case FieldServiceKind.Merchant:
                    if (facilityStep == 0)
                    {
                        facilityStep = 1;
                        RuinRunner.Backend.ShopOffer selected = null;
                        foreach (var offer in game.CurrentShop.offers)
                        {
                            if (offer.stock <= 0 || offer.price > game.RunGold)
                                continue;
                            if (offer.kind == "heal" && game.Player.Health > game.Player.MaxHealth * .7f)
                                continue;
                            if (offer.kind != "heal" && !game.Inventory.HasRoomFor(offer.kind == "weapon" ? InventoryEntryKind.Weapon : InventoryEntryKind.Accessory))
                                continue;
                            if (selected == null || ShopOfferRank(offer) > ShopOfferRank(selected))
                                selected = offer;
                        }
                        if (selected != null)
                        {
                            pendingFacilityAction = "buy";
                            pendingOffer = selected.offerId;
                            pendingStock = selected.stock;
                            pendingRevision = game.BackendEconomy.revision;
                            game.BuyShopOffer(selected.offerId);
                            purchases++;
                            JourneyEvent("merchant purchase requested: " + selected.contentId + " price=" + selected.price);
                            journeyActionAt = Time.unscaledTime + MenuPause(.5f);
                            return true;
                        }
                    }
                    if (facilityStep == 1)
                    {
                        facilityStep = 2;
                        if (adversityScenario && adversityRetried)
                            break;
                        foreach (var item in game.Inventory.OwnedWeapons)
                            if (!item.IsStartingEquipment && item.Oath == EquipmentOath.None && game.Inventory.CanDisassemble(item.Identity))
                            {
                                pendingFacilityAction = "sell";
                                pendingInstance = item.ServerInstanceId;
                                pendingRevision = game.BackendEconomy.revision;
                                game.SellShopItem(item.Identity);
                                sales++;
                                JourneyEvent("merchant sale requested: " + item.Name);
                                journeyActionAt = Time.unscaledTime + MenuPause(.5f);
                                return true;
                            }
                    }
                    EndFacility(service, "merchant decisions finished");
                    break;
                case FieldServiceKind.ShapeAnvil:
                    foreach (var weapon in PreferredOwnedWeapons())
                    {
                        var mutation = weapon.Definition.AttackType == WeaponAttackType.Melee ? EquipmentMutation.LongFinisher : EquipmentMutation.Ricochet;
                        if (!EquipmentMutationRules.Compatible(weapon, mutation))
                            continue;
                        game.ServiceSelectedEquipment = weapon.Identity;
                        game.ApplyFieldMutation(mutation);
                        if (weapon.Mutation == mutation)
                        {
                            mutations++;
                            JourneyEvent("normal found weapon transformed: " + weapon.Name + " " + mutation);
                        }
                        break;
                    }
                    EndFacility(service, service.Used ? "anvil applied" : "anvil deferred until compatible equipment", service.Used);
                    break;
                case FieldServiceKind.OathStone:
                    foreach (var weapon in PreferredOwnedWeapons())
                    {
                        if (!FitsJourneyBuild(weapon.Definition))
                            continue;
                        var oath = weapon.Definition.AttackType == WeaponAttackType.Melee ? EquipmentOath.FinisherHunt : EquipmentOath.SteadyVolley;
                        if (!game.CanBeginOath(weapon, oath))
                            continue;
                        game.ServiceSelectedEquipment = weapon.Identity;
                        game.AcceptEquipmentOath(oath);
                        if (weapon.Oath == oath)
                        {
                            oaths++;
                            JourneyEvent("oath accepted, completion still required: " + oath);
                        }
                        break;
                    }
                    EndFacility(service, service.Used ? "oath accepted" : "oath deferred until a feasible choice", service.Used);
                    break;
                case FieldServiceKind.ExchangeAltar:
                    if (adversityScenario && adversityRetried)
                    {
                        EndFacility(service, "keeping naturally found ingredients for full-bag journey");
                        break;
                    }
                    if (facilityStep == 0)
                    {
                        facilityStep = 1;
                        foreach (var recipe in game.ExchangeRecipes)
                            if (game.ExchangeIngredients(recipe.Id).Length == 2)
                            {
                                pendingFacilityAction = "exchange";
                                pendingRevision = game.BackendEconomy.revision;
                                game.PreviewExchange(recipe.Id);
                                game.ConfirmExchange();
                                exchanges++;
                                JourneyEvent("exchange requested using naturally owned ingredients: " + recipe.Id);
                                journeyActionAt = Time.unscaledTime + .5f;
                                return true;
                            }
                    }
                    EndFacility(service, service.Used ? "exchange confirmed" : "exchange deferred until ingredients arrive", service.Used);
                    break;
                default:
                    EndFacility(service, "facility inspected");
                    break;
            }
            return true;
        }
        IEnumerable<OwnedWeapon> PreferredOwnedWeapons()
        {
            var others = new List<OwnedWeapon>();
            foreach (var item in game.Inventory.AllWeapons())
                others.Add(item);
            others.Sort((a, b) => OwnedWeaponRank(b).CompareTo(OwnedWeaponRank(a)));
            foreach (var item in others)
                yield return item;
        }
        float ShopOfferRank(RuinRunner.Backend.ShopOffer offer)
        {
            if (offer.kind == "heal")
                return game.Player.Health < game.Player.MaxHealth * .4f ? 10000 : 20;
            if (offer.kind == "accessory")
                return 100 + AccessoryRank(AccessoryLootTable.Find(offer.contentId));
            var weapon = WeaponLootTable.Find(offer.contentId);
            return weapon == null ? 0 : WeaponRank(weapon) / 10f;
        }
        bool CanUseDeferredFacility(FieldServiceObject service)
        {
            if (service.Used)
                return false;
            if (service.Kind == FieldServiceKind.ExchangeAltar)
            {
                foreach (var recipe in game.ExchangeRecipes)
                    if (game.ExchangeIngredients(recipe.Id).Length == 2)
                        return true;
            }
            if (service.Kind == FieldServiceKind.ShapeAnvil)
            {
                foreach (var item in game.Inventory.AllWeapons())
                    if (item.Mutation == EquipmentMutation.None)
                        return true;
            }
            if (service.Kind == FieldServiceKind.OathStone)
            {
                foreach (var item in game.Inventory.AllWeapons())
                    if (game.CanBeginOath(item, item.Definition.AttackType == WeaponAttackType.Melee ? EquipmentOath.FinisherHunt : EquipmentOath.SteadyVolley))
                        return true;
            }
            return false;
        }
        string FacilityInventoryState() => game.Inventory.Revision + ":" + game.RunGold;
        void ConfirmFacilityAction(FieldServiceObject service)
        {
            if (pendingFacilityAction == null)
                return;
            bool confirmed = game.BackendEconomy.revision > pendingRevision;
            if (pendingFacilityAction == "buy")
            {
                var offer = game.CurrentShop == null ? null : Array.Find(game.CurrentShop.offers, x => x.offerId == pendingOffer);
                confirmed &= offer != null && offer.stock < pendingStock;
                if (confirmed)
                    purchasesConfirmed++;
            }
            else if (pendingFacilityAction == "sell")
            {
                confirmed &= game.Inventory.FindServerItem(pendingInstance) == null;
                if (confirmed)
                    salesConfirmed++;
            }
            else
            {
                confirmed &= service.Used;
                if (confirmed)
                    exchangesConfirmed++;
            }
            JourneyEvent(pendingFacilityAction + (confirmed ? " confirmed by server and local projection" : " request did not produce the expected confirmed change"));
            pendingFacilityAction = null;
            pendingInstance = null;
            pendingOffer = null;
        }
        void EndFacility(FieldServiceObject service, string outcome, bool complete = true)
        {
            if (complete)
                visitedFacilities.Add(service.StableId);
            else
                deferredFacilities[service.StableId] = FacilityInventoryState();
            activeFacility = null;
            JourneyEvent(service.Kind + ": " + outcome);
            game.CloseService();
            journeyActionAt = Time.unscaledTime + .25f;
        }
        void ObserveAwakenedEquipment()
        {
            if (!backendJourney || game.Inventory == null)
                return;
            foreach (var weapon in game.Inventory.AllWeapons())
                if (weapon.Awakened && awakenedCopies.Add(weapon.Identity))
                    JourneyEvent("oath awakened through combat input: " + weapon.Name + " " + weapon.Oath);
            var active = game.Inventory.ActiveSlot.Item;
            if (active != null && active.Awakened && game.Player.MotionState == PlayerMotionState.Attack && awakenedCopiesUsed.Add(active.Identity))
                JourneyEvent("awakened copy used in an actual attack: " + active.Name);
        }
        [Serializable]
        sealed class JourneyEvidence
        {
            public bool serverJourneyPassed, manualUiInput = false, cheatsOrSuppliedState = false, screenshotsEnabled;
            public string reason, build, profileId, runId;
            public int purchasesRequested, salesRequested, purchasesConfirmed, salesConfirmed, mutationsApplied, oathsAccepted, awakenedCopies, awakenedCopiesUsed, exchangesRequested, exchangesConfirmed;
            public string[] events;
        }
        void WriteJourneyEvidence(bool passed, string reason)
        {
            if (string.IsNullOrEmpty(captureDirectory))
                return;
            var evidence = new JourneyEvidence
            {
                serverJourneyPassed = passed,
                cheatsOrSuppliedState = demoRun,
                screenshotsEnabled = captureScreenshots,
                reason = reason,
                build = preferMobility ? "mobility" : preferSkills ? "skill" : preferRanged ? "ranged" : "melee",
                profileId = game.BackendProfile?.profileId,
                runId = game.BackendRun?.runId,
                purchasesRequested = purchases,
                salesRequested = sales,
                mutationsApplied = mutations,
                oathsAccepted = oaths,
                awakenedCopies = awakenedCopies.Count,
                awakenedCopiesUsed = awakenedCopiesUsed.Count,
                exchangesRequested = exchanges,
                purchasesConfirmed = purchasesConfirmed,
                salesConfirmed = salesConfirmed,
                exchangesConfirmed = exchangesConfirmed,
                events = journeyEvents.ToArray()
            };
            File.WriteAllText(Path.Combine(captureDirectory, "server-journey.json"), JsonUtility.ToJson(evidence, true), new UTF8Encoding(false));
        }
    }
}
