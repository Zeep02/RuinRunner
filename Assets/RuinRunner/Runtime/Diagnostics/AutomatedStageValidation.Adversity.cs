using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RuinRunner
{
    // Opt-in normal-rules journey: enemy attacks cause the first defeat; the
    // following expedition earns all inventory and deferred rewards normally.
    public sealed partial class AutomatedStageValidation
    {
        readonly bool adversityScenario=GetCommandLineValue("--validation-scenario")=="adversity";
        bool adversityDefeated,adversityRetried,adversityResultCaptured,adversityBagBlocked,adversityBagVisible;
        bool adversityReserved,adversityResolved,adversityRedeemed,adversityExitPending,adversityLeftRewards;
        float adversityResultAfter,adversityProbeAfter,adversityHoldPeak;
        int adversityReserveCount,adversityBagCount,adversityProbeStep;
        long adversityProbeRevision;
        FieldLootPickup adversityProbe;
        readonly List<string> adversityEvents=new List<string>();

        [Serializable] sealed class AdversityEvidence
        {
            public bool passed,normalEnemyDefeat,retryThroughTown,fullBagPickupBlocked,fullBagInventoryShown;
            public bool rewardReserved,rewardOpenedInSafety,rewardChosen,pendingExitRewards,leftRewardsByHeldInput;
            public bool cheatsOrSuppliedState=false,manualUiInput=false;
            public float leaveInputPeak;
            public string reason;
            public string[] events;
        }
        void AdversityEvent(string message)
        {
            adversityEvents.Add(message);Debug.Log("[Adversity] "+message);
            if(captureScreenshots&&!string.IsNullOrEmpty(captureDirectory))
                ScreenCapture.CaptureScreenshot(Path.Combine(captureDirectory,"adversity-"+adversityEvents.Count.ToString("00")+".png"));
            WriteAdversity(false,"running; acceptance not complete");
        }
        void WriteAdversity(bool passed,string reason)
        {
            if(!adversityScenario||string.IsNullOrEmpty(captureDirectory))return;
            var result=new AdversityEvidence{passed=passed,reason=reason,normalEnemyDefeat=adversityDefeated,retryThroughTown=adversityRetried,
                fullBagPickupBlocked=adversityBagBlocked,fullBagInventoryShown=adversityBagVisible,rewardReserved=adversityReserved,
                rewardOpenedInSafety=adversityResolved,rewardChosen=adversityRedeemed,pendingExitRewards=adversityExitPending,
                leftRewardsByHeldInput=adversityLeftRewards,leaveInputPeak=adversityHoldPeak,events=adversityEvents.ToArray()};
            File.WriteAllText(Path.Combine(captureDirectory,"adversity.json"),JsonUtility.ToJson(result,true),new UTF8Encoding(false));
        }
        bool HoldAdversityDefeatResult()
        {
            if(!adversityScenario||adversityDefeated||game.IsVictory)return false;
            if(!adversityResultCaptured)
            {
                adversityResultCaptured=true;adversityResultAfter=Time.unscaledTime+1.2f;
                AdversityEvent("Enemy attacks caused normal defeat; server-confirmed result remains visible before return.");
                return true;
            }
            if(Time.unscaledTime<adversityResultAfter)return true;
            adversityDefeated=true;return false;
        }
        bool ContinueAdversityAfterTown()
        {
            if(!adversityScenario||!adversityDefeated||adversityRetried)return false;
            adversityRetried=true;
            AdversityEvent("Returned after defeat with totalRuns="+game.BackendProfile.state.totalRuns+"; preparing a normal retry.");
            visitedFacilities.Clear();deferredFacilities.Clear();visitedRegions.Clear();
            route.Clear();routeRoom=-1;lastRoom=-1;lastStage=-1;leavingEquipment=false;
            telegraphMovementChecked=false;telegraphMovementStarted=false;
            AdvanceJourney(6);return true;
        }
        bool PrepareAdversityTalent()
        {
            if(!adversityScenario||!adversityDefeated||game.ActiveTalents.Has(TalentId.ReserveOffer))return false;
            if(Array.IndexOf(game.SaveSlots.ActiveData.unlockedTalentIds,"reserve_offer")<0)
            {
                if(game.BackendProfile.state.totalRuns<1||game.BackendProfile.legacyShards<5)
                {Finish(false,"normal defeat did not earn the ordinary requirements for reserve_offer");return true;}
                game.UnlockTownChoice("talent","reserve_offer");
            }
            else game.ToggleTalent("reserve_offer");
            journeyActionAt=Time.unscaledTime+.5f;return true;
        }
        bool DriveAdversityCombat()
        {
            if(!adversityScenario||adversityDefeated||game.CurrentStageIndex!=0||game.Boss==null)return false;
            StageValidationInput.ReleaseAll();attackHeld=false;
            var delta=(Vector2)game.Boss.transform.position-(Vector2)game.Player.transform.position;
            currentIntent="normal defeat check: approach Warden without attacking or dodging";
            StageValidationInput.SetAim(delta);SetMove(delta.magnitude>1.1f?delta.normalized:Vector2.zero);
            return true;
        }
        bool DriveAdversityState()
        {
            if(!adversityScenario||!adversityRetried||game.Player==null||!game.IsExpedition||game.IsGameOver||game.IsStageTransitioning)return false;
            // XP can open an ordinary choice between the rejected pickup and
            // the inventory key. Let the normal choice driver finish it first.
            if(game.IsChoosingAccessory)return false;
            if(game.IsRewardReserved)
            {
                if(!adversityReserved)
                {
                    adversityReserved=true;adversityReserveCount=game.Progression.PendingLevelRewards;
                    AdversityEvent("Level reward held through active play; pending="+adversityReserveCount+", enemies="+game.EnemiesAlive+", paused="+game.IsPaused);
                }
                if(game.EnemiesAlive==0&&!game.IsPaused&&!game.IsEncounterTransitioning)
                {
                    game.ResolveReservedReward();
                    if(game.IsChoosingAccessory&&!adversityResolved)
                    {adversityResolved=true;AdversityEvent("Opened the reserved reward using its normal button handler in a cleared region.");}
                    return true;
                }
            }
            if(adversityResolved&&!adversityRedeemed&&!game.IsChoosingAccessory&&game.Progression.PendingLevelRewards<adversityReserveCount)
            {adversityRedeemed=true;AdversityEvent("Reserved reward selected through its ordinary choice input; pending count consumed once.");}
            if(adversityProbeStep==0)return false;
            StageValidationInput.ReleaseAll();attackHeld=false;
            if(Time.unscaledTime<adversityProbeAfter)return true;
            if(adversityProbeStep==1)
            {
                if(adversityProbe==null||game.Inventory.StoredItemCount!=adversityBagCount||game.BackendEconomy.revision!=adversityProbeRevision||
                    game.LootMessage==null||!game.LootMessage.Contains("가방이 가득"))
                {Finish(false,"full-bag interaction did not retain pickup/count/revision and display its capacity message");return true;}
                adversityBagBlocked=true;AdversityEvent("Normal pickup rejected a naturally full bag; loot, inventory count and server revision remain unchanged.");
                adversityProbeStep=2;
            }
            if(adversityProbeStep==2)
            {
                if(!game.IsInventoryOpen){Tap(GameAction.Inventory);return true;}
                adversityBagVisible=true;AdversityEvent("Full inventory shown through the normal inventory key: "+adversityBagCount+"/"+game.Inventory.UnlockedCapacity);
                adversityProbeAfter=Time.unscaledTime+1.2f;adversityProbeStep=3;return true;
            }
            if(game.IsInventoryOpen){Tap(GameAction.Inventory);return true;}
            adversityProbeStep=0;return false;
        }
        bool ProbeAdversityFullBag()
        {
            if(!adversityScenario||!adversityRetried||adversityBagBlocked||game.Inventory.StoredItemCount<game.Inventory.UnlockedCapacity||
                game.Inventory.HasRoomFor(InventoryEntryKind.Weapon)||game.EconomyOperationPending||game.BackendHasPending||game.IsPaused)return false;
            FieldLootPickup nearest=null;float best=float.PositiveInfinity;
            foreach(var item in RuntimeSceneObjects.FindAll<FieldLootPickup>())
            {
                if(game.Inventory.HasRoomFor(item.InventoryKind))continue;
                float distance=Vector2.SqrMagnitude(item.transform.position-game.Player.transform.position);
                if(distance<best){best=distance;nearest=item;}
            }
            if(nearest==null)return false;
            currentIntent="try an ordinary pickup with a naturally full bag";
            if(!nearest.IsSettled){ReleaseMovement();return true;}
            if(best>1f){MoveTo(nearest.transform.position,true);return true;}
            ReleaseMovement();
            if(!Tap(GameAction.Interact))return true;
            adversityProbe=nearest;adversityBagCount=game.Inventory.StoredItemCount;adversityProbeRevision=game.BackendEconomy.revision;
            adversityProbeAfter=Time.unscaledTime+.35f;adversityProbeStep=1;return true;
        }
        void ObserveAdversityExit()
        {
            if(!adversityScenario||!adversityRetried)return;
            adversityHoldPeak=Mathf.Max(adversityHoldPeak,game.LeaveEquipmentProgress);
            if(game.PendingStageRewards>0&&!adversityExitPending)
            {adversityExitPending=true;AdversityEvent("Stage exit retains "+game.PendingStageRewards+" uncollected equipment rewards before explicit leave input.");}
            if(adversityExitPending&&!adversityLeftRewards&&game.PendingStageRewards==0&&adversityHoldPeak>.5f)
            {adversityLeftRewards=true;AdversityEvent("Held ordinary interaction to leave uncollected equipment; exit unblocked without granting it.");}
        }
        bool ValidateAdversityCompletion(bool passed,string reason)
        {
            if(!adversityScenario)return passed;
            bool complete=passed&&adversityDefeated&&adversityRetried&&adversityBagBlocked&&adversityBagVisible&&adversityReserved&&
                adversityResolved&&adversityRedeemed&&adversityExitPending&&adversityLeftRewards;
            WriteAdversity(complete,complete?reason:"Adversity coverage incomplete: "+reason);return complete;
        }
    }
}
