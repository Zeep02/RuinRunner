using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Isolated lethal-hit and retry fixture. Damage is injected; never campaign evidence.
public static class DeathFlowFixture
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool pass,string name){if(!pass)throw new Exception("[Death Flow] FAIL "+name);checks++;Debug.Log("[Death Flow] CHECK "+name);}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        var root="F:/codex/LoglikeCap/Logs/Validation/2026-09-16-death/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(root);
        game.ReturnToMainMenu();
        typeof(GameController).GetProperty("SaveSlots").GetSetMethod(true).Invoke(game,new object[]{new SaveSlotService(root+"/Saves")});
        Application.runInBackground=true;Time.captureFramerate=0;
        UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.StartSlot(1);
        game.CameraFollow.StartCoroutine(Run(game,root));
        return "Isolated death/retry capture started: "+root;
    }
    static IEnumerator Run(GameController game,string root)
    {
        yield return new WaitForSecondsRealtime(1);
        game.StopAllCoroutines();
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        var player=game.Player;
        typeof(PlayerController).GetField("invincibilityTicks",Private).SetValue(player,0);
        player.SetStageClearSafety(false);
        var pose=player.GetComponent<PlayerAnimationController>();
        var visual=player.transform.Find("VisualRoot");
        var anchor=visual.localPosition;
        var initialRuns=game.SaveSlots.ActiveData.totalRuns;
        var position=player.transform.position;
        player.TakeDamage(999,Vector2.zero);
        Check(player.IsDead && game.IsGameOver,"lethal damage enters death and ends gameplay");
        Check(game.SaveSlots.ActiveData.totalRuns==initialRuns+1,"death result recorded once immediately");
        var shards=game.MetaProgression.LegacyShards;
        player.TakeDamage(999,Vector2.zero);game.PlayerDied();
        Check(game.SaveSlots.ActiveData.totalRuns==initialRuns+1 && game.MetaProgression.LegacyShards==shards,"repeated lethal/callback cannot duplicate reward");
        var seen=new System.Collections.Generic.HashSet<int>();
        var last=-1;
        var start=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-start<1.25f)
        {
            yield return new WaitForEndOfFrame();
            if(pose.PoseName!="death" || visual.localPosition!=anchor || pose.FrameIndex<last)
                throw new Exception("Death pose/anchor changed or rewound");
            last=pose.FrameIndex;
            if(seen.Add(last)) { Check(true,"death fixed anchor and forward pose "+last); Capture(root+"/pose-"+last+".png"); }
        }
        Check(seen.Count==5 && last==4 && player.DeathProgress==1,"five unique authored death poses finish in unscaled time");
        Check(Vector3.Distance(player.transform.position,position)<.03f,"death prevents movement");
        var held=player.GetComponent<HeldWeaponView>();
        Check(!held.IsVisible,"death hides held weapon");
        Capture(root+"/result.png");
        game.RestartRun();
        yield return new WaitForSecondsRealtime(1.5f);
        Check(game.Player!=player && !game.Player.IsDead && game.IsRunActive && !game.IsGameOver,"retry creates a live fresh player");
        Check(game.SaveSlots.ActiveData.totalRuns==initialRuns+1,"retry does not settle the previous death again");
        game.ReturnToMainMenu();
        yield return null;
        Check(game.Player==null && !game.IsRunActive,"menu return clears the run");
        Debug.Log("[Death Flow] PASS "+checks+" checks; isolated lethal injection and real-time screenshots: "+root);
    }
    static void Capture(string path)=>Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{path,1});
}
