using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Isolated real 2D physics. No campaign completion claim.
public static class CombatPresentationPhysics20260930
{
    static readonly List<string> checks=new List<string>();static string folder;static PlayerController player;static GameController game;
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-30-combat-presentation/physics-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        Time.timeScale=1;Application.runInBackground=true;
        var bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));
        player=PlayerController.Create(new Vector2(-4,0),()=>false,()=>true,()=>{},game.CameraFollow,bag,new GameSettingsService());player.enabled=false;
        game.CameraFollow.Target=player.transform;game.CameraFollow.FocusRoom(Vector2.zero,new Vector2(20,14),true);
        checks.Clear();game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static void Require(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static EnemyController Enemy(Vector2 at)
    {var e=EnemyController.Create(EnemyType.Grunt,at,player,()=>false,_=>{},game.CameraFollow);e.enabled=false;return e;}
    static void Clear()
    {
        foreach(var p in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()){p.gameObject.SetActive(false);UnityEngine.Object.Destroy(p.gameObject);}
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemyController>()){e.gameObject.SetActive(false);UnityEngine.Object.Destroy(e.gameObject);}
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            object value=null;bool next;try{next=run.MoveNext();if(next)value=run.Current;}
            catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e+"\n"+string.Join("\n",checks));Debug.LogException(e);yield break;}
            if(!next)break;yield return value;
        }
        File.WriteAllText(folder+"/result.txt","PASS isolated projectile physics\n"+string.Join("\n",checks));Debug.Log("[Combat presentation physics] PASS "+folder);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.1f);
        var enemy=Enemy(new Vector2(1.3f,0));var initial=enemy.Health;
        var pierce=PlayerProjectile.Create(Vector2.zero,Vector2.right,10,8,false,Color.white,new Vector2(.35f,.15f),3,WeaponFamily.Pistol,false,"scrap_pistol");
        Physics2D.SyncTransforms();yield return new WaitForSeconds(.22f);
        Require(enemy.Health<initial,"animated projectile damages enemy using real trigger physics");
        var after=enemy.Health;
        typeof(PlayerProjectile).GetMethod("OnTriggerEnter2D",Private).Invoke(pierce,new object[]{enemy.DamageCollider});
        Require(enemy.Health==after,"repeat collider event cannot duplicate damage");Clear();yield return null;
        foreach(var family in new[]{WeaponFamily.Cannon,WeaponFamily.Pistol})
        foreach(var obstructed in new[]{true,false})
        {
            var direct=Enemy(new Vector2(1.3f,0));var neighbour=Enemy(new Vector2(1.3f,.60f));var behind=Enemy(new Vector2(2.1f,0));
            var wall=new GameObject("Blast obstruction");wall.layer=GameLayers.Ground;wall.transform.position=new Vector3(1.7f,0,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.12f,2.4f);wall.SetActive(obstructed);
            Physics2D.SyncTransforms();var a=direct.Health;var b=neighbour.Health;var c=behind.Health;
            PlayerProjectile.Create(Vector2.zero,Vector2.right,10,8,false,Color.white,new Vector2(.35f,.15f),3,family,true,family==WeaponFamily.Pistol?"flintlock":"dragon_cannon");
            yield return new WaitForSeconds(.28f);
            Require(direct.Health<a,"explosive direct hit "+family);
            Require(neighbour.Health<b,"explosion reaches visible neighbouring enemy "+family);
            Require(obstructed?behind.Health==c:behind.Health<c,(obstructed?"explosion cannot damage through wall ":"same enemy is reached when wall removed ")+family);
            Require(a-direct.Health<12,"direct target is excluded from secondary blast "+family);
            Clear();UnityEngine.Object.Destroy(wall);yield return null;
        }
        var boundary=new GameObject("Fast projectile wall");boundary.layer=GameLayers.Ground;boundary.transform.position=new Vector3(.7f,0,0);
        boundary.AddComponent<BoxCollider2D>().size=new Vector2(.06f,2);
        var blocked=Enemy(new Vector2(1.3f,0));var hp=blocked.Health;
        Physics2D.SyncTransforms();PlayerProjectile.Create(Vector2.zero,Vector2.right,200,8,false,Color.white,new Vector2(.4f,.18f),1,WeaponFamily.Rifle);
        yield return new WaitForSeconds(.1f);
        Require(blocked.Health==hp&&UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length==0,"fast damage box stops at thin wall");
        UnityEngine.Object.Destroy(boundary);Clear();
    }
}
