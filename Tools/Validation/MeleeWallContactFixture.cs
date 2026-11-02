using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Reproduces the normal melee run's final-boss wall position. Forced setup only;
// attacks enter through GameAction. Includes a fully occluded target control.
public static class MeleeWallContactFixture
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string folder;
    static int checks;
    static void Set(GameController game,string name,object value) => typeof(GameController).GetProperty(name).GetSetMethod(true).Invoke(game,new[]{value});
    static void Check(bool value,string label)
    {
        File.AppendAllText(folder+"/result.txt",(value?"PASS ":"FAIL ")+label+"\n");
        if(!value)throw new Exception("[Melee Wall] "+label);
        checks++;
    }
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-melee-wall/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);checks=0;
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();
        game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        Set(game,"RunSeed",824942335);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        typeof(StageValidationInput).GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        StageValidationInput.ReleaseAll();Application.runInBackground=true;Time.captureFramerate=0;
        UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.CameraFollow.StartCoroutine(Run(game));return folder;
    }
    static IEnumerator Run(GameController game)
    {
        for(var stage=0;stage<2;stage++)
        {
            yield return new WaitForSecondsRealtime(1);
            game.Player.SetStageClearSafety(true);
            typeof(GameController).GetMethod("BeginNextStage",Private).Invoke(game,null);
        }
        yield return new WaitForSecondsRealtime(1.2f);
        game.StopAllCoroutines();
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        yield return null;
        game.Player.SetStageClearSafety(true);
        game.Player.Teleport(new Vector2(-9.35f,73.57f));
        Set(game,"CurrentRoom",3);Set(game,"StageIntroUntil",0f);
        var target=Target(game,new Vector2(-9.41f,73.95f));Set(game,"Boss",target);
        yield return null;Physics2D.SyncTransforms();
        var origin=(Vector2)game.Player.transform.position;
        var surface=target.DamageCollider.ClosestPoint(origin);
        var centerWall=Physics2D.Linecast(origin,target.DamageCollider.bounds.center,1<<GameLayers.Ground).collider;
        var surfaceWall=Physics2D.Linecast(origin,surface,1<<GameLayers.Ground).collider;
        File.AppendAllText(folder+"/result.txt","position="+origin+" hurtbox="+target.DamageCollider.bounds+" surface="+surface+" centerWall="+centerWall+" surfaceWall="+surfaceWall+"\n");
        Check(centerWall!=null && surfaceWall==null,"reproduces exposed body with center behind northern wall");
        var hp=target.Health;
        var attacks=Attack(game,target);while(attacks.MoveNext())yield return attacks.Current;
        Capture("01-exposed-body");yield return null;
        Check(target.Health<hp,"normal sword input damages exposed boss body at wall");
        UnityEngine.Object.Destroy(target.gameObject);yield return null;
        var center=new Vector2(-18,61);
        var wall=new GameObject("Fixture solid wall");wall.layer=GameLayers.Ground;wall.transform.position=center;
        wall.AddComponent<BoxCollider2D>().size=new Vector2(.25f,6);
        game.Player.Teleport(center+new Vector2(-.8f,0));
        target=Target(game,center+new Vector2(1.1f,0));Set(game,"Boss",target);
        yield return null;Physics2D.SyncTransforms();
        origin=game.Player.transform.position;surface=target.DamageCollider.ClosestPoint(origin);
        Check(Physics2D.Linecast(origin,surface,1<<GameLayers.Ground).collider!=null,"control target is fully behind wall");
        hp=target.Health;var contacts=game.Player.MeleeContactsResolved;
        attacks=Attack(game,target);while(attacks.MoveNext())yield return attacks.Current;
        Capture("02-occluded-body");yield return null;
        Check(game.Player.MeleeContactsResolved>contacts,"occluded control actually resolves sword attacks");
        Check(target.Health==hp,"solid wall still blocks damage");
        UnityEngine.Object.Destroy(wall);yield return null;
        attacks=Attack(game,target);while(attacks.MoveNext())yield return attacks.Current;
        Check(target.Health<hp,"same target becomes hittable after wall removal");
        File.AppendAllText(folder+"/result.txt","COMPLETE "+checks+" checks; forced stage/positions, normal attacks; not a campaign clear\n");
        Debug.Log("[Melee Wall] PASS "+checks+" checks: "+folder);
    }
    static EnemyController Target(GameController game,Vector2 position)
    {
        var target=EnemyController.Create(EnemyType.SealHeart,position,game.Player,()=>true,_=>{},game.CameraFollow);
        target.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        return target;
    }
    static IEnumerator Attack(GameController game,EnemyController target)
    {
        for(var i=0;i<4;i++)
        {
            StageValidationInput.SetAim((Vector2)target.DamageCollider.bounds.center-(Vector2)game.Player.transform.position);
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);yield return null;
            StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.55f);
        }
    }
    static void Capture(string name) => Type.GetType("UnityEngine.ScreenCapture,UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{folder+"/"+name+".png",1});
}
