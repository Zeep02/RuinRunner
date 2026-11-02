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

// Real AI and attack clocks in a staged arena. Not a normal campaign or FPS test.
public static class ExternalCreatureFixture
{
    static string folder;static GameController game;static PlayerController player;static CameraFollow follow;
    static readonly StringBuilder rows=new StringBuilder();static readonly List<string> checks=new List<string>();
    static readonly Dictionary<Sprite,float> opaqueTops=new Dictionary<Sprite,float>();
    static int frame;static bool paused;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-22-external-creatures/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var cam in UnityEngine.Object.FindObjectsByType<CameraFollow>()){cam.gameObject.SetActive(false);UnityEngine.Object.Destroy(cam.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        follow=CameraFollow.Create();player=PlayerController.Create(Vector2.zero,()=>paused,()=>false,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=null;follow.FocusRoom(Vector2.zero,new Vector2(20,11.25f),true);
        rows.Clear();rows.AppendLine("frame,realtime,phase,pose,acting,attackPhase,velocity,health");checks.Clear();opaqueTops.Clear();frame=0;paused=false;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.StartCoroutine(Guard(Run()));return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack=new Stack<IEnumerator>();stack.Push(run);string failure=null;
        while(stack.Count>0)
        {
            object current=null;bool next=false;
            try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}
            catch(Exception e){failure=e.ToString();Debug.LogException(e);break;}
            if(!next){stack.Pop();continue;}
            if(current is IEnumerator nested){stack.Push(nested);continue;}
            yield return current;
        }
        paused=false;Time.timeScale=1;StageValidationInput.ReleaseAll();
        File.WriteAllText(folder+"/recording.csv",rows.ToString());File.WriteAllLines(folder+"/checks.txt",checks);
        File.WriteAllText(folder+"/result.txt",failure==null?"PASS":"FAIL "+failure);
        Debug.Log("[External Creature] "+(failure==null?"PASS":"FAIL")+" "+folder);
    }
    static void Check(bool condition,string description)
    {checks.Add((condition?"PASS ":"FAIL ")+description);if(!condition)throw new Exception(description);}
    static float OpaqueTop(Sprite sprite)
    {
        if(opaqueTops.TryGetValue(sprite,out var top))return top;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))))throw new Exception("PNG decode failed");
            var pixels=texture.GetPixels32();var highest=-1;
            for(var y=texture.height-1;y>=0&&highest<0;y--)
                for(var x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a!=0){highest=y;break;}
            top=(highest+1-sprite.pivot.y)/sprite.pixelsPerUnit;
            opaqueTops[sprite]=top;return top;
        }
        finally{UnityEngine.Object.Destroy(texture);}
    }
    static void CheckBar(EnemyController actor,Sprite pose,string label)
    {
        var visual=actor.transform.Find("VisualRoot");
        var bar=actor.transform.Find("HealthBack").GetComponent<SpriteRenderer>();
        var top=visual.TransformPoint(new Vector3(0,OpaqueTop(pose),0)).y;
        var clearance=bar.bounds.min.y-top;
        Check(clearance>=4f/32f-.001f,label+" health bar clears opaque pose by >=4 source pixels ("+clearance.ToString("F4",CultureInfo.InvariantCulture)+")");
    }
    static IEnumerator Capture(EnemyController actor,string phase)
    {
        yield return new WaitForEndOfFrame();
        if(actor==null)yield break;
        if(!player.IsDead)
        {
            var playerBody=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            Check(playerBody.enabled&&playerBody.color.a>=.719f,phase+" player remains visible during damage immunity");
            Check(player.GetComponent<HeldWeaponView>().IsVisible,phase+" held weapon remains visible");
            if(player.InvincibilityTicksRemaining==0)Check(playerBody.color==Color.white,phase+" immunity ending restores original palette");
        }
        var sprite=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;
        if(!actor.IsDead)CheckBar(actor,sprite,phase);
        ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+frame.ToString("D6")+".png");
        rows.AppendLine(string.Join(",",frame++,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,
            AssetDatabase.GetAssetPath(sprite),actor.IsActing,actor.AttackVisualPhase,actor.Velocity.magnitude,player.Health));
        yield return new WaitForSecondsRealtime(.04f);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.25f);
        foreach(var type in new[]{EnemyType.Grunt,EnemyType.Shooter,EnemyType.Charger,EnemyType.Leaper,EnemyType.Tidecaller,EnemyType.Sentinel})
        {
            player.Stats.Heal(player.MaxHealth);var pb=player.GetComponent<Rigidbody2D>();pb.position=Vector2.zero;pb.linearVelocity=Vector2.zero;
            player.transform.position=Vector3.zero;yield return new WaitForSeconds(.2f);
            var actor=EnemyController.Create(type,new Vector2(type==EnemyType.Shooter||type==EnemyType.Tidecaller?-5:4,0),player,()=>paused,_=>{},follow);
            var visual=actor.transform.Find("VisualRoot");var renderer=visual.GetComponent<SpriteRenderer>();
            if(type==EnemyType.Grunt)
            {
                var healthBefore=player.Health;
                player.TakeDamage(1,Vector2.zero);
                Check(player.Health<healthBefore&&player.InvincibilityTicksRemaining==PlayerController.HitInvincibilityTicks,"controlled hit starts original immunity duration");
                var hitHealth=player.Health;player.TakeDamage(20,Vector2.zero);
                Check(player.Health==hitHealth,"repeat hit cannot bypass damage immunity");
                yield return Capture(actor,"controlled-player-hurt");
                var playerBody=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                var frozenColor=playerBody.color;var frozenTicks=player.InvincibilityTicksRemaining;
                paused=true;Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
                Check(playerBody.color==frozenColor&&player.InvincibilityTicksRemaining==frozenTicks,"pause freezes player damage tint and immunity clock together");
                paused=false;Time.timeScale=.35f;
                yield return new WaitForSecondsRealtime(.35f);
                Check(player.InvincibilityTicksRemaining<frozenTicks&&player.InvincibilityTicksRemaining>0,"slow motion advances immunity without ending early");
                yield return Capture(actor,"slow-motion-player-hurt");
                Time.timeScale=1;
            }
            Check(Mathf.Abs(visual.lossyScale.x-1)<.001f&&Mathf.Abs(visual.lossyScale.y-1)<.001f,type+" native uniform world scale");
            var known=new HashSet<Sprite>();
            for(var dir=0;dir<4;dir++)for(var state=0;state<4;state++)
            {
                var poses=ExternalCreatureArt.Frames(type,state,dir);if(poses==null)continue;
                foreach(var pose in poses)
                {
                    known.Add(pose);Check(pose.pixelsPerUnit==32&&AssetDatabase.GetAssetPath(pose).Contains("/External/"),type+" external32PPU "+state+"/"+dir+"/"+pose.name);
                    if(state!=3)CheckBar(actor,pose,type+" "+state+"/"+dir+"/"+pose.name);
                }
            }
            for(var dir=0;dir<4;dir++)
            {
                var hurt=ExternalCreatureArt.HurtFrames(type,dir);
                if(hurt!=null)foreach(var pose in hurt)CheckBar(actor,pose,type+" hurt/"+dir+"/"+pose.name);
            }
            var phases=new HashSet<int>();var observed=new HashSet<Sprite>();var end=Time.time+5.5f;
            while(Time.time<end&&!player.IsDead)
            {
                if(actor.IsActing)phases.Add(actor.AttackVisualPhase);observed.Add(renderer.sprite);
                Check(known.Contains(renderer.sprite),type+" no generated fallback");
                yield return Capture(actor,type.ToString());
            }
            Check(phases.Count==3,type+" actual AI anticipation/contact/recovery");
            Check(observed.Count>=6,type+" at least six original poses observed");
            var frozen=renderer.sprite;paused=true;Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);
            Check(renderer.sprite==frozen,type+" pause freezes sprite");paused=false;Time.timeScale=1;
            actor.TakeDamage(1,Vector2.zero);yield return Capture(actor,type+" hurt");
            actor.TakeDamage(actor.MaxHealth*10,Vector2.zero);Check(actor.IsDead&& !actor.DamageCollider.enabled,type+" death disables hurtbox immediately");
            var deathUntil=Time.time+1.4f;
            while(actor!=null&&Time.time<deathUntil)yield return Capture(actor,type+" death");
            Check(actor==null,type+" death finishes");
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<EnemyProjectile>())UnityEngine.Object.Destroy(projectile.gameObject);
            Check(!player.IsDead,type+" player survived staged encounter");
        }
        yield return new WaitForSecondsRealtime(.3f);
        File.WriteAllText(folder+"/recording.csv",rows.ToString());File.WriteAllLines(folder+"/checks.txt",checks);
        File.WriteAllText(folder+"/scope.txt","Six sequential authored enemies use normal AI/physics/attack clocks. Player reset/healed between actors; one-point and lethal damage injected only for hurt/death inspection. No action phase injection. Staged render/animation regression, not normal campaign completion or performance evidence.");
        Debug.Log("[External Creature] PASS "+folder);
    }
}
