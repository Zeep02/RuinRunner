using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Equipment is injected in an isolated arena to cover every catalog entry.
// This is deliberately NOT a normal loot/run completion claim.
public static class WeaponCombatRegression20261001
{
    static int checks;
    static string folder;
    static readonly List<string> assertions=new List<string>();
    static Type input;
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;assertions.Add("PASS "+name);Debug.Log("[Equipment Combat] CHECK "+name);}
    static void Hold(GameAction action,bool value)=>input.GetMethod("SetHeld").Invoke(null,new object[]{action,value});
    static void Frame()=>input.GetMethod("BeginFrame").Invoke(null,null);
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-01-weapon-identity/catalog-"+DateTime.Now.ToString("HHmmss");System.IO.Directory.CreateDirectory(folder);assertions.Clear();
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu();
        input=typeof(GameController).Assembly.GetType("RuinRunner.StageValidationInput");
        input.GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        input.GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        input.GetMethod("ReleaseAll").Invoke(null,null);
        Time.timeScale=1;Time.captureFramerate=0;
        Application.runInBackground=true;
        UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.enabled=false;game.CameraFollow.StartCoroutine(Guard(Run(game),game));
        return folder;
    }
    static IEnumerator Guard(IEnumerator run,GameController game)
    {
        string error=null;
        while(true){object value=null;bool next;try{next=run.MoveNext();if(next)value=run.Current;}catch(Exception e){error=e.ToString();break;}if(!next)break;yield return value;}
        input.GetMethod("ReleaseAll").Invoke(null,null);game.ReturnToMainMenu();game.enabled=true;
        System.IO.File.WriteAllText(folder+"/result.txt",(error==null?"PASS supplied full equipment catalog / actual GameAction combat":"FAIL "+error)+"\n"+string.Join("\n",assertions));
    }
    static IEnumerator Run(GameController game)
    {
        yield return null;yield return null;
        checks=0;
        var camera=game.CameraFollow;
        foreach(var weapon in WeaponLootTable.All)
        {
            var inventory=new InventorySystem();inventory.AddWeapon(weapon);
            var settings=new GameSettingsService("F:/codex/LoglikeCap/Logs/Validation/2026-09-16-equipment/unused-settings.json",false);
            var player=PlayerController.Create(Vector2.zero,()=>false,()=>false,()=>{},camera,inventory,settings);
            player.EquipWeapon(weapon);camera.Target=player.transform;
            var ranged=weapon.AttackType==WeaponAttackType.Ranged;
            var target=EnemyController.Create(EnemyType.Boss,new Vector2(ranged?3:1.05f,0),player,()=>true,_=>{},null);
            target.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            var aim=((Vector2)target.DamageCollider.bounds.center-(Vector2)player.transform.position).normalized;
            input.GetMethod("SetAim").Invoke(null,new object[]{aim});
            yield return null;Frame();
            var hp=target.Health;var ammo=inventory.ActiveSlot.Ammo;
            Hold(GameAction.Attack,true);yield return null;Frame();Hold(GameAction.Attack,false);
            yield return new WaitForSeconds(.65f);Frame();
            Check(target.Health<hp,weapon.Id+" primary deals actual collider damage");
            Check(!ranged || inventory.ActiveSlot.Ammo==ammo-1,weapon.Id+" primary consumes exactly one round");
            Check(player.GetComponent<HeldWeaponView>().RendererSprite!=null && InventoryIconFactory.Get(weapon.Id,weapon.Color,InventoryIconKind.Weapon)!=null,weapon.Id+" held sprite and UI icon");
            UnityEngine.Object.Destroy(target.gameObject);
            foreach(var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())UnityEngine.Object.Destroy(shot.gameObject);
            player.Teleport(Vector2.zero);
            target=EnemyController.Create(EnemyType.Boss,new Vector2(ranged?3:1.05f,0),player,()=>true,_=>{},null);
            target.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
            yield return null;Frame();
            input.GetMethod("SetAim").Invoke(null,new object[]{((Vector2)target.DamageCollider.bounds.center-(Vector2)player.transform.position).normalized});
            hp=target.Health;var mana=player.Mana;var health=player.Health;
            Hold(GameAction.Guard,true);yield return null;Frame();
            if(weapon.Family==WeaponFamily.Longsword)
            {
                Check(player.IsGuarding,weapon.Id+" RMB enters guard");
                player.TakeDamage(10,-player.AimDirection);
                Check(player.Health>health-10 && player.Health<health,weapon.Id+" directional guard reduces damage");
            }
            else if(weapon.Family==WeaponFamily.Katana)
            {
                Check(player.IsParrying,weapon.Id+" RMB enters parry window");
                player.TakeDamage(10,-player.AimDirection);
                Check(player.Health==health,weapon.Id+" timed parry prevents incoming damage");
            }
            else if(weapon.Family==WeaponFamily.Greatsword)yield return new WaitForSeconds(.65f);
            Hold(GameAction.Guard,false);
            yield return new WaitForSeconds(.7f);Frame();
            if(weapon.Family!=WeaponFamily.Longsword)Check(target.Health<hp,weapon.Id+" RMB produces actual damage");
            if(ranged || weapon.Family==WeaponFamily.Mace)Check(player.Mana<mana,weapon.Id+" RMB spends mana");
            var charges=player.DashCharges;var start=(Vector2)player.transform.position;
            Hold(GameAction.MoveUp,true);Hold(GameAction.Dash,true);yield return null;Frame();Hold(GameAction.Dash,false);Hold(GameAction.MoveUp,false);
            yield return new WaitForSeconds(.25f);Frame();
            Check(player.DashCharges==charges-1 && player.transform.position.y>start.y+2,weapon.Id+" action to movement-directed dash");
            input.GetMethod("ReleaseAll").Invoke(null,null);
            UnityEngine.Object.Destroy(player.gameObject);UnityEngine.Object.Destroy(target.gameObject);
            foreach(var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())UnityEngine.Object.Destroy(shot.gameObject);
            yield return null;
        }
        foreach(var item in AccessoryLootTable.All)
        {
            var stats=new PlayerStats();var before=Snapshot(stats);stats.ApplyAccessory(item.Type);var after=Snapshot(stats);
            Check(before!=after && InventoryIconFactory.Get(item.Id,item.Color,InventoryIconKind.Accessory)!=null,item.Id+" changes player stats and has icon");
        }
        Debug.Log("[Equipment Combat] PASS "+checks+" checks across "+WeaponLootTable.Count+" weapons / "+AccessoryLootTable.Count+" accessories. Isolated injected equipment, not normal run.");
    }
    static string Snapshot(PlayerStats s)=>string.Join("/",s.MaxHealth,s.Health,s.MaxMana,s.Mana,s.WeaponDamageMultiplier,s.CriticalChance,s.CriticalDamage,s.Armor,s.Luck,s.MoveSpeed,s.ManaRegeneration,s.SpecialDamageMultiplier,s.LifeOnKill,s.ManaOnKill);
}
