using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Supplied inventory/clear-room fixture. Input and transactions are production paths.
// This is not a normal campaign or OS keyboard test.
public static class InventoryKeys20261004
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    const BindingFlags A=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy;
    static GameController game; static GameSettingsService oldSettings; static string folder;
    static EditorWindow view; static object sizes; static int oldSize; static Type tick; static bool oldTick; static int oldInterval;
    static readonly List<int> temporary=new List<int>(); static readonly List<string> checks=new List<string>();
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static void Set(string key,object value)=>typeof(GameController).GetProperty(key).SetValue(game,value);
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-04-normal-growth/keys-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();
        oldSettings=game.Settings;Set("Settings",new GameSettingsService(folder+"/settings.json",false));
        typeof(GameController).GetMethod("BeginRunForActiveSlot",P).Invoke(game,null);
        foreach(var key in new[]{"enabled","initialized"})typeof(StageValidationInput).GetField(key,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        StageValidationInput.ReleaseAll();checks.Clear();temporary.Clear();
        view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Focus();oldSize=(int)view.GetType().GetProperty("selectedSizeIndex",A).GetValue(view);
        var t=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");var instance=t.GetProperty("instance",A).GetValue(null);
        sizes=t.GetMethod("GetGroup",A).Invoke(instance,new[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"),"Standalone")});
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){tick=assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand");if(tick!=null)break;}
        oldTick=(bool)tick.GetProperty("IsEnabled",A).GetValue(null);oldInterval=(int)(long)tick.GetProperty("CurrentIntervalMs",A).GetValue(null);
        tick.GetMethod("SetAutoTick",A).Invoke(null,new object[]{true,0,false});
        game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static void Resolution(int w,int h)
    {
        var t=Type.GetType("UnityEditor.GameViewSize,UnityEditor");var size=Activator.CreateInstance(t,new object[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"),"FixedResolution"),w,h,"Inventory keys QA"});
        int index=(int)sizes.GetType().GetMethod("GetTotalCount",A).Invoke(sizes,null);sizes.GetType().GetMethod("AddCustomSize",A).Invoke(sizes,new[]{size});temporary.Add(index);
        view.GetType().GetProperty("selectedSizeIndex",A).SetValue(view,index);view.Repaint();
    }
    static IEnumerator Tap(GameAction action)
    {
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(action,true);yield return null;
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(action,false);yield return null;
    }
    static void Capture(string label){var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+label+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);}
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.6f);game.StopAllCoroutines();
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>())UnityEngine.Object.Destroy(enemy.gameObject);
        Set("EnemiesAlive",0);typeof(GameController).GetField("waveTransition",P).SetValue(game,false);
        var bag=game.Inventory;bag.Reset();
        for(int i=0;i<4;i++)bag.AddWeapon(WeaponLootTable.Find("rust_sword"));
        bag.AddAccessory(AccessoryLootTable.Find("mana_thread"));bag.AwardMaterials(80,"keyboard-fixture");
        game.Player.EquipWeapon(bag.EquippedWeapon);var first=bag.ActiveSlot.Item;var spare=bag.OwnedWeapons[0];
        yield return Tap(GameAction.Inventory);
        Check(game.IsInventoryOpen&&game.InventoryFocusQuickSlot==0&&game.SelectedEquipment==first,"open selects active quick slot");
        int materials=bag.UpgradeMaterials,cost=first.UpgradeCost;
        yield return Tap(GameAction.Reload);
        Check(first.UpgradeLevel==1&&bag.UpgradeMaterials==materials-cost,"R upgrades selected copy and debits exact materials");
        Check(bag.QuickSlots[1].Item.UpgradeLevel==0&&spare.UpgradeLevel==0,"other copies remain unchanged");
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Reload,true);yield return null;
        for(int i=0;i<30;i++){StageValidationInput.BeginFrame();yield return null;}
        StageValidationInput.SetHeld(GameAction.Reload,false);yield return null;
        Check(first.UpgradeLevel==1,"held upgrade does not repeat after cooldown");
        yield return Tap(GameAction.MoveDown);yield return Tap(GameAction.MoveLeft);
        Check(game.SelectedEquipment==spare&&game.InventoryFocusQuickSlot<0,"down and left reach stored copy");
        yield return Tap(GameAction.Dismantle);Check(game.HasDisassemblyConfirmation,"X requests explicit confirmation");
        yield return Tap(GameAction.Dismantle);Check(game.HasDisassemblyConfirmation&&bag.FindOwned(spare.Identity)!=null,"repeat request cannot confirm");
        yield return Tap(GameAction.Pause);Check(game.IsInventoryOpen&&!game.HasDisassemblyConfirmation,"Escape cancels confirmation but keeps bag open");
        yield return Tap(GameAction.Dismantle);yield return Tap(GameAction.MoveRight);
        Check(!game.HasDisassemblyConfirmation,"selection change cancels pending deletion");
        yield return Tap(GameAction.MoveLeft);yield return Tap(GameAction.Dismantle);
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1440,900)})
        {Resolution(size.x,size.y);yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Capture("confirm-"+size.x+"x"+size.y);}
        materials=bag.UpgradeMaterials;int refund=spare.DisassemblyYield;
        // Reproduce BR: an ordinary loot E press just preceded opening the bag.
        // A suppressed pilot tap must not advance to checking the transaction.
        var pilot=game.gameObject.AddComponent<AutomatedStageValidation>();pilot.enabled=false;
        var pt=typeof(AutomatedStageValidation);
        pt.GetField("game",P).SetValue(pilot,game);pt.GetField("maintenancePhase",P).SetValue(pilot,2);
        pt.GetField("maintenanceIdentity",P).SetValue(pilot,spare.Identity);
        pt.GetField("maintenanceMaterials",P).SetValue(pilot,materials);pt.GetField("maintenanceDelta",P).SetValue(pilot,refund);
        pt.GetField("maintenanceDeadline",P).SetValue(pilot,Time.unscaledTime+10);
        pt.GetField("nextInteractAt",P).SetValue(pilot,Time.unscaledTime+.5f);
        var drive=pt.GetMethod("DriveEquipmentMaintenance",P);
        StageValidationInput.BeginFrame();drive.Invoke(pilot,null);yield return null;
        Check((int)pt.GetField("maintenancePhase",P).GetValue(pilot)==2&&game.HasDisassemblyConfirmation&&bag.FindOwned(spare.Identity)!=null,"recent loot input keeps pilot on confirmation without deleting");
        pt.GetField("nextInteractAt",P).SetValue(pilot,Time.unscaledTime-1);
        StageValidationInput.BeginFrame();drive.Invoke(pilot,null);yield return null;
        Check(bag.FindOwned(spare.Identity)==null&&bag.UpgradeMaterials==materials+refund,"E deletes exact copy and refunds once");
        StageValidationInput.BeginFrame();yield return new WaitForSecondsRealtime(.3f);drive.Invoke(pilot,null);
        Check((int)pt.GetField("disassemblyCount",P).GetValue(pilot)==1,"pilot verifies exactly one transaction after real E press");
        UnityEngine.Object.Destroy(pilot);
        yield return Tap(GameAction.Interact);Check(bag.UpgradeMaterials==materials+refund,"repeated confirm does not duplicate materials");
        yield return Tap(GameAction.MoveUp);yield return Tap(GameAction.MoveRight);
        Check(game.InventoryFocusQuickSlot==1&&game.SelectedEquipment==bag.QuickSlots[1].Item,"quick slot keyboard selection is independent of active combat weapon");
        Set("EnemiesAlive",1);materials=bag.UpgradeMaterials;yield return Tap(GameAction.Reload);
        Check(bag.UpgradeMaterials==materials&&bag.QuickSlots[1].Item.UpgradeLevel==0,"combat blocks upgrade key");Set("EnemiesAlive",0);
        game.Settings.SetBinding(GameAction.Dismantle,KeyCode.F,false);
        Check(game.Settings.GetBinding(GameAction.Dismantle).primary==KeyCode.F&&game.Settings.GetBinding(GameAction.Reload).primary==KeyCode.R,"new binding remaps without disturbing existing actions");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1440,900)})
        {Resolution(size.x,size.y);yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Capture("maintenance-"+size.x+"x"+size.y);}
        yield return Tap(GameAction.Inventory);Check(!game.IsInventoryOpen&&!game.IsPaused,"close resumes normal time");
        game.ReturnToMainMenu();
        game.OpenOptions();var hud=UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
        typeof(PrototypeHud).GetField("optionsTab",P).SetValue(hud,1);typeof(PrototypeHud).GetField("controlsPage",P).SetValue(hud,3);
        var style=(GUIStyle)typeof(PrototypeHud).GetField("bodyStyle",P).GetValue(hud);
        foreach(var action in (GameAction[])typeof(PrototypeHud).GetField("ConfigurableActions",A).GetValue(null))
        {
            string label="["+GameSettingsService.GetActionCategory(action)+"]  "+GameSettingsService.GetActionName(action);
            float width=style.CalcSize(new GUIContent(label)).x;
            Check(width<=265,"control label fits "+action+" "+width+"/265");
        }
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1440,900)})
        {
            Resolution(size.x,size.y);
            for(int page=2;page<=3;page++)
            {
                typeof(PrototypeHud).GetField("controlsPage",P).SetValue(hud,page);
                yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Capture("controls-page"+(page+1)+"-"+size.x+"x"+size.y);
            }
        }
        game.ReturnToMainMenu();
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        string failure=null;
        while(true){bool more=false;object value=null;try{more=routine.MoveNext();if(more)value=routine.Current;}catch(Exception e){failure=e.ToString();}if(failure!=null||!more)break;yield return value;}
        StageValidationInput.ReleaseAll();game.ReturnToMainMenu();Set("Settings",oldSettings);
        view.GetType().GetProperty("selectedSizeIndex",A).SetValue(view,oldSize);
        for(int i=temporary.Count-1;i>=0;i--)sizes.GetType().GetMethod("RemoveCustomSize",A).Invoke(sizes,new object[]{temporary[i]});
        tick.GetMethod("SetAutoTick",A).Invoke(null,new object[]{oldTick,oldInterval,false});
        File.WriteAllText(folder+"/result.txt",(failure==null?"PASS staged keyboard inventory checks; not normal campaign":"FAIL "+failure)+"\n"+string.Join("\n",checks));
        Debug.Log("[Inventory Keys] "+(failure==null?"PASS":"FAIL")+" "+folder);
    }
}
