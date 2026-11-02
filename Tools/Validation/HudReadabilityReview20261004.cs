using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using RuinRunner;
using Newtonsoft.Json.Linq;
// Supplied state for HUD review. No user saves and no campaign-completion claim.
public static class HudReadabilityReview20261004
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    static string folder; static GameController game; static EditorWindow view; static object sizes;
    static int oldSize,oldCustomCount; static Type autoTick; static bool oldTickEnabled; static long oldTickInterval;
    static readonly List<int> customSizes=new List<int>(); static readonly List<string> checks=new List<string>();
    static void Require(bool okay,string label){if(!okay)throw new Exception(label);checks.Add(label);}
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-04-hud-readability/editor-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder);
        checks.Clear(); customSizes.Clear();
        game = UnityEngine.Object.FindAnyObjectByType<GameController>(); game.ReturnToMainMenu(); game.SaveSlots.OpenTransientForTests();
        game.enabled = true; foreach (var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>()) hud.enabled = true;
        typeof(GameController).GetMethod("BeginRunForActiveSlot", Private).Invoke(game, null);
        typeof(StageValidationInput).GetField("enabled", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        typeof(StageValidationInput).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        StageValidationInput.ReleaseAll(); Application.runInBackground = true; Application.targetFrameRate = 60; Time.captureFramerate = 0;
        view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.Focus();
        oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", All).GetValue(view);
        var t = Type.GetType("UnityEditor.GameViewSizes,UnityEditor"); var instance = t.GetProperty("instance", All).GetValue(null);
        sizes = t.GetMethod("GetGroup", All).Invoke(instance, new[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"), "Standalone") });
        oldCustomCount = (int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { autoTick = assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand"); if (autoTick != null) break; }
        if (autoTick == null) throw new Exception("Installed editor auto-tick command not found");
        oldTickEnabled = (bool)autoTick.GetProperty("IsEnabled", All).GetValue(null);
        oldTickInterval = (long)autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null);
        autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { true, 0, false });
        game.CameraFollow.StartCoroutine(Guard(Run())); return folder;
    }
    static void Resolution(int width, int height)
    {
        var t = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var size = Activator.CreateInstance(t, new object[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"), "FixedResolution"), width, height, "RuinRunner review " + width + "x" + height });
        int index = (int)sizes.GetType().GetMethod("GetTotalCount", All).Invoke(sizes, null);
        sizes.GetType().GetMethod("AddCustomSize", All).Invoke(sizes, new[] { size }); customSizes.Add(index);
        view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, index); view.Repaint();
    }
    static void RestoreView()
    {
        if (autoTick != null) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTickEnabled, (int)oldTickInterval, false });
        if (view == null || sizes == null) return;
        view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, oldSize);
        for (int i = customSizes.Count - 1; i >= 0; i--)
        {
            sizes.GetType().GetMethod("RemoveCustomSize", All).Invoke(sizes, new object[] { customSizes[i] });
        }
        customSizes.Clear();
        Require((int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null) == oldCustomCount,
            "review restores original Game View size selection and removes only its own temporary presets");
    }

    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            bool next;object value=null;
            try {next=run.MoveNext();if(next)value=run.Current;}
            catch(Exception ex){Time.timeScale=1;RestoreView();File.WriteAllText(folder+"/result.txt","FAIL "+ex);Debug.LogException(ex);yield break;}
            if(!next)break;yield return value;
        }
        Time.timeScale=1;game.ReturnToMainMenu();game.enabled=true;RestoreView();
        File.WriteAllText(folder+"/result.txt","PASS staged HUD capture. Not campaign or motion validation.\n"+string.Join("\n",checks),new UTF8Encoding(false));
    }
    static void Capture(string name)
    {
        var t=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(folder+"/"+name+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;
        typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        var previous=game.DungeonMap;UnityEngine.Random.InitState(9100);
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,2);
        typeof(GameController).GetProperty("DungeonMap").SetValue(game,map);typeof(GameController).GetProperty("CurrentStageIndex").SetValue(game,2);
        UnityEngine.Object.Destroy(previous.Root);yield return null;
        var room=map.RoomCount-1;var center=map.GetRoomCenter(room);
        game.Player.Teleport(center+Vector2.down*2);game.Player.SetStageClearSafety(true);
        game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        var boss=EnemyController.Create(EnemyType.SealHeart,center,game.Player,()=>game.IsPaused,_=>{},game.CameraFollow);
        boss.enabled=false;boss.ConfigureArena(new Rect(center-map.GetRoom(room).Size*.5f,map.GetRoom(room).Size));
        typeof(GameController).GetProperty("Boss").SetValue(game,boss);
        typeof(GameController).GetProperty("CurrentRoom").SetValue(game,room);
        game.Inventory.Reset();foreach(var id in new[]{"rust_sword","storm_rifle","dragon_cannon"})game.Inventory.AddWeapon(WeaponLootTable.Find(id));
        game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));game.Inventory.ActiveSlot.Assign(game.Inventory.EquippedWeapon,12,0);
        var reports=new JArray();
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1440,900)})
        {
            Resolution(size.x,size.y);Time.timeScale=1;yield return null;yield return new WaitForSeconds(.3f);
            boss.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("SealCross",Private).Invoke(boss,null));
            yield return new WaitForSeconds(1.17f);Time.timeScale=0;yield return new WaitForEndOfFrame();
            Require(Screen.width==size.x&&Screen.height==size.y,"native output "+size);
            Capture(size.x+"x"+size.y+"-bright-boss-name");
            var hud=UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
            var style=(GUIStyle)typeof(PrototypeHud).GetField("quickNameStyle",Private).GetValue(hud);
            var names=new JArray();float max=0;string longest=null;
            foreach(var weapon in WeaponLootTable.All)
            {
                float w=style.CalcSize(new GUIContent(weapon.Name)).x;
                Require(w<=432,"name fits 432px content region: "+weapon.Id);
                foreach(var c in weapon.Name)Require(char.IsWhiteSpace(c)||style.font.HasCharacter(c),"name glyph "+c);
                names.Add(new JObject{{"id",weapon.Id},{"name",weapon.Name},{"width",w}});
                if(w>max){max=w;longest=weapon.Id;}
            }
            game.Inventory.Reset();foreach(var id in new[]{longest,"storm_rifle","dragon_cannon"})game.Inventory.AddWeapon(WeaponLootTable.Find(id));
            game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(0));yield return new WaitForEndOfFrame();Capture(size.x+"x"+size.y+"-longest-name");
            game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));game.Inventory.ActiveSlot.Assign(game.Inventory.EquippedWeapon,12,0);
            Require(game.Inventory.StartReload(),"reload starts "+size);game.Inventory.Tick(game.Inventory.EquippedWeapon.ReloadSeconds*.4f);
            yield return new WaitForEndOfFrame();Capture(size.x+"x"+size.y+"-reload-quickbar");
            var ammo=game.Player.transform.Find("AmmoGauge");var dash=game.Player.transform.Find("DashCharges");
            Require(Mathf.Abs(ammo.position.x-dash.position.x)<.001f && ammo.GetChild(0).GetComponent<SpriteRenderer>().bounds.max.y<dash.GetComponent<MeshRenderer>().bounds.min.y,"reload below centered dash "+size);
            typeof(GameController).GetProperty("IsInventoryOpen").SetValue(game,true);yield return new WaitForEndOfFrame();Capture(size.x+"x"+size.y+"-inventory");
            typeof(GameController).GetProperty("IsInventoryOpen").SetValue(game,false);
            reports.Add(new JObject{{"width",size.x},{"height",size.y},{"layoutScale",GameTypography.LayoutScale},{"longestWeapon",longest},{"maxNameWidth",max},{"names",names}});
            Time.timeScale=1;yield return new WaitForSeconds(2f);
            game.Inventory.Reset();foreach(var id in new[]{"rust_sword","storm_rifle","dragon_cannon"})game.Inventory.AddWeapon(WeaponLootTable.Find(id));
            game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));game.Inventory.ActiveSlot.Assign(game.Inventory.EquippedWeapon,12,0);
        }
        File.WriteAllText(folder+"/layout.json",reports.ToString(),new UTF8Encoding(false));
    }
}