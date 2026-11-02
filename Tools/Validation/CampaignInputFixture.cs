using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Isolated level reward and full-bag portal setup; inputs use GameSettings' normal path.
public static class CampaignInputFixture
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception("[Campaign Input] FAIL "+name);checks++;Debug.Log("[Campaign Input] CHECK "+name);}
    static void Set(GameController game,string property,object value)=>typeof(GameController).GetProperty(property).GetSetMethod(true).Invoke(game,new[]{value});
    public static string Main()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        var root="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-input/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(root);
        game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        var input=typeof(StageValidationInput);
        input.GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        input.GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        Application.runInBackground=true;Time.captureFramerate=0;
        UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.CameraFollow.StartCoroutine(Run(game,root));return root;
    }
    static IEnumerator Run(GameController game,string root)
    {
        yield return new WaitForSecondsRealtime(1);
        game.StopAllCoroutines();Clear();yield return null;
        Check(DemoContractValidation.Validate(game.DungeonMap),"current campaign art/content build contract");
        for(var choice=0;choice<6;choice++)
        {
            game.Progression.AddExperience(game.Progression.RequiredExperience);
            Check(game.IsChoosingAccessory && game.IsPaused && game.CurrentAccessoryChoices.Length==6,"six-choice modal pauses gameplay "+choice);
            var expected=game.CurrentAccessoryChoices[choice];var count=game.Inventory.Accessories.Count;
            if(choice==0){Capture(root+"/six-choices.png");yield return new WaitForSecondsRealtime(.2f);}
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameSettingsService.AccessoryChoiceAction(choice),true);
            yield return null;
            StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
            Check(!game.IsChoosingAccessory && game.Inventory.Accessories.Count==count+1 && game.Inventory.Accessories[count]==expected,"correct numbered reward without input leakage "+choice);
            yield return null;
        }
        while(game.Inventory.StoredItemCount<game.Inventory.UnlockedCapacity)Check(game.Inventory.AddAccessory(AccessoryLootTable.All[0]),"fill isolated bag");
        Set(game,"IsStageClearPending",true);Set(game,"IsStageResultReady",true);
        game.Player.Teleport(game.StageExitPosition);game.Player.SetStageClearSafety(true);
        var drop=FieldLootPickup.Create(game.StageExitPosition,"Fixture full bag loot",Color.white,game.Player,game.Settings,()=>false,0);
        drop.RequiresStageDecision=true;
        typeof(FieldLootPickup).GetField("tossTime",Private).SetValue(drop,0f);
        typeof(FieldLootPickup).GetField("settledPosition",Private).SetValue(drop,drop.transform.position);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Interact,true);
        yield return null;StageValidationInput.BeginFrame();
        yield return new WaitForSecondsRealtime(.4f);
        Check(game.PendingStageRewards==1 && !game.IsStageTransitioning,"short hold cannot discard equipment");
        yield return new WaitForSecondsRealtime(.95f);
        Check(game.PendingStageRewards==0 && !game.IsStageTransitioning,"long hold explicitly leaves nearby uncollectible gear");
        StageValidationInput.SetHeld(GameAction.Interact,false);yield return null;
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Interact,true);yield return null;
        StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
        Check(game.IsStageTransitioning,"new press enters next stage despite nearby left gear");
        yield return new WaitForSecondsRealtime(1.3f);
        Check(game.CurrentStageIndex==1 && !game.IsStageTransitioning,"full bag transition completes");
        Debug.Log("[Campaign Input] PASS "+checks+" checks; injected reward/portal fixture, not normal completion: "+root);
    }
    static void Clear()=>typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
    static void Capture(string path)=>Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{path,1});
}
