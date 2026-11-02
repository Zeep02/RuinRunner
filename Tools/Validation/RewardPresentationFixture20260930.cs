using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Staged art/interaction review only, with transient save data. Not campaign evidence.
public static class RewardPresentationFixture20260930
{
    static string folder;static GameController game;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly MethodInfo Open=typeof(RewardChest).GetMethod("Open",Private);
    static readonly List<string> checks=new List<string>();
    static readonly StringBuilder recording=new StringBuilder("frame,realtime,phase\n");
    sealed class RecordedFrame { public int index,width,height; public Color32[] pixels; }
    static readonly List<RecordedFrame> buffered=new List<RecordedFrame>();
    static int sample;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-30-reward-presentation/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder+"/frames");checks.Clear();sample=0;buffered.Clear();recording.Clear();recording.AppendLine("frame,realtime,phase");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();
        game.enabled=true;typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Application.runInBackground=true;Application.targetFrameRate=60;Time.captureFramerate=0;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static void Require(bool condition,string label){if(!condition)throw new Exception(label);checks.Add("PASS "+label);}
    static void Capture(string name)
    {var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);}
    static IEnumerator Record(float seconds,string phase)
    {
        var until=Time.realtimeSinceStartup+seconds;
        while(Time.realtimeSinceStartup<until)
        {
            yield return new WaitForEndOfFrame();var time=Time.realtimeSinceStartupAsDouble;
            var frame=ScreenCapture.CaptureScreenshotAsTexture();
            buffered.Add(new RecordedFrame{index=sample,width=frame.width,height=frame.height,pixels=frame.GetPixels32()});
            UnityEngine.Object.Destroy(frame);
            recording.AppendLine(sample+++","+time.ToString("F6",CultureInfo.InvariantCulture)+","+phase);
        }
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            object value=null;bool next;
            try{next=run.MoveNext();if(next)value=run.Current;}
            catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e+"\n"+string.Join("\n",checks),new UTF8Encoding(false));Debug.LogException(e);yield break;}
            if(!next)break;yield return value;
        }
        // Encode after all motion and assertions, so PNG compression does not stall the recorded animation.
        foreach(var frame in buffered)
        {
            var texture=new Texture2D(frame.width,frame.height,TextureFormat.RGBA32,false);
            texture.SetPixels32(frame.pixels);texture.Apply(false,false);
            File.WriteAllBytes(folder+"/frames/frame-"+frame.index.ToString("D6")+".png",texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);frame.pixels=null;
        }
        buffered.Clear();
        File.WriteAllText(folder+"/recording.csv",recording.ToString(),new UTF8Encoding(false));
        File.WriteAllText(folder+"/result.txt","PASS staged reward art and interaction review; not a normal campaign.\n"+string.Join("\n",checks),new UTF8Encoding(false));
        Debug.Log("[Reward presentation] PASS "+folder);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;
        typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>())UnityEngine.Object.Destroy(enemy.gameObject);
        foreach(var telegraph in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(telegraph.gameObject);
        foreach(var old in UnityEngine.Object.FindObjectsByType<RewardChest>())UnityEngine.Object.Destroy(old.gameObject);
        yield return null;
        var center=game.DungeonMap.GetRoomCenter(0);game.Player.Teleport(center+Vector2.down*1.6f);game.Player.enabled=false;
        var chests=new RewardChest[5];var loot=new FieldLootPickup[5];var opened=new int[5];var collected=new int[5];
        for(int i=0;i<5;i++)
        {
            var index=i;var grade=(WeaponRarity)i;var position=center+new Vector2((i-2)*2.55f,1.3f);
            WeaponDefinition weapon=null;foreach(var candidate in WeaponLootTable.All)if(candidate.Rarity==grade){weapon=candidate;break;}
            if(weapon==null)throw new Exception("Missing catalogue grade "+grade);
            var sprite=InventoryIconFactory.GetWorldSprite(weapon.Id,weapon.Color,InventoryIconKind.Weapon);
            chests[i]=RewardChest.Create(position,()=>
            {
                opened[index]++;loot[index]=FieldLootPickup.Create(position,weapon.Name,weapon.Color,game.Player,game.Settings,
                    ()=>{collected[index]++;return true;},0,sprite,grade);
            },game.Settings,null,grade);
        }
        yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();Capture("01-five-grades-closed");
        for(int i=0;i<5;i++)Open.Invoke(chests[i],null);
        Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);
        for(int i=0;i<5;i++)Require(opened[i]==0&&chests[i].FrameIndex==0,"pause holds chest and reward "+i);
        Time.timeScale=1;
        var record=Record(1.3f,"five-grade-opening");while(record.MoveNext())yield return record.Current;
        yield return new WaitForEndOfFrame();Capture("02-five-grades-open-and-loot");
        for(int i=0;i<5;i++)
        {
            Require(opened[i]==1&&chests[i].FrameIndex==5,"one reward and final native chest pose "+i);
            Require(loot[i]!=null&&loot[i].Rarity==chests[i].Rarity&&loot[i].IsSettled,"matching item grade and settled reveal "+i);
            Require(Vector3.Distance(loot[i].HaloCenter,loot[i].transform.position)<.001f,"floor marker separate from airborne item "+i);
            Open.Invoke(chests[i],null);
        }
        yield return new WaitForSeconds(.1f);for(int i=0;i<5;i++)Require(opened[i]==1,"repeat interact cannot duplicate chest "+i);
        var wall=new GameObject("Reward fixture wall");wall.layer=GameLayers.Ground;wall.transform.position=center+new Vector2(.95f,-3);
        wall.AddComponent<BoxCollider2D>().size=new Vector2(.15f,3);Physics2D.SyncTransforms();
        var safe=new List<FieldLootPickup>();
        var largeSprite=InventoryIconFactory.GetWorldSprite("scrap_pistol",Color.white,InventoryIconKind.Weapon);
        for(int i=0;i<3;i++)safe.Add(FieldLootPickup.Create(center+new Vector2(.45f,-3),"벽 근접 검증",Color.white,game.Player,game.Settings,()=>false,i,largeSprite));
        foreach(var drop in safe)
        {
            Require(StageNavigation.IsClear(game.DungeonMap.Terrain,drop.LandingPosition),"wall-adjacent drop remains walkable");
            Require(StageNavigation.HasCombatLane(center+new Vector2(.45f,-3),drop.LandingPosition,.25f),"drop never crosses wall");
        }
        for(int i=0;i<safe.Count;i++)for(int j=0;j<i;j++)Require(Vector2.Distance(safe[i].LandingPosition,safe[j].LandingPosition)>=.80f,"separate loot landing positions");
        yield return new WaitForSeconds(.9f);
        foreach(var drop in safe)
        {
            var bounds=drop.transform.Find("ItemSpin/ItemVisual").GetComponent<SpriteRenderer>().bounds;
            Require(Physics2D.OverlapBox(bounds.center,(Vector2)bounds.size+Vector2.one*.10f,0,1<<GameLayers.Ground)==null,"visible large loot icon clears wall");
            foreach(var other in safe)if(other!=drop)
                Require(!bounds.Intersects(other.transform.Find("ItemSpin/ItemVisual").GetComponent<SpriteRenderer>().bounds),"visible loot images do not overlap");
        }
        var embedded=FieldLootPickup.Create(wall.transform.position,"잘못된 생성 지점 복구",Color.white,game.Player,game.Settings,()=>false,0,largeSprite);
        yield return new WaitForSeconds(.6f);
        Require(StageNavigation.IsClear(game.DungeonMap.Terrain,embedded.LandingPosition)&&
            Physics2D.OverlapCircle(embedded.LandingPosition,.3f,1<<GameLayers.Ground)==null,"embedded drop source recovers to accessible floor");
        Require(!embedded.transform.Find("ItemSpin/ItemVisual").GetComponent<SpriteRenderer>().bounds.Intersects(wall.GetComponent<BoxCollider2D>().bounds),"recovered loot clears wall visually");
        UnityEngine.Object.Destroy(embedded.gameObject);
        foreach(var drop in safe)UnityEngine.Object.Destroy(drop.gameObject);UnityEngine.Object.Destroy(wall);
        game.Player.Teleport(loot[2].LandingPosition);yield return null;
        int selected=0;foreach(var drop in loot)if(drop.IsSelected)selected++;
        Require(selected==1,"only nearest item receives prompt and interact");
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Interact,true);StageValidationInput.SetHeld(GameAction.Interact,false);
        yield return null;StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
        int total=0;foreach(var count in collected)total+=count;Require(total==1,"one key press collects exactly one item");
        record=Record(.40f,"pickup");while(record.MoveNext())yield return record.Current;
        yield return new WaitForEndOfFrame();Capture("03-loot-pickup");
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
        yield return null;Require(NativeCombatEffect.ActiveCount==0&&UnityEngine.Object.FindObjectsByType<FieldLootPickup>().Length==0,"session cleanup removes reward effects and items");
    }
}
