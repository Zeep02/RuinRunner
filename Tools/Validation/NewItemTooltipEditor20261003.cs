using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Supplied transient inventory and real OnGUI renderer; not normal campaign evidence.
public static class NewItemTooltipEditor20261003
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy;
    static EditorWindow view;static object sizes;static int oldSize;static readonly List<int> added=new List<int>();
    static GameController game;static PrototypeHud hud;static string folder;static TooltipPaint20261003 paint;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-03-item-expansion/editor-ui-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",P).Invoke(game,null);
        hud=game.GetComponent<PrototypeHud>();Application.runInBackground=true;
        view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Focus();
        oldSize=(int)view.GetType().GetProperty("selectedSizeIndex",All).GetValue(view);
        var t=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");var instance=t.GetProperty("instance",All).GetValue(null);
        sizes=t.GetMethod("GetGroup",All).Invoke(instance,new[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"),"Standalone")});
        game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static void Size(int w,int h)
    {
        var t=Type.GetType("UnityEditor.GameViewSize,UnityEditor");var size=Activator.CreateInstance(t,new object[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"),"FixedResolution"),w,h,"Item tooltip QA"});
        int index=(int)sizes.GetType().GetMethod("GetTotalCount",All).Invoke(sizes,null);sizes.GetType().GetMethod("AddCustomSize",All).Invoke(sizes,new[]{size});added.Add(index);
        view.GetType().GetMethod("SizeSelectionCallback",All).Invoke(view,new object[]{index,null});view.Repaint();
    }
    static void Restore()
    {
        view.GetType().GetMethod("SizeSelectionCallback",All).Invoke(view,new object[]{oldSize,null});
        for(int n=added.Count-1;n>=0;n--)sizes.GetType().GetMethod("RemoveCustomSize",All).Invoke(sizes,new object[]{added[n]});added.Clear();
        if(paint!=null)UnityEngine.Object.Destroy(paint.gameObject);Time.timeScale=1;game.enabled=true;game.ReturnToMainMenu();
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true){bool next;object value=null;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception error){Restore();File.WriteAllText(folder+"/result.txt","FAIL "+error);Debug.LogException(error);yield break;}if(!next)break;yield return value;}
        Restore();File.WriteAllText(folder+"/result.txt","PASS actual GameView tooltip rendering, Galmuri metrics and delivered wheel event; supplied transient inventory, not campaign or Windows proof.");
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();
        if(paint.Error!=null)throw new Exception(paint.Error);
        var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;game.Player.enabled=false;game.Player.SetStageClearSafety(true);
        typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);typeof(GameController).GetProperty("IsInventoryOpen").SetValue(game,true);
        Time.timeScale=0;var bag=game.Inventory;bag.Reset();bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));
        foreach(var id in new[]{"mana_thread","guard_bell","rush_spur","steady_locket","powder_satchel","last_light_pendant"})bag.AddAccessory(AccessoryLootTable.Find(id));
        bag.AwardMaterials(999,"tooltip-fixture");foreach(var item in bag.OwnedAccessories)for(int level=0;level<3;level++)bag.TryUpgrade(item.Identity,level);
        game.FocusEquipment(bag.OwnedAccessories[4].Identity);
        paint=new GameObject("New item tooltip QA").AddComponent<TooltipPaint20261003>();paint.Hud=hud;
        var validator=paint.gameObject.AddComponent<UiValidation>();validator.enabled=false;
        typeof(UiValidation).GetField("hud",P).SetValue(validator,hud);typeof(UiValidation).GetField("directory",P).SetValue(validator,folder);paint.Validator=validator;
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1340,820)})
        {
            Size(resolution.x,resolution.y);paint.Key=null;yield return Capture(resolution+"-inventory");
            for(int n=0;n<6;n++){paint.Key="A:"+n;yield return Capture(resolution+"-"+bag.OwnedAccessories[n].Definition.Id);}
            paint.Key="A:0";paint.CheckLayout=true;yield return Capture(resolution+"-long-top");
            if(!paint.Checked)throw new Exception("OnGUI layout metrics did not execute");
            File.Move(folder+"/tooltip-layout.json",folder+"/tooltip-layout-"+resolution.x+"x"+resolution.y+".json");
            // Normal current descriptions fit at720p. Explicitly constrain available height
            // to exercise the production overflow fallback without pretending ordinary text overflows.
            paint.AvailableHeight=560;yield return Capture(resolution+"-constrained-560-top");
            float before=((Vector2)typeof(PrototypeHud).GetField("tooltipScroll",P).GetValue(hud)).y;
            bool delivered=false;EditorApplication.delayCall+=()=>{view.SendEvent(new Event{type=EventType.ScrollWheel,mousePosition=new Vector2(500,300),delta=new Vector2(0,12)});delivered=true;};
            float deadline=Time.realtimeSinceStartup+3;while(!delivered&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return Capture(resolution+"-constrained-560-wheel");
            float after=((Vector2)typeof(PrototypeHud).GetField("tooltipScroll",P).GetValue(hud)).y;
            if(!delivered||after<=before)throw new Exception("Real GameView wheel did not move tooltip "+resolution+" before="+before+" after="+after);
            paint.CheckLayout=false;paint.Checked=false;paint.AvailableHeight=0;
        }
    }
}
[DefaultExecutionOrder(2500)]public sealed class TooltipPaint20261003:MonoBehaviour
{
    public PrototypeHud Hud;public UiValidation Validator;public string Key,Error;public bool CheckLayout,Checked;public float AvailableHeight;
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    void OnGUI()
    {
        if(Hud==null||Key==null)return;
        var matrix=GUI.matrix;var mouse=Event.current.mousePosition;GUI.depth=-1000;
        float scale=GameTypography.LayoutScale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));Event.current.mousePosition=new Vector2(Screen.width/scale-12,Screen.height/scale-12);
        try{
            typeof(PrototypeHud).GetMethod("DrawItemTooltip",P).Invoke(Hud,new object[]{Screen.width/scale,AvailableHeight>0?AvailableHeight:Screen.height/scale,Key});
            if(CheckLayout&&!Checked&&Event.current.type==EventType.Repaint){typeof(UiValidation).GetMethod("ValidateTooltipLayouts",P).Invoke(Validator,new object[]{Screen.width/scale,Screen.height/scale});Checked=true;}
        }catch(Exception e){Error=e.ToString();}
        Event.current.mousePosition=mouse;GUI.matrix=matrix;
    }
}
