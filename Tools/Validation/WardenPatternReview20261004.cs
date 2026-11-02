using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RuinRunner;
using Newtonsoft.Json.Linq;

// Supplied arena, protected target and controlled distance. Real Update/coroutines;
// no forced pattern calls, shortened timers, damage or normal-campaign claim.
public static class WardenPatternReview20261004
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags All = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static GameController game; static EnemyController actor; static string folder;
    static readonly JArray rows = new JArray(); static bool expected, paused;
    static Type tick; static bool oldTick; static long oldInterval;
    static EditorWindow view; static Vector2 center;
    public static string Main(bool fixedExpected)
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        expected=fixedExpected; rows.Clear(); paused=false;
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-04-boss-pressure/"+(expected?"after-":"before-")+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu(); game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        Application.runInBackground=true; Time.captureFramerate=0;
        view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.Focus();
        tick=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand")).First(t=>t!=null);
        oldTick=(bool)tick.GetProperty("IsEnabled",All).GetValue(null);
        oldInterval=(long)tick.GetProperty("CurrentIntervalMs",All).GetValue(null);
        tick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{true,0,false});
        game.CameraFollow.StartCoroutine(Guard(Run())); return folder;
    }
    static void Save() => File.WriteAllText(folder+"/actions.json",rows.ToString(),new UTF8Encoding(false));
    static IEnumerator Guard(IEnumerator run)
    {
        Exception failure=null;
        while(true)
        {
            bool next=false; object current=null;
            try{next=run.MoveNext();if(next)current=run.Current;}catch(Exception e){failure=e;}
            if(failure!=null||!next)break;yield return current;
        }
        Save(); paused=false;
        if(actor!=null)UnityEngine.Object.Destroy(actor.gameObject);
        tick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{oldTick,(int)oldInterval,false});
        game.enabled=true;game.ReturnToMainMenu();
        File.WriteAllText(folder+"/result.txt",(failure==null?(expected?"PASS":"REPRODUCED"):"FAIL")+" staged Warden selection; supplied distance/protection, no normal combat claim\n"+(failure==null?"":failure.ToString()),new UTF8Encoding(false));
        if(failure!=null)Debug.LogException(failure);else Debug.Log("[Warden Pattern Review] "+folder);
    }
    static IEnumerator Run()
    {
        yield return null;
        game.StopAllCoroutines();game.enabled=false;
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        var previous=game.DungeonMap;var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        typeof(GameController).GetProperty("DungeonMap").SetValue(game,map);UnityEngine.Object.Destroy(previous.Root);
        yield return null;center=map.GetRoomCenter(3);game.Player.SetStageClearSafety(true);
        game.Player.enabled=false;game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        foreach(var mode in expected?new[]{"close","far","changing"}:new[]{"close"})
        {
            game.Player.Teleport(center+Vector2.right*(mode=="far"?6:1.9f));
            actor=EnemyController.Create(EnemyType.Boss,center,game.Player,()=>paused,_=>{},game.CameraFollow);
            var animation=actor.GetComponent<BossSpriteAnimator>();
            int actions=0; bool wasActing=false;float began=Time.time;float actionAt=0;JObject row=null;
            var phases=new HashSet<int>();var selected=new List<string>();
            while(actions<8||actor.IsActing)
            {
                if(Time.time-began>40)throw new Exception("AI selection timeout "+mode+" actions="+actions);
                if(!actor.IsActing)
                {
                    actor.GetComponent<Rigidbody2D>().position=center;actor.transform.position=center;
                    game.Player.Teleport(center+Vector2.right*(mode=="far"||mode=="changing"&&actions%2==1?6:1.9f));
                }
                view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame();
                if(actor.IsActing&&!wasActing)
                {
                    actions++;actionAt=Time.time;phases.Clear();selected.Add(animation.Pose.ToString());
                    row=new JObject{["mode"]=mode,["index"]=actions,["pose"]=animation.Pose.ToString(),["distance"]=(game.Player.transform.position-actor.transform.position).magnitude,["started"]=Time.time-began};rows.Add(row);
                    var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+mode+"-"+actions.ToString("D2")+"-"+animation.Pose+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
                }
                if(actor.IsActing)phases.Add(actor.AttackVisualPhase);
                if(!actor.IsActing&&wasActing)
                {
                    row["duration"]=Time.time-actionAt;row["phases"]=new JArray(phases.OrderBy(v=>v));
                    if(!phases.SetEquals(new[]{0,1,2}))throw new Exception("Missing telegraph/contact/recovery "+row);
                    Save();
                }
                wasActing=actor.IsActing;
            }
            if(expected)
            {
                for(int offset=0;offset<8;offset+=4)
                    if(selected.Skip(offset).Take(4).Distinct().Count()!=4)throw new Exception("Skipped/repeated pattern "+mode+" "+string.Join(",",selected));
            }
            else if(selected.Any(p=>p!="Charge"))throw new Exception("Expected close-range failure did not reproduce");
            UnityEngine.Object.Destroy(actor.gameObject);actor=null;
            foreach(var shot in UnityEngine.Object.FindObjectsByType<EnemyProjectile>())UnityEngine.Object.Destroy(shot.gameObject);
            yield return null;
        }
        game.Player.enabled=true;
    }
}
