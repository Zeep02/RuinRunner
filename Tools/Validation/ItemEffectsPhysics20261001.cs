using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Staged isolated physics/real controller actions. Overrides do not establish normal campaign balance.
public static class ItemEffectsPhysics20261001
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
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-item-effects/physics-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder);
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
        NewCase("scrap_shotgun", AccessoryType.BerserkerEmblem);
        var enemy = Enemy(new Vector2(1.5f, 0)); Physics2D.SyncTransforms();
        for (int action = 0; action < 2; action++)
        {
            Call("RangedAttack"); var shots = UnityEngine.Object.FindObjectsByType<PlayerProjectile>();
            Require(shots.Length == 3, "real shotgun action creates three pellet colliders");
            foreach (var shot in shots) typeof(PlayerProjectile).GetMethod("OnTriggerEnter2D", Private).Invoke(shot, new object[] { enemy.DamageCollider });
            ClearShots(); yield return new WaitForSeconds(.7f);
            Require(player.ItemEffects.BleedTicksResolved == 0, "pellet fan/repeat trigger cannot count as three source actions " + action);
        }
        Call("RangedAttack");
        foreach (var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) typeof(PlayerProjectile).GetMethod("OnTriggerEnter2D", Private).Invoke(shot, new object[] { enemy.DamageCollider });
        ClearShots(); Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
        Require(player.ItemEffects.BleedTicksResolved == 0, "pause freezes pending bleed"); Time.timeScale = 1;
        yield return new WaitForSeconds(1.95f);
        Require(player.ItemEffects.BleedTicksResolved == 3, "third original action resolves three real bleed ticks");
        Require(deaths == 0, "bleed does not fabricate kill rewards");

        NewCase("scrap_pistol", AccessoryType.BerserkerEmblem);
        Call("TryRangedWeaponAction"); Call("RangedAttack"); yield return new WaitForSeconds(.18f);
        int burstRounds = 0; int regularRounds = 0;
        foreach (var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())
        {
            bool special = (bool)typeof(PlayerProjectile).GetField("special", Private).GetValue(shot);
            int action = (int)typeof(PlayerProjectile).GetField("sourceAction", Private).GetValue(shot);
            if (special) { burstRounds++; Require(action == 1, "late burst round retains its paid source action despite concurrent regular shot"); }
            else { regularRounds++; Require(action == 2, "concurrent regular shot keeps a separate source action"); }
        }
        Require(burstRounds == 3 && regularRounds == 1, "real burst and concurrent normal input remain four distinct physical projectiles");
        ClearShots();

        NewCase("scrap_pistol", AccessoryType.VampireFang, AccessoryType.VampireFang, AccessoryType.VampireFang, AccessoryType.VampireFang);
        var boss = Enemy(new Vector2(1.3f, 0), true); Physics2D.SyncTransforms();
        for (int i = 0; i < 6; i++)
        {
            Call("RangedAttack"); yield return new WaitForSeconds(.15f); ClearShots();
        }
        Require(player.ItemEffects.ActiveShards == 1, "six real ranged contacts create recovery shard in a boss-only fight");
        player.OnEnemyDefeated(boss);
        Require(player.ItemEffects.ActiveShards == 1, "kill/contact triggers share shard cooldown rather than duplicate copies firing");
        player.Stats.ReceiveDamage(12); float health = player.Health;
        var shard = GameObject.Find("RecoveryShard"); var landing = shard.transform.position;
        player.transform.position = landing - (Vector3)RoomLayoutRules.FootOffset;
        player.GetComponent<Rigidbody2D>().position = player.transform.position;
        yield return new WaitForSeconds(.4f);
        Require(player.ItemEffects.ShardsCollected == 1 && Mathf.Abs(player.Health - health - 3f) < .01f, "recovery requires approach and duplicate strength caps at three copies");

        NewCase("scrap_pistol", AccessoryType.ArcaneLens);
        var front = Enemy(new Vector2(1.2f, 0)); var hidden = Enemy(new Vector2(2.4f, 0)); var wall = Wall(1.9f);
        float hp = hidden.Health; Special(); Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
        Require(player.ItemEffects.EchoContactsResolved == 0, "pause freezes paid delayed echo"); Time.timeScale = 1;
        yield return new WaitForSeconds(.3f);
        Require(player.ItemEffects.EchoContactsResolved == 1 && front.Health < 1000 && hidden.Health == hp, "echo reaches one aimed visible enemy and respects wall");
        player.EquipWeapon(WeaponLootTable.Find("hunter_bow")); bag.MoveBagItem(0, 4); Special(); yield return new WaitForSeconds(.3f);
        Require(player.ItemEffects.EchoContactsResolved == 1, "weapon/inventory movement cannot reset shared echo cooldown");
        wall.SetActive(false); yield return new WaitForSeconds(.8f); Special(); yield return new WaitForSeconds(.25f);
        Require(player.ItemEffects.EchoContactsResolved == 2, "echo resumes after elapsed cooldown");

        NewCase("scrap_pistol", AccessoryType.SoulCapacitor, AccessoryType.SoulCapacitor, AccessoryType.SoulCapacitor, AccessoryType.SoulCapacitor);
        var first = Enemy(new Vector2(.9f, 0)); var second = Enemy(new Vector2(1.3f, .75f)); var third = Enemy(new Vector2(1.3f, -.75f));
        var fourth = Enemy(new Vector2(1.7f, 1.2f)); hidden = Enemy(new Vector2(2.7f, 0)); Wall(2.2f);
        var extra = second.gameObject.AddComponent<BoxCollider2D>(); extra.isTrigger = true;
        Physics2D.SyncTransforms(); Special(); yield return null;
        int damaged = 0; foreach (var e in new[] { first, second, third, fourth }) if (e.Health < 1000) damaged++;
        Require(player.ItemEffects.ChainContactsResolved == 3 && damaged == 3, "chain is three unique victims despite extra collider and fourth reachable enemy");
        Require(hidden.Health == 1000, "chain cannot hop through a wall");
        float applied = player.Damage * .45f * 100f / (100f + first.Armor);
        Require(Mathf.Abs(1000 - first.Health - Mathf.Max(1, applied)) < .02f, "chain duplicate multiplier caps at three copies");
        Special(); Require(player.ItemEffects.ChainContactsResolved == 3, "chain shared cooldown blocks immediate repeated paid action");

        NewCase("scrap_pistol", AccessoryType.SageSigil, AccessoryType.BerserkerEmblem);
        for (int i = 0; i < 5; i++) Enemy(new Vector2(Mathf.Cos(i * Mathf.PI * 2 / 5), Mathf.Sin(i * Mathf.PI * 2 / 5)));
        hidden = Enemy(new Vector2(1.5f, 0)); Wall(1.25f); Physics2D.SyncTransforms(); Special();
        yield return new WaitForSeconds(2.7f);
        Require(player.ItemEffects.OrbitContactsResolved > 0 && player.ItemEffects.OrbitContactsResolved <= 4, "finite orbit uses swept contact and bounded unique victims");
        Require(hidden.Health == 1000, "orbit does not hit across a wall");
        Require(player.ItemEffects.BleedTicksResolved == 0, "proc orbit contacts do not feed original-hit bleed");
        int orbits = 0; foreach (var effect in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>()) if (effect.ClipId == "Energy") orbits++;
        Require(orbits == 0, "orbit visuals expire with gameplay action");

        NewCase("rust_sword", AccessoryType.StormFeather, AccessoryType.WingedBoots);
        front = Enemy(new Vector2(.45f, 0)); hidden = Enemy(new Vector2(1.7f, 0)); Wall(1.2f); Physics2D.SyncTransforms();
        player.StartCoroutine((IEnumerator)Call("Dash")); yield return new WaitForSeconds(.65f);
        Require(player.transform.position.x < 1.1f, "real dash capsule stops before fixture wall");
        Require(player.ItemEffects.WakeContactsResolved == 1 && front.Health < 1000 && hidden.Health == 1000, "dash wake follows safe actual path, hits once and stops at wall");
        Require(player.ItemEffects.BleedTicksResolved == 0, "wake does not recursively become a source attack");

        NewCase("rust_sword", AccessoryType.GuardianSeal, AccessoryType.GuardianPlate, AccessoryType.BulwarkBrooch);
        front = Enemy(new Vector2(.8f, 0)); hidden = Enemy(new Vector2(1.65f, 0)); Wall(1.25f); Physics2D.SyncTransforms();
        typeof(PlayerController).GetProperty("IsGuarding").SetValue(player, true);
        player.TakeDamage(10, Vector2.left);
        Require(player.ItemEffects.Shield == 8f && player.ItemEffects.BashContactsResolved == 1, "real guard success primes shield and bounded frontal bash");
        Require(hidden.Health == 1000, "guardian retaliation respects wall");
        health = player.Health; Field("invincibilityTicks", 0); player.TakeDamage(8, Vector2.left);
        Require(player.Health == health && player.ItemEffects.Shield < 8 && player.ItemEffects.Shield > 0, "shield absorbs finite damage without minimum-one health leak or refresh exploit");
        player.EquipWeapon(WeaponLootTable.Find("training_katana")); Field("parryTimer", .16f); Field("invincibilityTicks", 0);
        hp = front.Health; player.TakeDamage(20, Vector2.left);
        Require(player.Health == health && front.Health < hp && hidden.Health == 1000, "real katana parry damages visible victim and blocks wall victim");

        NewCase("scrap_pistol", AccessoryType.RubyHeart, AccessoryType.CrimsonRing, AccessoryType.BerserkerEmblem);
        var marked = Enemy(new Vector2(1f, 0)); var spreadA = Enemy(new Vector2(1.6f, .3f)); var spreadB = Enemy(new Vector2(1.6f, -.3f));
        hidden = Enemy(new Vector2(2.6f, 0)); Wall(2.1f); Physics2D.SyncTransforms();
        for (int i = 1; i <= 3; i++) player.ItemEffects.OnOriginalHit(marked, i, 10);
        marked.TakeDamage(2000, Vector2.zero, false); yield return new WaitForSeconds(.7f);
        Require(spreadA.Health < 1000 && spreadB.Health < 1000 && hidden.Health == 1000, "original kill spreads bounded marks to two visible neighbours");
        Require(deaths == 1, "marked kill is rewarded exactly once");
        // A proc kill must not turn the remaining neighbours into another spread wave.
        typeof(EnemyController).GetProperty("Health").SetValue(spreadA, .5f);
        var unaffected = Enemy(new Vector2(.8f, 1.5f)); Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.7f);
        Require(unaffected.Health == 1000 && deaths == 2, "bleed kill is rewarded once without recursive spread");

        NewCase("scrap_pistol", AccessoryType.SapphireFlask, AccessoryType.ManaPrism);
        player.Stats.TrySpendMana(30); float mana = player.Mana;
        player.ItemEffects.OnManaSpent(10, Vector2.right); bag.MoveBagItem(0, 4); player.ItemEffects.OnManaSpent(10, Vector2.right);
        Require(player.Mana == mana, "arcanist refund waits for third paid action");
        player.Teleport(Vector2.zero); player.ItemEffects.OnManaSpent(10, Vector2.right);
        Require(player.Mana == mana + 1, "paid-action counter survives inventory movement and teleport without recursive refund");
        bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.SageSigil)); bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.ArcaneLens));
        Require(Mathf.Abs(player.Mana - mana - 31) < .01f, "third distinct arcanist piece restores its max-mana acquisition increase once");
        float upgradedSetMana = player.Mana;
        front = Enemy(new Vector2(1, 0)); second = Enemy(new Vector2(1.5f, .45f)); third = Enemy(new Vector2(1.5f, -.45f)); Physics2D.SyncTransforms();
        player.ItemEffects.OnManaSpent(10, Vector2.right); yield return new WaitForSeconds(.25f);
        Require(player.ItemEffects.EchoContactsResolved == 3, "top arcanist set changes echo from one to three victims");
        player.ItemEffects.OnManaSpent(10, Vector2.right); player.ItemEffects.OnManaSpent(10, Vector2.right);
        Require(Mathf.Abs(player.Mana - upgradedSetMana - 3) < .01f, "top arcanist changes third-cast refund based on final mana cost");
        player.SetStageClearSafety(true); yield return null;
        Require(player.ItemEffects.ActiveShards == 0 && player.ItemEffects.Shield == 0, "stage safety clears transient gameplay state");
        player.SetStageClearSafety(false); player.ItemEffects.OnManaSpent(10, Vector2.right); yield return new WaitForSeconds(.25f);
        Require(player.ItemEffects.EchoContactsResolved == 3, "stage safety cannot reset still-running echo cooldown");
        // RuntimeSceneObjects is internal: invoke the production cleanup through its exact assembly type.
        typeof(PlayerController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null, null);
        yield return null;
        Require(UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length == 0 && GameObject.Find("RecoveryShard") == null, "production session cleanup removes native proc visuals and pickups");
        // Death cleanup is checked with an actual incoming lethal attack, without invulnerability.
        Field("invincibilityTicks", 0); player.TakeDamage(2000, Vector2.left); yield return null;
        Require(player.IsDead && player.ItemEffects.ActiveShards == 0, "real player death leaves no recoverable item effect");
        var scaled = new GameObject("Scaled native FX parent"); scaled.transform.localScale = new Vector3(.5f, 2f, 1f);
        var child = NativeCombatEffect.Create("Energy", Vector2.zero, 0, .32f, 0, Color.white, 2000, scaled.transform, true);
        var independent = NativeCombatEffect.Create("Energy", Vector2.zero, 0, .32f, 0, Color.white, 2000, null, true);
        Require(Vector3.Distance(child.GetComponent<SpriteRenderer>().bounds.size, independent.GetComponent<SpriteRenderer>().bounds.size) < .001f,
            "scaled enemy/pickup parent cannot stretch or shrink authored effect pixels");
        UnityEngine.Object.Destroy(scaled); UnityEngine.Object.Destroy(independent.gameObject);
        NewCase("scrap_pistol"); enemy = Enemy(new Vector2(1.4f, 0)); int impacts = 0;
        game.CameraFollow.ConfigureCombatFeedback((critical, bossHit) => impacts++, () => {});
        yield return new WaitForSeconds(.16f); enemy.TakeDamage(4, Vector2.zero, true);
        Require(impacts == 1, "original critical hit reaches camera feedback positive control");
        yield return new WaitForSeconds(.16f); enemy.TakeItemDamage(4, Vector2.zero);
        Require(impacts == 1, "item damage cannot occupy original attack camera impact throttle");
        enemy.TakeDamage(4, Vector2.zero, true);
        Require(impacts == 2, "critical hit immediately after item damage retains primary impact feedback");
    }
}
