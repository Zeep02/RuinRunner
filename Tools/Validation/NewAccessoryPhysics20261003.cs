using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Staged isolated physics/real controller actions. Overrides do not establish normal campaign balance.
public static class NewAccessoryPhysics20261003
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string folder;
    static GameController game;
    static PlayerController player;
    static InventorySystem bag;
    static bool paused;
    static int deaths;
    static readonly List<string> checks = new List<string>();
    static readonly List<GameObject> geometry = new List<GameObject>();
    static void Require(bool okay, string label) { if (!okay) throw new Exception(label); checks.Add("PASS " + label); }
    static void Field(string key, object value) => typeof(PlayerController).GetField(key, Private).SetValue(player, value);
    static object Call(string method) => typeof(PlayerController).GetMethod(method, Private).Invoke(player, null);
    static void ClearShots() { foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) { p.gameObject.SetActive(false); UnityEngine.Object.Destroy(p.gameObject); } }
    static void NewCase(string weapon, params AccessoryType[] accessories)
    {
        if (player != null) { player.ItemEffects.CancelTransient(); player.gameObject.SetActive(false); UnityEngine.Object.Destroy(player.gameObject); }
        foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>()) { e.gameObject.SetActive(false); UnityEngine.Object.Destroy(e.gameObject); }
        foreach (var g in geometry) { g.SetActive(false); UnityEngine.Object.Destroy(g); } geometry.Clear(); ClearShots();
        bag = new InventorySystem(); bag.AddWeapon(WeaponLootTable.Find(weapon));
        foreach (var type in accessories) bag.AddAccessory(AccessoryLootTable.Find(type));
        paused = false; deaths = 0; Time.timeScale = 1;
        player = PlayerController.Create(Vector2.zero, () => paused, () => false, () => {}, game.CameraFollow, bag, new GameSettingsService());
        player.enabled = false; player.EquipWeapon(bag.EquippedWeapon);
    }
    static EnemyController Enemy(Vector2 at, bool boss = false)
    {
        var e = EnemyController.Create(boss ? EnemyType.Boss : EnemyType.Grunt, at, player, () => paused,
            victim => { deaths++; player.OnEnemyDefeated(victim); }, game.CameraFollow);
        e.enabled = false; e.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        // A controlled health pool makes multi-tick/contact comparisons independent of campaign tuning.
        typeof(EnemyController).GetProperty("Health").SetValue(e, 1000f);
        return e;
    }
    static GameObject Wall(float x)
    {
        var wall = new GameObject("Proc fixture wall"); wall.layer = GameLayers.Ground; wall.transform.position = new Vector3(x, 0, 0);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.12f, 4); geometry.Add(wall); Physics2D.SyncTransforms(); return wall;
    }
    static void Special() { player.Stats.RestoreMana(player.MaxMana); Call("TryRangedWeaponAction"); ClearShots(); }
    static void UpgradeFirst(int level)
    {
        bag.AwardMaterials(200,"fixture-upgrade");var item=bag.OwnedAccessories[0];
        for(int n=0;n<level;n++)Require(bag.TryUpgrade(item.Identity,n),"real accessory payment +"+(n+1)+" "+item.Name);
    }
    static float Received(EnemyController enemy,float damage)=>Mathf.Max(1,damage*100/(100+enemy.Armor));
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-03-item-expansion/physics-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder);
        game = UnityEngine.Object.FindAnyObjectByType<GameController>(); game.ReturnToMainMenu(); game.enabled = false;
        foreach (var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>()) hud.enabled = false;
        Application.runInBackground = true; checks.Clear(); game.CameraFollow.StartCoroutine(Guard(Run())); return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            object value = null; bool next;
            try { next = run.MoveNext(); if (next) value = run.Current; }
            catch (Exception error) { Time.timeScale = 1; File.WriteAllText(folder + "/result.txt", "FAIL " + error + "\n" + string.Join("\n", checks)); Debug.LogException(error); yield break; }
            if (!next) break; yield return value;
        }
        Time.timeScale = 1; File.WriteAllText(folder + "/result.txt", "PASS staged isolated real actions/physics; not normal campaign or build evidence\n" + string.Join("\n", checks));
        Debug.Log("[Item effects] PASS " + folder);
    }
    static IEnumerator Run()
    {
        yield return null;
        Require(WeaponLootTable.Count==27&&WeaponLootTable.FindFirst(WeaponFamily.Bow)==null,"all four bows excluded from playable pool");
        var random=UnityEngine.Random.state;var seen=new HashSet<string>();
        for(int seed=0;seed<100;seed++){UnityEngine.Random.InitState(seed);for(int n=0;n<100;n++){if(WeaponLootTable.Roll(seed).Family==WeaponFamily.Bow)throw new Exception("Bow reward at seed "+seed);seen.Add(AccessoryLootTable.Roll(seed).Id);}}
        UnityEngine.Random.state=random;
        foreach(var id in new[]{"mana_thread","guard_bell","rush_spur","steady_locket","powder_satchel","last_light_pendant"})
            Require(seen.Contains(id)&&ItemIconLibrary.Get(id,InventoryIconKind.Accessory)!=null,"new item reachable with actual native icon "+id);
        NewCase("scrap_shotgun",AccessoryType.ManaThread);var enemy=Enemy(new Vector2(1.2f,0));var other=Enemy(new Vector2(1.5f,.2f));Physics2D.SyncTransforms();
        player.Stats.TrySpendMana(20);float mana=player.Mana;
        for(int repeat=0;repeat<12;repeat++){player.ItemEffects.OnOriginalHit(enemy,100,100);player.ItemEffects.OnOriginalHit(other,100,100);}
        Require(player.Mana==mana&&player.ItemEffects.ManaThreadProcs==0,"shotgun fan/multiple targets/duplicate callbacks count one action");
        for(int action=101;action<=103;action++)player.ItemEffects.OnOriginalHit(enemy,action,100);
        Require(Mathf.Abs(player.Mana-mana-4)<.01f&&player.ItemEffects.ManaThreadProcs==1,"four different attacks restore exactly4 mana");
        paused=true;Time.timeScale=0;player.ItemEffects.OnOriginalHit(enemy,104,100);yield return new WaitForSecondsRealtime(.2f);
        Require(player.ItemEffects.ManaThreadProcs==1,"pause cannot generate new item resources");paused=false;Time.timeScale=1;
        var owned=bag.OwnedAccessories[0];int materials;Require(bag.TryDisassemble(owned.Identity,0,out materials),"new accessory disassembles normally");
        for(int action=105;action<115;action++)player.ItemEffects.OnOriginalHit(enemy,action,100);
        Require(player.ItemEffects.ManaThreadProcs==1,"removed resource item leaves no trigger");
        NewCase("rust_sword",AccessoryType.GuardBell);player.Stats.TrySpendMana(20);mana=player.Mana;
        typeof(PlayerController).GetProperty("IsGuarding").SetValue(player,true);typeof(PlayerController).GetProperty("AimDirection").SetValue(player,Vector2.right);player.TakeDamage(4,Vector2.left);
        Require(player.ItemEffects.GuardBellProcs==1&&Mathf.Abs(player.Mana-mana-4)<.01f,"actual front guard hit rewards exactly4 mana");
        player.ItemEffects.OnDefenseSuccess(Vector2.right);Require(player.ItemEffects.GuardBellProcs==1,"repeated guard shares cooldown");
        NewCase("rust_sword",AccessoryType.RushSpur);enemy=Enemy(new Vector2(1.2f,0),true);Physics2D.SyncTransforms();float hp=enemy.Health;
        player.ItemEffects.OnDashStarted(Vector2.zero);player.ItemEffects.OnDashEnded();player.ItemEffects.OnOriginalHit(enemy,200,100);
        Require(player.ItemEffects.RushProcs==1&&enemy.Health<hp,"dash first hit applies bonus to boss");hp=enemy.Health;player.ItemEffects.OnOriginalHit(enemy,200,100);player.ItemEffects.OnOriginalHit(enemy,201,100);
        Require(enemy.Health==hp&&player.ItemEffects.RushProcs==1,"rush cannot repeat on same or next action before new dash");
        NewCase("rust_sword",AccessoryType.RushSpur);enemy=Enemy(new Vector2(1.2f,0));Wall(.6f);hp=enemy.Health;
        player.ItemEffects.OnDashEnded();player.ItemEffects.OnOriginalHit(enemy,210,100);Require(enemy.Health==hp,"dash bonus cannot damage through ground wall");
        NewCase("scrap_pistol",AccessoryType.SteadyLocket);enemy=Enemy(new Vector2(1.2f,0));Physics2D.SyncTransforms();yield return new WaitForSeconds(.85f);hp=enemy.Health;
        player.ItemEffects.OnOriginalHit(enemy,300,100);Require(player.ItemEffects.FocusProcs==1&&enemy.Health<hp,"real idle clock prepares one focused hit");
        yield return new WaitForSeconds(2.9f);Field("movementInput",Vector2.right);yield return null;hp=enemy.Health;player.ItemEffects.OnOriginalHit(enemy,301,100);Require(enemy.Health==hp,"moving clears focused preparation");
        NewCase("scrap_pistol",AccessoryType.PowderSatchel);enemy=Enemy(new Vector2(1.2f,0));other=Enemy(new Vector2(1.45f,.2f));var behind=Enemy(new Vector2(-1.4f,0));Wall(-.6f);Physics2D.SyncTransforms();hp=enemy.Health;float blocked=behind.Health;
        while(!bag.ActiveSlot.IsReloading)bag.TryConsumeAmmo();bag.Tick(bag.ActiveSlot.Weapon.ReloadSeconds);
        Require(player.ItemEffects.PowderProcs==1&&enemy.Health<hp&&other.Health<1000&&behind.Health==blocked,"empty-magazine actual completion hits visible targets and stops at wall");
        hp=enemy.Health;bag.Tick(100);Require(player.ItemEffects.PowderProcs==1&&enemy.Health==hp,"completed ammo clock cannot emit repeated explosions");
        NewCase("scrap_pistol",AccessoryType.PowderSatchel);enemy=Enemy(new Vector2(1.2f,0));hp=enemy.Health;bag.TryConsumeAmmo();bag.StartReload();bag.Tick(10);
        Require(player.ItemEffects.PowderProcs==0&&enemy.Health==hp,"partial magazine refill produces no free pulse");
        NewCase("scrap_pistol",AccessoryType.PowderSatchel);bag.AddWeapon(WeaponLootTable.Find("hand_crossbow"));var inactive=bag.QuickSlots[1];while(!inactive.IsReloading)inactive.TryConsumeAmmo();bag.Tick(10);
        Require(player.ItemEffects.PowderProcs==0,"inactive slot reload does not activate equipped-item reward");
        NewCase("rust_sword",AccessoryType.LastLightPendant);typeof(PlayerStats).GetProperty("Health").SetValue(player.Stats,player.MaxHealth*.25f);hp=player.Health;player.TakeDamage(6,Vector2.zero);
        Require(player.ItemEffects.LastLightProcs==1&&player.Health==hp&&Mathf.Abs(player.ItemEffects.Shield-4)<.01f,"actual low-health damage consumes6 of shield10 without losing health");
        float remainder=player.ItemEffects.AbsorbDamage(10);Require(Mathf.Abs(remainder-6)<.01f&&player.ItemEffects.LastLightProcs==1,"shield absorbs only remaining capacity and cooldown prevents regrant");
        player.ItemEffects.CancelTransient();Require(player.ItemEffects.Shield==0,"cleanup removes protective shield");
        NewCase("rust_sword",AccessoryType.ManaThread,AccessoryType.ManaThread,AccessoryType.ManaThread);UpgradeFirst(3);
        enemy=Enemy(new Vector2(1.2f,0));player.Stats.TrySpendMana(30);mana=player.Mana;
        for(int action=400;action<403;action++)player.ItemEffects.OnOriginalHit(enemy,action,100);
        Require(player.ItemEffects.ManaThreadProcs==1&&Mathf.Abs(player.Mana-mana-6.9f)<.01f,"highest +3 shared: three hits and capped three-copy mana6.9");
        bag.MoveBagItem(0,15);for(int action=403;action<410;action++)player.ItemEffects.OnOriginalHit(enemy,action,100);
        Require(player.ItemEffects.ManaThreadProcs==1,"moving upgraded duplicate cannot reset shared resource cooldown");
        NewCase("rust_sword",AccessoryType.GuardBell,AccessoryType.GuardBell,AccessoryType.GuardBell);UpgradeFirst(3);player.Stats.TrySpendMana(30);mana=player.Mana;
        player.ItemEffects.OnDefenseSuccess(Vector2.right);Require(Mathf.Abs(player.Mana-mana-6.9f)<.01f,"three-copy upgraded guard restores6.9, not independent resources");
        yield return new WaitForSeconds(3.1f);player.ItemEffects.OnDefenseSuccess(Vector2.right);Require(player.ItemEffects.GuardBellProcs==2,"+2 guard shared cooldown is actually3 seconds");
        NewCase("rust_sword",AccessoryType.RushSpur,AccessoryType.RushSpur,AccessoryType.RushSpur);UpgradeFirst(3);enemy=Enemy(new Vector2(1.2f,0));Physics2D.SyncTransforms();hp=enemy.Health;
        player.ItemEffects.OnDashEnded();player.ItemEffects.OnOriginalHit(enemy,500,100);
        Require(Mathf.Abs(hp-enemy.Health-Received(enemy,46))<.01f,"three-copy +3 rush damage40 percent times1.15");
        yield return new WaitForSeconds(2.5f);player.ItemEffects.OnDashEnded();player.ItemEffects.OnOriginalHit(enemy,501,100);Require(player.ItemEffects.RushProcs==2,"+2 dash trigger cooldown is actually2.4 seconds");
        NewCase("scrap_pistol",AccessoryType.SteadyLocket,AccessoryType.SteadyLocket,AccessoryType.SteadyLocket);UpgradeFirst(3);enemy=Enemy(new Vector2(1.2f,0));Physics2D.SyncTransforms();hp=enemy.Health;
        yield return new WaitForSeconds(.65f);player.ItemEffects.OnOriginalHit(enemy,600,100);
        Require(player.ItemEffects.FocusProcs==1&&Mathf.Abs(hp-enemy.Health-Received(enemy,51.75f))<.01f,"+2 focus prepares within0.55seconds, +3 capped damage51.75 percent");
        NewCase("scrap_pistol",AccessoryType.PowderSatchel,AccessoryType.PowderSatchel,AccessoryType.PowderSatchel);UpgradeFirst(3);
        var pack=new List<EnemyController>();for(int n=0;n<4;n++)pack.Add(Enemy(new Vector2(1.1f+n*.15f,0)));Physics2D.SyncTransforms();
        float powderDamage=player.Damage*player.Stats.SpecialDamageMultiplier*.30f*1.15f;
        while(!bag.ActiveSlot.IsReloading)bag.TryConsumeAmmo();bag.Tick(10);int hitCount=0;foreach(var target in pack)if(target.Health<1000){hitCount++;Require(Mathf.Abs(1000-target.Health-Received(target,powderDamage))<.01f,"upgraded powder damage respects actual special stats and armor");}
        Require(hitCount==3&&player.ItemEffects.PowderProcs==1,"powder copies retain max three targets and one shared pulse");
        yield return new WaitForSeconds(3.1f);while(!bag.ActiveSlot.IsReloading)bag.TryConsumeAmmo();bag.Tick(10);Require(player.ItemEffects.PowderProcs==2,"+2 empty reload cooldown is actually3 seconds");
        NewCase("rust_sword",AccessoryType.LastLightPendant,AccessoryType.LastLightPendant,AccessoryType.LastLightPendant);UpgradeFirst(3);
        typeof(PlayerStats).GetProperty("Health").SetValue(player.Stats,player.MaxHealth*.40f);float hpBefore=player.Health;
        remainder=player.ItemEffects.AbsorbDamage(1);
        Require(remainder==0&&Mathf.Abs(player.ItemEffects.Shield-15.1f)<.01f&&player.Health==hpBefore,"+2 shield activates at40percent, three-copy +3 capacity16.1 absorbs only1");
        var strongest=bag.OwnedAccessories[0];bag.TryDisassemble(strongest.Identity,3,out materials);Require(player.ItemEffects.Shield==0,"removing strongest protective duplicate cancels oversized shield");
        player.ItemEffects.AbsorbDamage(1);Require(player.ItemEffects.LastLightProcs==1,"downgrade/removal cannot reset protection cooldown");
        player.gameObject.SetActive(false);UnityEngine.Object.Destroy(player.gameObject);foreach(var e in UnityEngine.Object.FindObjectsByType<EnemyController>())UnityEngine.Object.Destroy(e.gameObject);
        foreach(var g in geometry)UnityEngine.Object.Destroy(g);geometry.Clear();game.enabled=true;foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=true;
    }
}
