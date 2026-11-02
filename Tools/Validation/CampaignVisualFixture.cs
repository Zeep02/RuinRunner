using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Invulnerable, staged art/pattern inspection. Never normal completion evidence.
public static class CampaignVisualFixture
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-16-motion/polish-02";
    static void Set(GameController game,string name,object value)=>typeof(GameController).GetProperty(name).SetValue(game,value);
    static void Clear()=>typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
    static void Capture(string name)=>Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{Folder+"/"+name+".png",1});
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        Directory.CreateDirectory(Folder);Application.runInBackground=true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(game.IsRunActive)game.ReturnToMainMenu();
        game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        game.CameraFollow.StartCoroutine(Run(game));
        return "Staged invulnerable visual fixture started, NOT gameplay completion evidence.";
    }
    static IEnumerator Run(GameController game)
    {
        yield return new WaitForSecondsRealtime(1);
        game.StopAllCoroutines();Clear();
        var types=new[]{EnemyType.Boss,EnemyType.Tidekeeper,EnemyType.SealHeart};
        for(var stage=0;stage<3;stage++)
        {
            Clear();var old=game.DungeonMap;
            var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,stage);
            Set(game,"DungeonMap",map);Set(game,"CurrentStageIndex",stage);Set(game,"CurrentRoom",3);
            Set(game,"StageIntroUntil",0f);Set(game,"WaveBannerUntil",0f);
            Set(game,"Boss",null);Set(game,"EnemiesAlive",0);Set(game,"ActiveChest",null);
            if(old?.Root!=null){old.Root.SetActive(false);UnityEngine.Object.Destroy(old.Root);}
            var center=map.GetRoomCenter(3);var room=map.GetRoom(3);
            game.Player.Teleport(center+new Vector2(-1,-1.5f));game.Player.SetStageClearSafety(true);
            var boss=EnemyController.Create(types[stage],center+new Vector2(1,1),game.Player,()=>game.IsPaused,_=>{},game.CameraFollow);
            boss.ConfigureArena(new Rect(center+room.LocalBounds.position,room.LocalBounds.size));
            Set(game,"Boss",boss);Set(game,"EnemiesAlive",1);
            EnemyController.Create(stage==0?EnemyType.Grunt:EnemyType.Tidecaller,center+new Vector2(-4,1),game.Player,()=>true,_=>{},game.CameraFollow);
            EnemyController.Create(stage==0?EnemyType.Shooter:EnemyType.Sentinel,center+new Vector2(-6,1),game.Player,()=>true,_=>{},game.CameraFollow);
            RewardChest.Create(center+new Vector2(3,-2),()=>{},game.Settings);
            var weapon=WeaponLootTable.Find("rust_sword");
            FieldLootPickup.Create(center+new Vector2(4,-2),weapon.Name,weapon.Color,game.Player,game.Settings,()=>false,0,PixelArtAtlas.GetHeldWeaponSprite(weapon));
            game.CameraFollow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
            Time.timeScale=0;
            yield return new WaitForSecondsRealtime(.25f);Capture("stage-"+(stage+1)+"-composition");
            yield return new WaitForSecondsRealtime(.15f);Time.timeScale=1;
            var seen=new bool[3];var end=Time.realtimeSinceStartup+8;
            while(Time.realtimeSinceStartup<end)
            {
                if(boss.IsActing && !seen[boss.AttackVisualPhase])
                {
                    seen[boss.AttackVisualPhase]=true;Capture("stage-"+(stage+1)+"-phase-"+boss.AttackVisualPhase);
                }
                yield return null;
            }
            Debug.Log("[Visual Fixture] stage="+(stage+1)+" captures ready; player invulnerable, manual scene setup, no completion claim.");
        }
        Time.timeScale=0;
        Debug.Log("[Visual Fixture] DONE 3 stages, hero/new actors/chest/drop/HUD. Inspect images and video before acceptance.");
    }
}
