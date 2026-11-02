using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RuinRunner;

public static class WeaponFxLifecycle20261001
{
    static string folder;static GameController game;static readonly List<string> checks=new List<string>();
    const BindingFlags P=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Require(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-01-weapon-identity/lifecycle-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);checks.Clear();
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();game.enabled=true;
        typeof(GameController).GetMethod("BeginRunForActiveSlot",P).Invoke(game,null);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Application.runInBackground=true;game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        string error=null;while(true){object value=null;bool next;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception e){error=e.ToString();break;}if(!next)break;yield return value;}
        Time.timeScale=1;StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;
        File.WriteAllText(folder+"/result.txt",(error==null?"PASS isolated effect lifecycle":"FAIL "+error)+"\n"+string.Join("\n",checks));
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>())UnityEngine.Object.Destroy(enemy.gameObject);
        foreach(var telegraph in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(telegraph.gameObject);
        var at=game.DungeonMap.GetRoomCenter(3);game.Player.Teleport(at);
        for(int i=0;i<20;i++)WeaponSecondaryParticles.Fan("StoneChip",at,Vector2.up,10,300,4,.5f,.65f,2001,Color.white,3);
        Require(WeaponSecondaryParticles.ActiveCount==64,"hit flood is capped at 64 cosmetic fragments");
        var effect=NativeCombatEffect.Create("CannonBlast",at,0,2,.5f,Color.white);
        yield return new WaitForSeconds(.08f);var pose=effect.FrameIndex;var fragments=UnityEngine.Object.FindObjectsByType<WeaponSecondaryParticles>();var before=new Vector3[fragments.Length];for(int i=0;i<fragments.Length;i++)before[i]=fragments[i].transform.position;
        Time.timeScale=0;yield return new WaitForSecondsRealtime(.18f);
        Require(effect!=null&&effect.FrameIndex==pose,"pause freezes authored animation pose");
        bool frozen=true;for(int i=0;i<fragments.Length;i++)frozen&=fragments[i]!=null&&fragments[i].transform.position==before[i];
        Require(frozen,"pause freezes fragment movement and lifetime");Time.timeScale=1;yield return new WaitForSeconds(.8f);
        Require(WeaponSecondaryParticles.ActiveCount==0&&effect==null,"effects expire after resume without a residual pool count");
        var wall=new GameObject("Cosmetic fragment wall");wall.layer=GameLayers.Ground;wall.transform.position=at+Vector2.right*.10f;wall.AddComponent<BoxCollider2D>().size=new Vector2(.02f,1);Physics2D.SyncTransforms();
        WeaponSecondaryParticles.Fan("MetalSpark",at,Vector2.right,1,0,8,1,.5f,2002,Color.white);
        yield return new WaitForSeconds(.10f);Require(WeaponSecondaryParticles.ActiveCount==0,"cosmetic chip stops at a thin ground wall");UnityEngine.Object.Destroy(wall);
        game.Inventory.Reset();game.Inventory.AddWeapon(WeaponLootTable.Find("iron_greatsword"));game.Player.EquipWeapon(game.Inventory.EquippedWeapon);
        StageValidationInput.BeginFrame();StageValidationInput.SetAim(Vector2.right);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();yield return new WaitForSeconds(.12f);
        Require(game.Player.IsGreatswordCharging&&UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length>0,"held RMB creates actual charge presentation");
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Dash,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.24f);
        bool chargeGone=true;foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())chargeGone&=fx.ClipId!="Charge";
        Require(!game.Player.IsGreatswordCharging&&chargeGone,"real dash cancels charge state and its attached glow");
        NativeCombatEffect.Create("Smoke",at,0,1,2,Color.white,2000,game.Player.transform,true);
        WeaponSecondaryParticles.Fan("StoneChip",at,Vector2.up,12,300,4,2,.6f,2002,Color.white);
        game.ReturnToMainMenu();yield return null;yield return null;
        Require(NativeCombatEffect.ActiveCount==0&&WeaponSecondaryParticles.ActiveCount==0,"real menu session teardown clears looping effects and flying fragments");
    }
}
