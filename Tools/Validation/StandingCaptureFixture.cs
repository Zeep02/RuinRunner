using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Timestamped real-time capture on an isolated stage with staged equipment.
// Visual review only, never progression or performance evidence.
public static class StandingCaptureFixture
{
    static string folder,phase="settle";static bool recording;static int frame;
    static readonly StringBuilder rows=new StringBuilder("frame,realtime,phase,pose,direction,bodyFrame\n");
    static GameController game;static PlayerAnimationController animation;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-standing-capture/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var old in UnityEngine.Object.FindObjectsByType<CameraFollow>()){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();
        var player=PlayerController.Create(Vector2.zero,()=>false,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;
        follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        animation=player.GetComponent<PlayerAnimationController>();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        rows.Clear();rows.AppendLine("frame,realtime,phase,pose,direction,bodyFrame");frame=0;
        game.StartCoroutine(Run());return folder;
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.5f);
        recording=true;game.StartCoroutine(Record());
        for(var dir=0;dir<8;dir++)
        {
            phase="standing-"+dir;StageValidationInput.SetAim(new Vector2(Mathf.Sin(dir*Mathf.PI/4),Mathf.Cos(dir*Mathf.PI/4)));
            yield return new WaitForSeconds(PlayerAnimationController.IdleCycleSeconds+.2f);
        }
        phase="walk-stop-attack-dash";StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.MoveRight,true);
        yield return new WaitForSeconds(.6f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.MoveRight,false);
        yield return new WaitForSeconds(1.9f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);
        yield return new WaitForSeconds(.35f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);
        yield return new WaitForSeconds(.65f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Dash,true);
        yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Dash,false);
        yield return new WaitForSeconds(2f);StageValidationInput.ReleaseAll();recording=false;
    }
    static IEnumerator Record()
    {
        while(recording)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+frame.ToString("D6")+".png");
            rows.AppendLine(string.Join(",",frame++,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,animation.PoseName,animation.FacingDirection,animation.FrameIndex));
            yield return new WaitForSecondsRealtime(.04f);
        }
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
        File.WriteAllText(folder+"/scope.txt","Isolated stage, staged longsword, no enemy spawns and HUD hidden; actual GameAction input and real-time Game View capture. Visual fixture, NOT normal campaign completion or performance benchmark. Prior capture 022816 was obscured by first-run guide/enemies and is retained as rejected visual evidence.");
        Debug.Log("[Standing Capture] DONE "+folder);
    }
}
