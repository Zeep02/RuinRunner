using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Isolated forced-state regression. Never presented as input playthrough or balance evidence.
public static class CampaignTransitionFixture
{
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Invoke(GameController game,string method,params object[] args) => typeof(GameController).GetMethod(method,Private).Invoke(game,args);
    private static void Require(bool value,string message) {if(!value)throw new Exception("[Campaign Fixture] FAIL: "+message);}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        Application.runInBackground=true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(game.IsRunActive)game.ReturnToMainMenu();
        game.SaveSlots.OpenTransientForTests();
        Invoke(game,"BeginRunForActiveSlot");
        game.CameraFollow.StartCoroutine(Run(game));
        return "Forced-state campaign transition fixture started; transient profile. NOT normal run evidence.";
    }
    private static IEnumerator Run(GameController game)
    {
        yield return null;
        var player=game.Player; var inventory=game.Inventory; var progression=game.Progression; var stats=game.Stats;
        inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));
        inventory.SelectQuickSlot(1);player.EquipWeapon(inventory.EquippedWeapon);inventory.TryConsumeAmmo();inventory.TryConsumeAmmo();
        var startingAmmo=inventory.ActiveSlot.Ammo;
        var startingLegacy=game.MetaProgression.LegacyShards;
        for(var stage=0;stage<3;stage++)
        {
            Require(game.CurrentStageIndex==stage,"stage index");
            player.SetStageClearSafety(true); // Fixture only.
            for(var room=0;room<4;room++)
            {
                var deadline=Time.realtimeSinceStartup+30;
                while(!game.IsExitOpen)
                {
                    Require(Time.realtimeSinceStartup<deadline,"encounter timeout stage="+stage+" room="+room);
                    if(game.IsChoosingAccessory)game.SelectAccessory(game.CurrentAccessoryChoices[0]);
                    foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>())
                        if(!enemy.IsDead)enemy.TakeDamage(99999,Vector2.zero);
                    yield return null;
                }
                Require(game.CurrentRoom==room,"encounter sequence");
                player.Teleport(game.DungeonMap.GetRoomCenter(room)+RoomLayoutRules.RewardPosition);
                typeof(RewardChest).GetMethod("Open",Private).Invoke(game.ActiveChest,null);
                var rewardDeadline=Time.realtimeSinceStartup+12;
                while(Time.realtimeSinceStartup<rewardDeadline)
                {
                    if(game.IsChoosingAccessory)game.SelectAccessory(game.CurrentAccessoryChoices[0]);
                    foreach(var loot in UnityEngine.Object.FindObjectsByType<FieldLootPickup>())
                    {
                        var collect=(Func<bool>)typeof(FieldLootPickup).GetField("collect",Private).GetValue(loot);
                        if(collect()) {loot.RequiresStageDecision=false;UnityEngine.Object.Destroy(loot.gameObject);}
                    }
                    if(!game.IsChoosingAccessory && UnityEngine.Object.FindObjectsByType<ExperienceOrb>().All(o=>o.IsCollected))break;
                    yield return null;
                }
                if(room<3)
                {
                    Invoke(game,"EnterRoom",room+1);
                    player.Teleport(game.DungeonMap.GetRoomCenter(room+1));
                }
                yield return null;
            }
            Require(!game.IsGameOver,"intermediate clear cannot settle victory");
            Require(game.Progression.PendingLevelRewards==0,"level choices resolved");
            Require(game.IsStageResultReady,"exit ready after boss XP");
            var hp=player.Health;var level=progression.Level;var kills=stats.EnemiesDefeated;
            var ammo=inventory.ActiveSlot.Ammo;var bag=inventory.StoredItemCount;var equip=inventory.EquippedWeapon;
            Require(ammo==startingAmmo,"ammo did not refill during encounters");
            player.Teleport(game.StageExitPosition);
            if(game.PendingStageRewards>0)game.LeaveUncollectedStageEquipment();
            // Hold a real callback from the old epoch and invoke it after the new stage loads.
            var callback=(Action<EnemyController>)typeof(GameController).GetMethod("CaptureEnemyDeathCallback",Private).Invoke(game,null);
            game.ShowStageResult();game.ShowStageResult();
            yield return new WaitForSecondsRealtime(1.2f);
            if(stage<2)
            {
                Require(game.CurrentStageIndex==stage+1 && !game.IsGameOver,"next stage, not result");
                Require(ReferenceEquals(player,game.Player)&&ReferenceEquals(inventory,game.Inventory)&&ReferenceEquals(progression,game.Progression)&&ReferenceEquals(stats,game.Stats),"same run-owned instances");
                Require(game.Player.Health==hp && progression.Level==level && stats.EnemiesDefeated==kills,"health/level/stats preserved");
                Require(inventory.StoredItemCount==bag&&inventory.ActiveSlot.Ammo==ammo&&ReferenceEquals(equip,inventory.EquippedWeapon),"bag/equipment/ammo preserved");
                Require(game.MetaProgression.LegacyShards==startingLegacy,"no intermediate meta settlement");
                Require(UnityEngine.Object.FindObjectsByType<StageRuntimeOwnership>().Length==1,"old terrain destroyed");
                callback(null);Require(stats.EnemiesDefeated==kills,"stale callback ignored");
                Debug.Log("[Campaign Fixture] PASS transition "+(stage+1)+" -> "+(stage+2)+" health="+hp+" ammo="+ammo+" level="+level+" kills="+kills);
            }
        }
        Require(game.IsVictory&&game.IsGameOver&&game.StagesCleared==3,"final victory");
        var settled=game.MetaProgression.LegacyShards;
        game.ShowStageResult();game.PlayerDied();
        Require(game.MetaProgression.LegacyShards==settled,"settles exactly once");
        Debug.Log("[Campaign Fixture] PASS final settlement; 3 stages, same player/inventory/progression/stats, stale callbacks, partial ammo. FORCED STATE / NOT NORMAL COMPLETION.");
    }
}
