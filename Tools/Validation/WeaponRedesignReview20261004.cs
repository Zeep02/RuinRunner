using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Supplied-item visual/socket fixture. Never evidence of a normal campaign.
public static class WeaponRedesignReview20261004
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.FlattenHierarchy;
    static GameController game;static string folder;static Vector2 center;static EditorWindow view;
    static Type autoTick;static bool oldTick;static long oldInterval;static int oldSize,newSize;static object sizes;
    static readonly JArray cases=new JArray();static readonly List<string> checks=new List<string>();
    static HashSet<string> filter;
    public static string Main()
    { filter=null; return Start(); }
    public static string Changed()
    { filter=new HashSet<string>{"tide_sword","ember_sword","dragon_slayer","blood_reaver"}; return Start(); }
    public static string Katanas()
    { filter=new HashSet<string>{"training_katana","moon_blade","blood_moon_katana"}; return Start(); }
    public static string Void()
    { filter=new HashSet<string>{"void_revolver"}; return Start(); }
    public static string NewUnique()
    { filter=new HashSet<string>{"returning_edge","gravity_maul","tether_carbine","bastion_shotgun"}; return Start(); }
    static string Start()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-04-weapon-redesign/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        cases.Clear();checks.Clear();game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();
        game.enabled=true;typeof(GameController).GetMethod("BeginRunForActiveSlot",All).Invoke(game,null);
        typeof(StageValidationInput).GetField("enabled",All).SetValue(null,true);typeof(StageValidationInput).GetField("initialized",All).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Application.runInBackground=true;Application.targetFrameRate=60;
        view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Focus();oldSize=(int)view.GetType().GetProperty("selectedSizeIndex",All).GetValue(view);
        var t=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");var instance=t.GetProperty("instance",All).GetValue(null);
        sizes=t.GetMethod("GetGroup",All).Invoke(instance,new[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"),"Standalone")});
        var size=Activator.CreateInstance(Type.GetType("UnityEditor.GameViewSize,UnityEditor"),new object[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"),"FixedResolution"),1280,720,"Weapon source review"});
        newSize=(int)sizes.GetType().GetMethod("GetTotalCount",All).Invoke(sizes,null);sizes.GetType().GetMethod("AddCustomSize",All).Invoke(sizes,new[]{size});view.GetType().GetProperty("selectedSizeIndex",All).SetValue(view,newSize);
        foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()){autoTick=asm.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand");if(autoTick!=null)break;}
        oldTick=(bool)autoTick.GetProperty("IsEnabled",All).GetValue(null);oldInterval=(long)autoTick.GetProperty("CurrentIntervalMs",All).GetValue(null);autoTick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{true,0,false});
        game.CameraFollow.StartCoroutine(Guard());return folder;
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Restore()
    {
        StageValidationInput.ReleaseAll();Time.timeScale=1;game.ReturnToMainMenu();game.enabled=true;
        view.GetType().GetProperty("selectedSizeIndex",All).SetValue(view,oldSize);sizes.GetType().GetMethod("RemoveCustomSize",All).Invoke(sizes,new object[]{newSize});
        autoTick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{oldTick,(int)oldInterval,false});
    }
    static IEnumerator Guard()
    {
        var run=Run();string result="PASS";
        while(true)
        {
            bool next;object value=null;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception e){result="FAIL "+e;break;}
            if(!next)break;yield return value;
        }
        Restore();File.WriteAllText(folder+"/result.txt",result+"\nSupplied items, production held sprite and body, eight aim directions. Static composition/socket checks only; no campaign or animation smoothness claim.\n"+string.Join("\n",checks),new UTF8Encoding(false));
        File.WriteAllText(folder+"/cases.json",cases.ToString(),new UTF8Encoding(false));Debug.Log("[Weapon redesign] "+result+" "+folder);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil",All).SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemyController>()){e.gameObject.SetActive(false);UnityEngine.Object.Destroy(e.gameObject);}
        center=game.DungeonMap.GetRoomCenter(3);game.Player.Teleport(center);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        yield return new WaitForEndOfFrame();Require(Screen.width==1280&&Screen.height==720,"Native capture resolution");
        var seen=new HashSet<string>();var changed=new HashSet<string>();
        foreach(var row in JArray.Parse(File.ReadAllText("ArtDirection/Review-20261004/weapon-redesign/accepted-selection.json",Encoding.UTF8)))changed.Add((string)row["id"]);
        foreach(var def in WeaponLootTable.All)
        {
            if(filter!=null&&!filter.Contains(def.Id))continue;
            Require(seen.Add(def.Id),"Duplicate catalog ID "+def.Id);Require(def.Family!=WeaponFamily.Bow,"Retired bow in normal rewards");
            var bag=game.Inventory;bag.Reset();Require(bag.AddWeapon(def),"Could not equip "+def.Id);game.Player.EquipWeapon(bag.EquippedWeapon);
            var sprite=PixelArtAtlas.GetHeldWeaponSprite(def);var icon=ItemIconLibrary.Get(def.Id,InventoryIconKind.Weapon);
            Require(sprite!=null&&sprite.texture==icon,"Held/icon source mismatch "+def.Id);
            Require((!changed.Contains(def.Id)||sprite.pixelsPerUnit==32)&&icon.filterMode==FilterMode.Point&&icon.mipmapCount==1,"Pixel import mismatch "+def.Id);
            Require(NativeWeaponArt.TryGet(def.Id,out var pose),"Missing native pose "+def.Id);
            Require(Vector2.Distance(sprite.pivot,new Vector2(pose.Grip.x,sprite.rect.height-pose.Grip.y))<.001f,"Imported grip mismatch "+def.Id);
            if(def.AttackType==WeaponAttackType.Ranged)Require(RangedWeaponArt.TryGet(def.Id,out var unused),"Missing muzzle "+def.Id);
            var sheet=new Texture2D(960,480,TextureFormat.RGB24,false);sheet.SetPixels32(new Color32[960*480]);
            for(int d=0;d<8;d++)
            {
                StageValidationInput.ReleaseAll();StageValidationInput.SetAim(new Vector2(Mathf.Sin(d*Mathf.PI/4),Mathf.Cos(d*Mathf.PI/4)));
                yield return new WaitForSeconds(.26f);view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame();
                var pivot=game.Player.transform.Find("HeldWeaponPivot");var body=game.Player.GetComponent<PlayerAnimationController>();
                Require(Vector2.Distance(pivot.localPosition,body.HandLocalPosition)<.001f,"Hand mismatch "+def.Id+"/"+d);
                Require(Mathf.Approximately(pivot.localScale.x,1),"Non-native scale "+def.Id);
                var image=ScreenCapture.CaptureScreenshotAsTexture();var screen=Camera.main.WorldToScreenPoint(game.Player.transform.position);
                int x=Mathf.Clamp(Mathf.RoundToInt(screen.x)-120,0,image.width-240),y=Mathf.Clamp(Mathf.RoundToInt(screen.y)-88,0,image.height-240);
                sheet.SetPixels(d%4*240,(1-d/4)*240,240,240,image.GetPixels(x,y,240,240));
                if(d==2&&(def.Id=="iron_greatsword"||def.Id=="training_katana"||def.Id=="coil_rifle"))File.WriteAllBytes(folder+"/full-"+def.Id+".png",image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
                cases.Add(new JObject{{"id",def.Id},{"direction",d},{"grip",new JArray(pose.Grip.x,pose.Grip.y)},{"source",AssetDatabase.GetAssetPath(sprite)},{"ppu",sprite.pixelsPerUnit},{"scale",pivot.localScale.x}});
            }
            sheet.Apply();File.WriteAllBytes(folder+"/views-"+def.Id+".png",sheet.EncodeToPNG());UnityEngine.Object.Destroy(sheet);
            checks.Add("PASS "+def.Id+" eight directions, same held/icon PNG, grip pivot and native scale");
            File.WriteAllText(folder+"/progress.json",new JObject{{"reviewedWeapons",seen.Count},{"cases",cases.Count},{"last",def.Id}}.ToString(),new UTF8Encoding(false));
        }
        if(filter!=null){Require(seen.SetEquals(filter),"Filtered replacement coverage");yield break;}
        // Exercise the actual normal reward selection, preserving Unity's RNG state.
        var random=UnityEngine.Random.state;var rolled=new HashSet<string>();
        try{UnityEngine.Random.InitState(20261004);for(int i=0;i<18000;i++)rolled.Add(WeaponLootTable.Roll(i%8).Id);}
        finally{UnityEngine.Random.state=random;}
        Require(rolled.Count==seen.Count,"Some registered weapons cannot appear in normal rewards");
        checks.Add("PASS all "+seen.Count+" catalog weapons reached through normal rarity rolls; RNG state restored");
    }
}
