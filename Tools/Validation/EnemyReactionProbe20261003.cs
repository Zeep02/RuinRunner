using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using RuinRunner;
using Newtonsoft.Json.Linq;

// Supplied actors and contacts, actual action/death coroutines and pause API.
public static class EnemyReactionProbe20261003
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string folder; static GameController game; static Vector2 center; static bool strict;
    static readonly JArray checks=new JArray();
    static Type tick; static bool oldTick; static long oldInterval;
    static void Check(bool okay,string message){checks.Add(new JObject{{"pass",okay},{"check",message}});File.WriteAllText(folder+"/checks.json",checks.ToString());if(!okay&&strict)throw new Exception(message);}
    static SpriteRenderer View(EnemyController e)=>e.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
    static bool Same(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.001f&&Mathf.Abs(a.g-b.g)<.001f&&Mathf.Abs(a.b-b.b)<.001f;
    static readonly Color Warm=new Color(1,.68f,.5f),Yellow=new Color(1,.9f,.25f);
    public static string Baseline(){strict=false;return Start();}
    public static string Main(){strict=true;return Start();}
    static string Start()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-03-enemy-reactions/"+(strict?"verified-":"baseline-")+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);checks.Clear();
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();game.enabled=true;
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        Application.runInBackground=true;Application.targetFrameRate=60;Time.timeScale=1;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){tick=assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand");if(tick!=null)break;}
        var all=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        oldTick=(bool)tick.GetProperty("IsEnabled",all).GetValue(null);oldInterval=(long)tick.GetProperty("CurrentIntervalMs",all).GetValue(null);
        tick.GetMethod("SetAutoTick",all).Invoke(null,new object[]{true,0,false});
        game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true){object next=null;bool more;try{more=run.MoveNext();if(more)next=run.Current;}catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e);RestoreTick();Time.timeScale=1;Debug.LogException(e);yield break;}if(!more)break;yield return next;}
        RestoreTick();
        File.WriteAllText(folder+"/result.txt",(strict?"PASS":"BASELINE")+" supplied-contact fixture; real damage/action/death/pause paths; not normal gameplay or performance.");
        game.ReturnToMainMenu();game.enabled=true;Time.timeScale=1;
    }
    static void RestoreTick()=>tick.GetMethod("SetAutoTick",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{oldTick,(int)oldInterval,false});
    static EnemyController Spawn(EnemyType type,Action<EnemyController> died)
    {
        var e=EnemyController.Create(type,center,game.Player,()=>game.IsPaused,died,game.CameraFollow);return e;
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        center=game.DungeonMap.GetRoomCenter(3);game.Player.Teleport(center+new Vector2(2,0));game.Player.SetStageClearSafety(true);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        yield return new WaitForSeconds(.1f);
        var a=Spawn(EnemyType.Grunt,_=>{});
        a.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("GruntLunge",Private).Invoke(a,new object[]{Vector2.right}));
        Check(Same(View(a).color,Yellow),"real lunge shows yellow anticipation");
        a.TakeDamage(1,Vector2.zero);yield return new WaitForSeconds(.09f);
        Check(a.IsActing&&a.AttackVisualPhase==0&&Same(View(a).color,Yellow),"hit restores ongoing anticipation tint after short flash");
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"/anticipation-after-hit.png");
        UnityEngine.Object.Destroy(a.gameObject);yield return null;
        a=Spawn(EnemyType.Grunt,_=>{});a.TakeDamage(1,Vector2.zero);yield return new WaitForSeconds(.035f);a.TakeDamage(1,Vector2.zero);
        yield return new WaitForSeconds(.03f);Check(Same(View(a).color,Warm),"latest overlapping hit retains its full flash duration");
        yield return new WaitForSeconds(.07f);Check(Same(View(a).color,Color.white),"overlapping hits restore original tint without residue");UnityEngine.Object.Destroy(a.gameObject);yield return null;
        foreach(var type in new[]{EnemyType.Boss,EnemyType.Tidekeeper,EnemyType.SealHeart})
        {
            int deaths=0;var boss=Spawn(type,_=>deaths++);boss.TakeDamage(100000,Vector2.zero);
            yield return new WaitForSeconds(.08f);typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});yield return new WaitForEndOfFrame();
            float hp=boss.Health;var frame=View(boss).sprite;
            yield return new WaitForSecondsRealtime(.28f);
            Check(boss!=null&&boss.Health==hp&&View(boss).sprite==frame&&deaths==0,"pause freezes boss death, HP drain and reward callback "+type);
            game.ResumeGame();yield return new WaitForSeconds(1.3f);Check(boss==null&&deaths==1,"boss death resumes and completes exactly once "+type);
            foreach(var f in UnityEngine.Object.FindObjectsByType<FadingSpark>())UnityEngine.Object.Destroy(f.gameObject);
            yield return null;
        }
    }
}
