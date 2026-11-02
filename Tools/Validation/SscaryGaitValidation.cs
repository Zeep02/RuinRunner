using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Production PlayerController and GameAction inputs in an isolated arena.
// This verifies replacement and control continuity, not finished combat art or campaign completion.
public static class SscaryGaitValidation
{
    static GameController game;
    static PlayerController player;
    static PlayerAnimationController animation; static InventorySystem inventory; static int shots, socketChecks; static float maxMuzzleError; static Vector3 origin;
    static string folder,phase,observationFailure; static int captureFrame; static double captureStart,nextCapture; static readonly StringBuilder captureRows=new StringBuilder();
    static bool paused,observe;
    static int observed,oldFrames,lastShot;
    static readonly HashSet<string> poses=new HashSet<string>();
    static readonly HashSet<int> idleFrames=new HashSet<int>(),dashFrames=new HashSet<int>();
    static readonly StringBuilder rows=new StringBuilder();
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-22-gait-summon/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);Directory.CreateDirectory(folder+"/frames");captureRows.Clear();captureRows.AppendLine("frame,realtime,phase");captureFrame=0;captureStart=nextCapture=Time.realtimeSinceStartupAsDouble;observationFailure=null;
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var camera in UnityEngine.Object.FindObjectsByType<CameraFollow>()){camera.gameObject.SetActive(false);UnityEngine.Object.Destroy(camera.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();
        player=PlayerController.Create(map.GetRoomCenter(3),()=>paused,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        animation=player.GetComponent<PlayerAnimationController>();origin=player.transform.position;shots=socketChecks=0;maxMuzzleError=0;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        if(!DemoContractValidation.Validate(map))throw new Exception("Active art/content contract failed");
        rows.Clear();rows.AppendLine("time,phase,pose,frame,source,speed,combo,thrust");
        observed=oldFrames=lastShot=0;poses.Clear();idleFrames.Clear();dashFrames.Clear();paused=false;observe=true;
        game.StartCoroutine(Observe());game.StartCoroutine(Guard());return folder;
    }
    static IEnumerator Guard()
    {
        var run=Run();string status="PASS";
        while(true)
        {
            bool more;object current=null;
            try{more=run.MoveNext();if(more)current=run.Current;}
            catch(Exception e){status="FAIL "+e;break;}
            if(!more)break;yield return current;
        }
        if(observationFailure!=null)status="FAIL "+observationFailure;
        File.WriteAllText(folder+"/recording.csv",captureRows.ToString());
        observe=false;StageValidationInput.ReleaseAll();paused=false;Time.timeScale=1;
        File.WriteAllText(folder+"/timeline.csv",rows.ToString());
        File.WriteAllText(folder+"/result.txt",status+"\nObserved="+observed+"; retired/foreign body frames="+oldFrames+"; socketChecks="+socketChecks+"; shots="+shots+"; maxMuzzleError="+maxMuzzleError+"; idle="+idleFrames.Count+"; dash sampled="+dashFrames.Count+"\nReal PlayerController/GameAction movement, sword combo, dash, pause and death on production map; isolated encounter, no campaign/performance claim. Combat body art is explicitly pending; same-source idle/walk stance only. Source six views map to eight input directions.");
        Debug.Log("[Active Sscary] "+status+" "+folder);
    }
    static void Require(bool value,string label){if(!value)throw new Exception(label);}
    static void Input(Vector2 direction,bool attack=false,bool dash=false)
    {
        StageValidationInput.BeginFrame();StageValidationInput.SetAim(direction);
        StageValidationInput.SetHeld(GameAction.MoveRight,direction.x>.1f);StageValidationInput.SetHeld(GameAction.MoveLeft,direction.x<-.1f);
        StageValidationInput.SetHeld(GameAction.MoveUp,direction.y>.1f);StageValidationInput.SetHeld(GameAction.MoveDown,direction.y<-.1f);
        StageValidationInput.SetHeld(GameAction.Attack,attack);StageValidationInput.SetHeld(GameAction.Dash,dash);
    }
    static void Capture(string name){ScreenCapture.CaptureScreenshot(folder+"/"+name+".png");}
    static IEnumerator Run()
    {
        phase="idle";StageValidationInput.SetAim(Vector2.down);yield return new WaitForSeconds(1.7f);Capture("01-hand-idle");
        Require(idleFrames.Count==8,"all eight source idle frames");
        phase="shared-summon";
        var seals=new System.Collections.Generic.List<EnemySpawnTelegraph>();
        foreach(EnemyType type in Enum.GetValues(typeof(EnemyType)))
            seals.Add(EnemySpawnTelegraph.Create((Vector2)origin+new Vector2((seals.Count%3-1)*2.5f,2+(seals.Count/3)*1.8f),type));
        yield return new WaitForSeconds(.35f);
        var sealFrame=seals[0].GetComponent<SpriteRenderer>().sprite;
        foreach(var seal in seals)Require(seal.GetComponent<SpriteRenderer>().sprite==sealFrame,"all monsters share summoning frame");
        Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
        Require(seals[0].GetComponent<SpriteRenderer>().sprite==sealFrame,"summoning pauses");Time.timeScale=1;
        yield return new WaitForSeconds(.5f);
        foreach(var seal in seals)Require(seal.GetComponent<SpriteRenderer>().sprite.name=="35","circle completed before enemy appears");
        yield return new WaitForSeconds(.15f);
        foreach(var seal in seals)seal.Complete();
        yield return new WaitForSeconds(.25f);
        Require(UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>(FindObjectsSortMode.None).Length==0,"summoning cleaned");
        foreach(var family in new[]{WeaponFamily.Longsword,WeaponFamily.Pistol})
        {
            inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            for(var direction=0;direction<8;direction++)
            {
                phase="reset";StageValidationInput.ReleaseAll();player.Teleport(origin);yield return new WaitForSeconds(.15f);
                var angle=direction*Mathf.PI/4;var aim=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                phase=family+"-walk-"+direction;Input(aim,true);yield return new WaitForSeconds(.8f);
                if(direction==3)Capture(family+"-moving");
                phase=family+"-aim-"+direction;StageValidationInput.ReleaseAll();StageValidationInput.SetAim(aim);
                yield return new WaitForSeconds(.2f);
            }
        }
        StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.3f);
        phase="dash";Input(Vector2.left,false,true);yield return null;Input(Vector2.left);
        yield return new WaitForSeconds(.08f);Capture("04-hand-dash");yield return new WaitForSeconds(.3f);
        StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.3f);
        phase="pause";paused=true;Time.timeScale=0;
        var sprite=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;var hand=animation.HandLocalPosition;
        yield return new WaitForSecondsRealtime(.3f);
        Require(sprite==player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite&&hand==animation.HandLocalPosition,"pause holds body and socket");
        paused=false;Time.timeScale=1;
        Require(shots>4,"multiple actual pistol emissions observed");
        Require(maxMuzzleError<.002f,"projectile origin matches rendered barrel "+maxMuzzleError);
        Require(observed>60&&oldFrames==0,"only selected source hero");
        Require(socketChecks>60,"visible weapon socket checks");
        phase="death";player.TakeDamage(99999,Vector2.zero);yield return new WaitForSeconds(1);
        Require(!player.transform.Find("VisualRoot/NativeGroundShadow").GetComponent<SpriteRenderer>().enabled,"shadow disabled at death");
    }
    static IEnumerator Observe()
    {
        while(observe)
        {
            yield return new WaitForEndOfFrame();
            var sr=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var source=AssetDatabase.GetAssetPath(sr.sprite);
            var pivot=player.transform.Find("HeldWeaponPivot");
            if(Vector2.Distance(pivot.localPosition,animation.HandLocalPosition)>=.0001f){observationFailure="visible grip detached";yield break;}
            socketChecks++;
            if(player.IsRangedShotActive && animation.PoseName!="death")
            {
                var barrel=pivot.Find("HeldWeaponSprite").GetComponent<SpriteRenderer>();
                if(RangedWeaponArt.TryGet(player.EquippedWeapon.Id,out var landmarks))
                {
                    // Check only the emission frame, before later body movement can legitimately move the gun.
                    if(lastShot!=player.AttackAnimationSequence)
                    {
                        lastShot=player.AttackAnimationSequence;shots++;
                        var pixel=new Vector2(landmarks.Muzzle.x,barrel.sprite.rect.height-landmarks.Muzzle.y)-barrel.sprite.pivot;
                        var local=pixel/barrel.sprite.pixelsPerUnit;if(barrel.flipY)local.y=-local.y;
                        var shown=barrel.transform.TransformPoint(local);
                        var emitted=(Vector2)typeof(PlayerController).GetField("committedShotOrigin",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(player);
                        maxMuzzleError=Mathf.Max(maxMuzzleError,Vector2.Distance(shown,emitted));
                    }
                }
            }
            if(Time.realtimeSinceStartupAsDouble>=nextCapture)
            {
                var now=Time.realtimeSinceStartupAsDouble;
                ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+captureFrame.ToString("D6")+".png");
                captureRows.AppendLine(captureFrame+","+(now-captureStart).ToString("F6",CultureInfo.InvariantCulture)+","+phase);
                captureFrame++;nextCapture=now+1.0/12;
            }
            observed++;if(!source.StartsWith("Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/"))oldFrames++;
            poses.Add(animation.PoseName);
            if(phase=="idle")idleFrames.Add(animation.FrameIndex);
            if(animation.PoseName=="dash")dashFrames.Add(animation.FrameIndex);
            rows.AppendLine(string.Join(",",Time.time.ToString("F4",CultureInfo.InvariantCulture),phase,animation.PoseName,animation.FrameIndex,source,player.GetComponent<Rigidbody2D>().linearVelocity.magnitude,player.ActiveComboAnimationStep,player.IsThrustActive));
        }
    }
}





