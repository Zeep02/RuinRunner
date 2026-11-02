using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Staged equipment and isolated arena; attacks/movement/actions use real GameAction input.
public static class CombatFeelFixture
{
    static string folder,phase;static PlayerController player;static InventorySystem inventory;static GameController game;
    static CameraFollow follow;static DungeonMap map;static bool recording;static int frame;
    static readonly StringBuilder report=new StringBuilder(),rows=new StringBuilder();
    static readonly HashSet<string> effects=new HashSet<string>();
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);report.AppendLine("PASS "+message);}
    static void Input(GameAction action,bool held){StageValidationInput.BeginFrame();StageValidationInput.SetHeld(action,held);}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-combat-feel/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        if(game.CameraFollow!=null){game.CameraFollow.enabled=false;var c=game.CameraFollow.GetComponent<Camera>();if(c!=null)c.enabled=false;var listener=game.CameraFollow.GetComponent<AudioListener>();if(listener!=null)listener.enabled=false;}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        follow=CameraFollow.Create();player=PlayerController.Create(map.GetRoomCenter(3),()=>false,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        frame=0;effects.Clear();report.Clear();rows.Clear();rows.AppendLine("frame,realtime,phase,x,y,vx,vy,step,motion,progress,contacts,effects");
        follow.StartCoroutine(Guard(Run()));return folder;
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        var stack=new Stack<IEnumerator>();stack.Push(routine);string failure=null;
        while(stack.Count>0)
        {
            object current=null;bool next=false;
            try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}
            catch(Exception e){failure=e.ToString();Debug.LogException(e);break;}
            if(!next){stack.Pop();continue;}
            if(current is IEnumerator nested){stack.Push(nested);continue;}
            yield return current;
        }
        recording=false;StageValidationInput.ReleaseAll();Time.timeScale=1;
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
        File.WriteAllText(folder+"/result.txt",(failure==null?"PASS":"FAIL "+failure)+"\n"+report+"\nObserved source Sprites:\n"+string.Join("\n",effects.OrderBy(x=>x)));
        File.WriteAllText(folder+"/scope.txt","Isolated real stage0; staged weapons, actors repositioned between cases. Movement/primary/right-click/dash use real GameAction inputs. No enemies in main sequence; no claim of campaign completion, balance or performance. CSV contains real timestamps; screenshots may miss authored frames at low capture rates. Menu cleanup uses a real transient GameController session, separately from the staged actors. Visual inspection required.");
        Debug.Log("[Combat feel] "+(failure==null?"PASS":"FAIL")+" "+folder);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.4f);
        // Screenshot PNG writes stall the Editor. Measure input/contact latency
        // without capture first, then record the same real input sequence separately.
        foreach(var capture in new[]{false,true})
        {
        recording=capture;if(capture)follow.StartCoroutine(Record());
        foreach(var family in new[]{WeaponFamily.Longsword,WeaponFamily.Dagger,WeaponFamily.Katana,WeaponFamily.Greatsword,WeaponFamily.Mace})
        {
            StageValidationInput.ReleaseAll();phase=family+"-equip";
            inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            player.Teleport(map.GetRoomCenter(3)+new Vector2(-5,-1));yield return new WaitForSeconds(.55f);
            StageValidationInput.SetAim(Vector2.up);Input(GameAction.MoveRight,true);yield return new WaitForSeconds(.22f);
            phase=family+"-moving-combo";var profile=MeleeComboCatalog.Get(player.EquippedWeapon);
            var startContacts=player.MeleeContactsResolved;var sequence=player.AttackAnimationSequence;
            var observed=new List<int>();var minSpeed=float.PositiveInfinity;var thrustStart=0f;var thrustContact=-1f;var limit=Time.time+4;
            Input(GameAction.Attack,true);
            while(player.MeleeContactsResolved<startContacts+profile.LightSteps.Length&&Time.time<limit)
            {
                yield return null;
                StageValidationInput.BeginFrame(); // KeyDown is a one-frame edge, not the held state.
                if(!capture&&family==WeaponFamily.Longsword)report.AppendLine("TIMING t="+Time.time.ToString("F6")+" dt="+Time.deltaTime.ToString("F6")+" progress="+player.AttackAnimationProgress.ToString("F6")+" step="+player.ActiveComboAnimationStep+" contacts="+player.MeleeContactsResolved);
                if(player.AttackAnimationSequence!=sequence)
                {
                    sequence=player.AttackAnimationSequence;observed.Add(player.ActiveComboAnimationStep);
                    if(player.IsThrustActive)thrustStart=Time.time;
                }
                if(player.IsThrustActive)
                {
                    minSpeed=Mathf.Min(minSpeed,player.GetComponent<Rigidbody2D>().linearVelocity.x);
                    if(player.MeleeContactsResolved==startContacts+profile.LightSteps.Length)thrustContact=Time.time-thrustStart;
                }
            }
            Input(GameAction.Attack,false);
            while(player.IsMeleeSwingActive&&Time.time<limit)
            {if(player.IsThrustActive)minSpeed=Mathf.Min(minSpeed,player.GetComponent<Rigidbody2D>().linearVelocity.x);yield return null;}
            Input(GameAction.MoveRight,false);
            Require(observed.SequenceEqual(Enumerable.Range(0,profile.LightSteps.Length)),family+" ordered full combo "+string.Join("/",observed));
            Require(player.MeleeContactsResolved==startContacts+profile.LightSteps.Length,family+" one contact per step, no extra attack after release");
            if(family==WeaponFamily.Longsword)
            {
                Require(minSpeed>=player.Stats.MoveSpeed*.85f,"Longsword thrust retains strafe speed min="+minSpeed.ToString("F3"));
                if(!capture)Require(thrustContact>=0&&thrustContact<=.19f,"Unrecorded Longsword third contact latency="+thrustContact.ToString("F3")+"s");
                else report.AppendLine("CAPTURE Longsword third contact latency="+thrustContact.ToString("F3")+"s (PNG capture overhead; not a latency pass)");
            }
            yield return new WaitForSeconds(.65f);phase=family+"-right-click";
            var before=player.MeleeContactsResolved;Input(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();
            if(family==WeaponFamily.Longsword)Require(player.IsGuarding,"Longsword right-click enters guard");
            if(family==WeaponFamily.Katana)Require(player.IsParrying,"Katana right-click enters parry");
            yield return new WaitForSeconds(family==WeaponFamily.Greatsword?.4f:.1f);Input(GameAction.Guard,false);
            yield return new WaitForSeconds(.6f);
            if(family==WeaponFamily.Dagger||family==WeaponFamily.Greatsword)Require(player.MeleeContactsResolved==before+1,family+" right-click attack resolves once");
        }
        }
        // Cancel before contact. A canceled attack must not fire its delayed effect/damage.
        phase="dash-cancel";StageValidationInput.ReleaseAll();inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));player.EquipWeapon(inventory.EquippedWeapon);
        player.Teleport(map.GetRoomCenter(3));yield return new WaitForSeconds(.6f);var contacts=player.MeleeContactsResolved;
        Input(GameAction.Attack,true);yield return null;Input(GameAction.Attack,false);Input(GameAction.Dash,true);yield return null;Input(GameAction.Dash,false);
        yield return new WaitForSeconds(.5f);Require(player.MeleeContactsResolved==contacts,"Dash cancels pre-contact damage");
        foreach(var vertical in new[]{false,true})
        {
            phase=vertical?"seal-vertical-travel":"seal-horizontal-travel";
            var axis=vertical?Vector2.up:Vector2.right;var center=map.GetRoomCenter(3);
            player.Teleport(center-axis*1.4f);var seal=EncounterBarrier.Create(map.Root.transform,center,vertical,true,4.5f);
            var move=vertical?GameAction.MoveUp:GameAction.MoveRight;Input(move,true);yield return new WaitForSeconds(.5f);
            var feet=player.GetComponent<CapsuleCollider2D>().bounds;var boundary=seal.GetComponent<BoxCollider2D>().bounds;
            Require(vertical?feet.max.y<=boundary.min.y+.025f:feet.max.x<=boundary.min.x+.025f,"Closed seal blocks actual foot collider "+phase);
            seal.SetClosed(false);Require(!seal.GetComponent<BoxCollider2D>().enabled,"Seal unlock immediately disables collision "+phase);
            yield return new WaitForSeconds(.45f);Require(Vector2.Dot((Vector2)player.transform.position-center,axis)>.3f,"Open seal can be crossed "+phase);
            Input(move,false);seal.SetClosed(true);Time.timeScale=0;var images=seal.GetComponentsInChildren<SpriteRenderer>().Select(s=>s.sprite).ToArray();
            yield return new WaitForSecondsRealtime(.2f);
            Require(images.SequenceEqual(seal.GetComponentsInChildren<SpriteRenderer>().Select(s=>s.sprite)),"Seal animation pauses "+phase);
            Time.timeScale=1;UnityEngine.Object.Destroy(seal.gameObject);yield return new WaitForSeconds(.25f);
        }
        recording=false;StageValidationInput.ReleaseAll();yield return new WaitForSecondsRealtime(.15f);
        phase="real-session-menu-cleanup";game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(game,null);
        yield return new WaitForSecondsRealtime(.6f);Require(game.IsRunActive,"Transient real run started for menu cleanup");
        MeleeSlashEffect.Create(Vector2.zero,0,Vector2.one,WeaponFamily.Longsword,false,false);
        MeleeThrustEffect.Create(Vector2.zero,0,1000,false);ExternalCombatEffect.Create("Impact",Vector2.zero,0,1,1000);
        Time.timeScale=0;game.ReturnToMainMenu();yield return null;yield return null;
        Require(!game.IsRunActive&&UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>().Length==0&&UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>().Length==0&&UnityEngine.Object.FindObjectsByType<ExternalCombatEffect>().Length==0,"Paused menu return clears all active melee effect types");
    }
    static IEnumerator Record()
    {
        while(recording)
        {
            yield return new WaitForEndOfFrame();var paths=new List<string>();
            foreach(var component in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>().Cast<Component>().Concat(UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>()).Concat(UnityEngine.Object.FindObjectsByType<ExternalCombatEffect>()))
            {var view=component.GetComponentInChildren<SpriteRenderer>();if(view!=null&&view.enabled&&view.sprite!=null){var path=AssetDatabase.GetAssetPath(view.sprite);paths.Add(path);effects.Add(path);}else if(component is MeleeThrustEffect)paths.Add("Engine/ThrustStreak");}
            var pos=player.transform.position;var v=player.GetComponent<Rigidbody2D>().linearVelocity;
            rows.AppendLine(string.Join(",",frame,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,pos.x,pos.y,v.x,v.y,player.ActiveComboAnimationStep,player.ActiveMeleeMotion,player.AttackAnimationProgress,player.MeleeContactsResolved,string.Join(";",paths)));
            ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+(frame++).ToString("D6")+".png");yield return new WaitForSecondsRealtime(.03f);
        }
    }
}
