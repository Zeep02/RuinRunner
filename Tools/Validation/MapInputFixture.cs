using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Isolated transient profile and safety flag. Normal GameAction inputs; not a campaign clear.
public static class MapInputFixture
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string folder;
    static int checks;
    static void Check(bool value,string label)
    {
        File.AppendAllText(folder+"/result.txt",(value?"PASS ":"FAIL ")+label+"\n");
        if(!value)throw new Exception("[Map Input] "+label);
        checks++;
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-map-input/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);checks=0;
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();
        game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        typeof(StageValidationInput).GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        StageValidationInput.ReleaseAll();Application.runInBackground=true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.CameraFollow.StartCoroutine(Run(game));return folder;
    }
    static IEnumerator Run(GameController game)
    {
        yield return new WaitForSecondsRealtime(1.5f);
        game.Player.SetStageClearSafety(true);
        foreach(var close in new[]{GameAction.Map,GameAction.Pause})
        {
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Map,true);yield return null;
            StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
            Check(game.IsMapOpen && game.IsPaused && Time.timeScale==0,"map pauses the run: "+close);
            var position=game.Player.GetComponent<Rigidbody2D>().position;var time=Time.time;
            var sequence=game.Player.AttackAnimationSequence;var dashes=game.Player.DashCharges;
            var weapon=game.Player.EquippedWeapon;
            foreach(var key in new[]{GameAction.MoveUp,GameAction.Attack,GameAction.Guard,GameAction.Dash,GameAction.Inventory,GameAction.Stats,GameAction.Legacy,GameAction.Reload,GameAction.Weapon2,GameAction.Interact})
                StageValidationInput.SetHeld(key,true);
            yield return null;StageValidationInput.BeginFrame();
            yield return new WaitForSecondsRealtime(.4f);
            Check(game.Player.GetComponent<Rigidbody2D>().position==position && Time.time==time,"world and movement remain frozen");
            Check(game.Player.AttackAnimationSequence==sequence && game.Player.DashCharges==dashes && game.Player.EquippedWeapon==weapon,"combat / dash / equipment input blocked");
            Check(game.IsMapOpen && !game.IsInventoryOpen && !game.IsStatsOpen && !game.IsUnlockOpen,"other modals do not open through map");
            if(close==GameAction.Map)
            {
                Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{folder+"/map-paused.png",1});
                yield return null;
            }
            StageValidationInput.ReleaseAll();yield return null;StageValidationInput.BeginFrame();
            StageValidationInput.SetHeld(close,true);StageValidationInput.SetHeld(GameAction.Attack,true);StageValidationInput.SetHeld(GameAction.Dash,true);
            StageValidationInput.SetHeld(GameAction.Inventory,true);yield return null;
            Check(!game.IsMapOpen && !game.IsManuallyPaused && !game.IsInventoryOpen,"close action is consumed: "+close);
            Check(game.Player.AttackAnimationSequence==sequence && game.Player.DashCharges==dashes,"closing frame cannot attack or dash");
            StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();yield return null;yield return null;
            Check(!game.IsPaused && Time.timeScale>0,"simulation resumes after closing");
        }
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Map,true);yield return null;
        StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();game.ReturnToMainMenu();
        Check(!game.IsMapOpen,"return to menu clears map");
        File.AppendAllText(folder+"/result.txt","COMPLETE "+checks+" checks; isolated fixture, not normal completion\n");
        Debug.Log("[Map Input] PASS "+checks+" checks: "+folder);
    }
}
