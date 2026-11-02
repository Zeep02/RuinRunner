using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using RuinRunner;

public static class PilotRouteFixture
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string folder;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        Application.runInBackground=true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-pilot-route/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        game.CameraFollow.StartCoroutine(Run(game));return folder;
    }
    static IEnumerator Run(GameController game)
    {
        yield return new WaitForSecondsRealtime(.2f);
        typeof(GameController).GetMethod("BeginNextStage",Private).Invoke(game,null);
        yield return new WaitForSecondsRealtime(.8f);
        game.StopAllCoroutines();
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        game.Player.SetStageClearSafety(true);
        for(var i=0;i<game.DungeonMap.RoomCount;i++)game.DungeonMap.UnlockExit(i);
        var input=typeof(StageValidationInput);
        input.GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        input.GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        StageValidationInput.ReleaseAll();
        var pilot=game.gameObject.AddComponent<AutomatedStageValidation>();pilot.Initialize(game);pilot.enabled=false;
        var move=typeof(AutomatedStageValidation).GetMethod("MoveTo",Private);
        QualitySettings.vSyncCount=0;
        var report="Injected starting positions / no combat. Movement uses only normal GameAction inputs.\n";
        foreach(var fps in new[]{20,30,60,120})
        {
            Application.targetFrameRate=fps;
            game.Player.Teleport(new Vector2(9.5f,30.65f));
            typeof(AutomatedStageValidation).GetField("routeRoom",Private).SetValue(pilot,-1);
            var destination=new Vector2(10,26.25f);var start=Time.realtimeSinceStartup;var frames=0;
            var positions="seconds,x,y,vx,vy,waypointX,waypointY,braking,inputX,inputY\n";
            while(Time.realtimeSinceStartup-start<12 && Vector2.Distance(game.Player.transform.position,destination)>1.2f)
            {
                StageValidationInput.BeginFrame();move.Invoke(pilot,new object[]{destination,true});
                var p=game.Player.transform.position;var velocity=game.Player.GetComponent<Rigidbody2D>().linearVelocity;
                var route=(System.Collections.Generic.List<Vector2>)typeof(AutomatedStageValidation).GetField("route",Private).GetValue(pilot);
                var waypoint=route.Count>0?route[0]:destination;
                var braking=typeof(AutomatedStageValidation).GetField("brakingAtWaypoint",Private).GetValue(pilot);
                var inputX=(StageValidationInput.GetKey(GameAction.MoveRight)?1:0)-(StageValidationInput.GetKey(GameAction.MoveLeft)?1:0);
                var inputY=(StageValidationInput.GetKey(GameAction.MoveUp)?1:0)-(StageValidationInput.GetKey(GameAction.MoveDown)?1:0);
                positions+=(Time.realtimeSinceStartup-start).ToString("F4")+","+p.x+","+p.y+","+velocity.x+","+velocity.y+","+waypoint.x+","+waypoint.y+","+braking+","+inputX+","+inputY+"\n";frames++;
                yield return null;
            }
            StageValidationInput.ReleaseAll();var elapsed=Time.realtimeSinceStartup-start;
            var passed=Vector2.Distance(game.Player.transform.position,destination)<=1.2f;
            report+="requestedFPS="+fps+" observed="+(frames/elapsed).ToString("F1")+" "+(passed?"PASS":"FAIL")+" elapsed="+elapsed.ToString("F2")+" end="+game.Player.transform.position+"\n";
            File.WriteAllText(folder+"/route-"+fps+".csv",positions);File.WriteAllText(folder+"/result.txt",report);
            Debug.Log("[Pilot Route] "+report);
        }
        UnityEngine.Object.Destroy(pilot);game.ReturnToMainMenu();
    }
}
