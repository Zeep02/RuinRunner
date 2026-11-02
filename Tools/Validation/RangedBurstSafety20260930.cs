using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RuinRunner;
public static class RangedBurstSafety20260930
{
    static GameController game;static PlayerController player;static InventorySystem bag;
    static string folder;static readonly List<string> checks=new List<string>();
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly MethodInfo Special=typeof(PlayerController).GetMethod("TryRangedWeaponAction",Private);
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-30-combat-presentation/burst-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));
        player=PlayerController.Create(Vector2.zero,()=>false,()=>true,()=>{},game.CameraFollow,bag,new GameSettingsService());player.enabled=false;
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();StageValidationInput.SetAim(Vector2.right);Time.timeScale=1;Application.runInBackground=true;
        checks.Clear();game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static void Require(bool okay,string label){if(!okay)throw new Exception(label);checks.Add("PASS "+label);}
    static PlayerProjectile[] Shots()=>UnityEngine.Object.FindObjectsByType<PlayerProjectile>();
    static void Clear(){foreach(var shot in Shots()){shot.gameObject.SetActive(false);UnityEngine.Object.Destroy(shot.gameObject);}}
    static void Equip(string id){Clear();bag.Reset();bag.AddWeapon(WeaponLootTable.Find(id));player.EquipWeapon(bag.EquippedWeapon);player.Stats.RestoreMana(player.MaxMana);}
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            bool next;object value=null;try{next=run.MoveNext();if(next)value=run.Current;}
            catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e+"\n"+string.Join("\n",checks));Debug.LogException(e);yield break;}
            if(!next)break;yield return value;
        }
        File.WriteAllText(folder+"/result.txt","PASS isolated real coroutine/cancellation checks; not campaign evidence\n"+string.Join("\n",checks));
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.15f);Equip("scrap_pistol");yield return null;
        var mana=player.Mana;Special.Invoke(player,null);Require(Shots().Length==1,"first burst round is immediate");
        Time.timeScale=0;yield return new WaitForSecondsRealtime(.16f);Require(Shots().Length==1,"pause cannot advance pending burst");
        Time.timeScale=1;yield return new WaitForSeconds(.16f);
        Require(Shots().Length==3,"burst emits three distinct timed rounds");
        Require(Mathf.Abs((mana-player.Mana)-player.EquippedWeapon.SpecialManaCost)<.01f,"burst pays mana once");
        foreach(var shot in Shots())Require((int)typeof(PlayerProjectile).GetField("remainingHits",Private).GetValue(shot)>=2,"pistol precision rounds actually pierce");
        Equip("twin_pistols");yield return null;Special.Invoke(player,null);Require(Shots().Length==1,"new burst first round");
        player.EquipWeapon(WeaponLootTable.Find("rust_sword"));yield return new WaitForSeconds(.20f);
        Require(Shots().Length==1,"weapon change cancels pending burst rounds");
        Equip("scrap_pistol");yield return null;Special.Invoke(player,null);
        player.StartCoroutine((IEnumerator)typeof(PlayerController).GetMethod("Dash",Private).Invoke(player,null));yield return new WaitForSeconds(.20f);
        Require(Shots().Length==1,"real dash cancels pending burst rounds");
        player.Teleport(Vector2.zero);
        Equip("flintlock");yield return null;Special.Invoke(player,null);yield return new WaitForSeconds(.20f);
        Require(Shots().Length==1,"flintlock uses one explosive shell rather than pistol burst");
        Require((bool)typeof(PlayerProjectile).GetField("explosiveShot",Private).GetValue(Shots()[0]),"flintlock advertised explosion is enabled");
        Clear();
    }
}
