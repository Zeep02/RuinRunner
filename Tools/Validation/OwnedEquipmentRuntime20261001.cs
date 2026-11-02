using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Supplied equipment and controlled targets; real production transactions, HUD and attack methods.
// These checks do not establish normal campaign/build/performance completion.
public static class OwnedEquipmentRuntime20261001
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    static GameController game; static PlayerController player; static InventorySystem bag; static PrototypeHud hud;
    static string folder; static Vector2 center; static EditorWindow view; static object sizes; static int oldSize, oldCustomCount;
    static Type autoTick; static bool oldTickEnabled; static long oldTickInterval;
    static EquipmentHudPaint20261001 activePaint;
    static EquipmentMouseProbe20261001 activeProbe;
    static readonly List<int> customSizes = new List<int>(); static readonly List<string> checks = new List<string>();
    static readonly List<GameObject> walls = new List<GameObject>();
    static void Require(bool okay, string label) { if (!okay) throw new Exception(label); checks.Add("PASS " + label); }
    static void Prop(string name, object value) => typeof(GameController).GetProperty(name).SetValue(game, value);
    static void Field(string name, object value) => typeof(GameController).GetField(name, P).SetValue(game, value);
    static object Call(string name, params object[] args) => typeof(PlayerController).GetMethod(name, P).Invoke(player, args);
    static void Clear()
    {
        player.ItemEffects.CancelTransient();
        foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>()) { e.gameObject.SetActive(false); UnityEngine.Object.Destroy(e.gameObject); }
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) { p.gameObject.SetActive(false); UnityEngine.Object.Destroy(p.gameObject); }
        foreach (var wall in walls) { wall.SetActive(false); UnityEngine.Object.Destroy(wall); } walls.Clear();
    }
    static void Shots()
    { foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) { p.gameObject.SetActive(false); UnityEngine.Object.Destroy(p.gameObject); } }
    static void Equip(string id, int level = 2, params AccessoryType[] accessories)
    {
        Clear(); bag.Reset(); bag.AddWeapon(WeaponLootTable.Find(id));
        foreach (var a in accessories) bag.AddAccessory(AccessoryLootTable.Find(a));
        bag.AwardMaterials(200, "staged-test-only");
        for (int i = 0; i < level; i++) Require(bag.TryUpgrade(bag.ActiveSlot.Item.Identity, i), "prepare actual owned level " + id + " +" + (i + 1));
        player.Teleport(center); player.EquipWeapon(bag.EquippedWeapon); player.Stats.RestoreMana(player.MaxMana);
        Physics2D.SyncTransforms();
    }
    static EnemyController Enemy(Vector2 offset)
    {
        var e = EnemyController.Create(EnemyType.Grunt, center + offset, player, () => false, dead => player.OnEnemyDefeated(dead), game.CameraFollow);
        e.enabled = false; e.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        typeof(EnemyController).GetProperty("Health").SetValue(e, 1000f); return e;
    }
    static void Wall(float x)
    {
        var w = new GameObject("Upgrade validation wall"); w.layer = GameLayers.Ground; w.transform.position = center + Vector2.right * x;
        w.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 6); walls.Add(w); Physics2D.SyncTransforms();
    }
    static void UpgradeAccessory(AccessoryType type, int level)
    {
        foreach (var a in bag.OwnedAccessories) if (a.Definition.Type == type)
        { for (int i = a.UpgradeLevel; i < level; i++) Require(bag.TryUpgrade(a.Identity, i), "prepare accessory " + type + " +" + (i + 1)); return; }
        throw new Exception("Missing accessory");
    }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-equipment-upgrades/runtime-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder);
        checks.Clear(); customSizes.Clear(); walls.Clear();
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
            if (component.GetType().Name == "EquipmentHudPaint20261001" || component.GetType().Name == "EquipmentMouseProbe20261001") UnityEngine.Object.Destroy(component);
        game = UnityEngine.Object.FindAnyObjectByType<GameController>(); game.ReturnToMainMenu(); game.SaveSlots.OpenTransientForTests();
        game.enabled = true; hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>(); hud.enabled = true;
        typeof(GameController).GetMethod("BeginRunForActiveSlot", P).Invoke(game, null);
        Application.runInBackground = true;
        view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.Focus();
        oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", All).GetValue(view);
        var t = Type.GetType("UnityEditor.GameViewSizes,UnityEditor"); var instance = t.GetProperty("instance", All).GetValue(null);
        sizes = t.GetMethod("GetGroup", All).Invoke(instance, new[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"), "Standalone") });
        oldCustomCount = (int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { autoTick = assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand"); if (autoTick != null) break; }
        oldTickEnabled = (bool)autoTick.GetProperty("IsEnabled", All).GetValue(null); oldTickInterval = (long)autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null);
        autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { true, 0, false });
        game.CameraFollow.StartCoroutine(Guard(Run())); return folder;
    }
    static void Resolution(int width, int height)
    {
        var t = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var size = Activator.CreateInstance(t, new object[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"), "FixedResolution"), width, height, "Equipment QA " + width + "x" + height });
        int index = (int)sizes.GetType().GetMethod("GetTotalCount", All).Invoke(sizes, null);
        sizes.GetType().GetMethod("AddCustomSize", All).Invoke(sizes, new[] { size }); customSizes.Add(index);
        view.GetType().GetMethod("SizeSelectionCallback", All).Invoke(view, new object[] { index, null }); view.Repaint();
    }
    static void RestoreView()
    {
        autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTickEnabled, (int)oldTickInterval, false });
        view.GetType().GetMethod("SizeSelectionCallback", All).Invoke(view, new object[] { oldSize, null });
        for (int i = customSizes.Count - 1; i >= 0; i--) sizes.GetType().GetMethod("RemoveCustomSize", All).Invoke(sizes, new object[] { customSizes[i] });
        Require((int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null) == oldCustomCount, "restored original Game View presets");
    }
    static void Capture(string name)
    { var t = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(folder + "/" + name + ".png", t.EncodeToPNG()); UnityEngine.Object.Destroy(t); }
    static IEnumerator Click(Vector2 point)
    {
        // Deliver through the actual GameView's editor event handler, including its viewport zoom/offset.
        // Calling the native input queue alone did not dispatch OnGUI mouse events in this editor context.
        var offset = (Vector2)view.GetType().GetProperty("gameMouseOffset", All).GetValue(view);
        float mouseScale = (float)view.GetType().GetProperty("gameMouseScale", All).GetValue(view);
        var local = point / mouseScale - offset;
        int received = activeProbe.MouseEvents;
        var calibration = new Vector2(64, 64); // Outside every inventory/tooltip control.
        var calibrationLocal = calibration / mouseScale - offset;
        var calibrationDown = new Event { type = EventType.MouseDown, button = 0, mousePosition = calibrationLocal };
        bool delivered = false;
        EditorApplication.delayCall += () => { view.SendEvent(calibrationDown); delivered = true; };
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!delivered && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(.12f);
        Require(activeProbe.MouseEvents > received, "native GameView delivers viewport calibration event");
        // EditorWindow.SendEvent also crosses the parent tab/header offset. Calibrate against
        // the actual runtime OnGUI event rather than assuming an OS window decoration size.
        local += (calibration - activeProbe.LastMouse) / mouseScale;
        var calibrationUp = new Event { type = EventType.MouseUp, button = 0, mousePosition = calibrationLocal };
        delivered = false;
        EditorApplication.delayCall += () => { view.SendEvent(calibrationUp); delivered = true; };
        deadline = Time.realtimeSinceStartup + 3f;
        while (!delivered && Time.realtimeSinceStartup < deadline) yield return null;
        Require(delivered, "native GameView delivers calibration release");
        yield return new WaitForSecondsRealtime(.12f);
        var down = new Event { type = EventType.MouseDown, button = 0, mousePosition = local };
        delivered = false;
        EditorApplication.delayCall += () => { view.SendEvent(down); delivered = true; };
        deadline = Time.realtimeSinceStartup + 3f;
        while (!delivered && Time.realtimeSinceStartup < deadline) yield return null;
        Require(delivered, "native GameView delivers button press");
        yield return new WaitForSecondsRealtime(.12f);
        var up = new Event { type = EventType.MouseUp, button = 0, mousePosition = local };
        delivered = false;
        EditorApplication.delayCall += () => { view.SendEvent(up); delivered = true; };
        deadline = Time.realtimeSinceStartup + 3f;
        while (!delivered && Time.realtimeSinceStartup < deadline) yield return null;
        Require(delivered, "native GameView delivers button release");
        yield return new WaitForSecondsRealtime(.12f);
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            object value = null; bool next;
            try { next = run.MoveNext(); if (next) value = run.Current; }
            catch (Exception error)
            {
                RestoreView(); if (activePaint != null) UnityEngine.Object.Destroy(activePaint); hud.enabled = true;
                if (activeProbe != null) UnityEngine.Object.Destroy(activeProbe);
                File.WriteAllText(folder + "/result.txt", "FAIL " + error + "\n" + string.Join("\n", checks));
                game.ReturnToMainMenu(); Debug.LogException(error); yield break;
            }
            if (!next) break; yield return value;
        }
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged owned equipment / real attack / native HUD checks; not normal campaign/build proof\n" + string.Join("\n", checks));
        Debug.Log("[Owned equipment runtime] PASS " + folder);
    }
    static IEnumerator Run()
    {
        yield return null; game.StopAllCoroutines(); game.enabled = false; Prop("StageIntroUntil", 0f);
        foreach (var t in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>()) UnityEngine.Object.Destroy(t.gameObject);
        player = game.Player; player.enabled = false; bag = game.Inventory; center = game.DungeonMap.GetRoomCenter(0); player.Teleport(center); Clear();
        Field("waveTransition", false); Prop("EnemiesAlive", 0); Prop("IsInventoryOpen", true); Time.timeScale = 0;
        game.FocusEquipment(bag.ActiveSlot.Item.Identity);
        var award = typeof(GameController).GetMethod("AwardEncounterMaterials", P);
        award.Invoke(game, new object[] { 0 }); award.Invoke(game, new object[] { 0 });
        Require(bag.UpgradeMaterials == 8, "production first-region reward grants eight materials once");
        var first = bag.ActiveSlot.Item; Prop("EnemiesAlive", 1);
        Require(!game.UpgradeSelectedEquipment(first.Identity, 0) && bag.UpgradeMaterials == 8, "combat blocks upgrade without payment");
        Prop("EnemiesAlive", 0);
        Require(game.UpgradeSelectedEquipment(first.Identity, 0) && bag.UpgradeMaterials == 4, "first regular clear pays real +1 upgrade");
        bag.AwardMaterials(100, "staged-extra-for-transaction-test"); int wallet = bag.UpgradeMaterials;
        Require(!game.UpgradeSelectedEquipment(first.Identity, 1) && bag.UpgradeMaterials == wallet, "same-frame upgrade repeat cannot charge next tier");
        yield return new WaitForSecondsRealtime(.35f);
        bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.RubyHeart)); var a = bag.OwnedAccessories[0];
        game.FocusEquipment(a.Identity); Require(game.RequestEquipmentDisassembly(a.Identity, 0), "selected copy enters destructive confirmation");
        game.FocusEquipment(first.Identity); Require(!game.ConfirmEquipmentDisassembly() && bag.FindOwned(a.Identity) == a, "selection change cancels old disassembly");
        game.FocusEquipment(a.Identity); game.RequestEquipmentDisassembly(a.Identity, 0); game.CancelEquipmentDisassembly();
        Require(!game.ConfirmEquipmentDisassembly() && bag.FindOwned(a.Identity) == a, "explicit cancel preserves copy and wallet");
        game.RequestEquipmentDisassembly(a.Identity, 0); bag.MoveBagItem(0, 4);
        Require(!game.ConfirmEquipmentDisassembly() && bag.FindOwned(a.Identity) == a, "inventory revision invalidates pending destructive request");
        game.RequestEquipmentDisassembly(a.Identity, 0); int before = bag.UpgradeMaterials;
        Require(game.ConfirmEquipmentDisassembly() && !game.ConfirmEquipmentDisassembly() && bag.UpgradeMaterials == before + 2, "production confirmed disassembly pays once");
        Prop("IsInventoryOpen", false); Time.timeScale = 1; game.CameraFollow.ConfigureCombatFeedback((critical, boss) => {}, () => {});

        Equip("hunter_bow"); Call("TryRangedWeaponAction"); Require(UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length == 5, "+2 bow emits five real arrow colliders"); Shots();
        Equip("scrap_shotgun"); Call("TryRangedWeaponAction"); Require(UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length == 9, "+2 shotgun emits nine special pellets"); Shots();
        Call("RangedAttack"); Require(UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length == 3, "+2 shotgun keeps regular three-pellet action"); Shots();
        Equip("hand_crossbow"); Call("TryRangedWeaponAction");
        var shot = UnityEngine.Object.FindAnyObjectByType<PlayerProjectile>(); Require((int)typeof(PlayerProjectile).GetField("remainingHits", P).GetValue(shot) == 8, "+2 crossbow has eight real pierce contacts"); Shots();
        Equip("storm_rifle"); Call("RangedAttack"); shot = UnityEngine.Object.FindAnyObjectByType<PlayerProjectile>();
        Require((int)typeof(PlayerProjectile).GetField("remainingHits", P).GetValue(shot) == WeaponLootTable.Find("storm_rifle").ProjectilePierce + 1, "+2 rifle regular pierce gains one"); Shots();
        Equip("scrap_pistol"); float mana = player.Mana; Call("TryRangedWeaponAction");
        // Retain each real emitted round for counting; the native map wall otherwise destroys early rounds.
        // Background Editor frame pacing must not truncate a four-round WaitForSeconds sequence.
        float burstDeadline = Time.realtimeSinceStartup + 6;
        while (Time.realtimeSinceStartup < burstDeadline)
        {
            foreach (var round in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) round.enabled = false;
            if (UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length >= 4) break;
            // Coroutine-created rounds are retained at frame end, before the next physics catch-up step.
            yield return new WaitForEndOfFrame();
        }
        int burstCount = UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length;
        Require(burstCount == 4 && Mathf.Abs(player.Mana - mana + bag.EquippedWeapon.SpecialManaCost) < .01f, "+2 pistol commits four rounds with one mana payment; rounds=" + burstCount + " mana=" + player.Mana); Shots();
        yield return new WaitForSeconds(.8f); Call("TryRangedWeaponAction"); yield return new WaitForSeconds(.025f); player.EquipWeapon(WeaponLootTable.Find("rust_sword")); Shots(); yield return new WaitForSeconds(.25f);
        Require(UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length == 0, "equipment switch cancels remaining upgraded burst");

        Equip("dragon_cannon", 0); var victim = Enemy(new Vector2(1.6f, 0)); Physics2D.SyncTransforms();
        var blast = typeof(PlayerProjectile).GetMethod("Blast", P);
        shot = PlayerProjectile.Create(center, Vector2.right, 0, 20, false, Color.white, new Vector2(.2f, .2f), 1, WeaponFamily.Cannon);
        blast.Invoke(shot, new object[] { center }); Require(victim.Health == 1000, "unupgraded cannon radius excludes distant contact"); Shots();
        bag.TryUpgrade(bag.ActiveSlot.Item.Identity, 0); bag.TryUpgrade(bag.ActiveSlot.Item.Identity, 1);
        shot = PlayerProjectile.Create(center, Vector2.right, 0, 20, false, Color.white, new Vector2(.2f, .2f), 1, WeaponFamily.Cannon, false, "dragon_cannon", player, 200, 2);
        blast.Invoke(shot, new object[] { center }); Require(victim.Health < 1000, "+2 cannon expanded blast contacts distant victim"); Shots();
        Wall(.65f); victim = Enemy(new Vector2(1.4f, .3f)); Physics2D.SyncTransforms();
        shot = PlayerProjectile.Create(center, Vector2.right, 0, 20, false, Color.white, new Vector2(.2f, .2f), 1, WeaponFamily.Cannon, false, "dragon_cannon", player, 201, 2);
        blast.Invoke(shot, new object[] { center }); Require(victim.Health == 1000, "expanded blast still respects solid wall"); Shots();

        Equip("iron_mace", 3); player.Stats.TrySpendMana(10); mana = player.Mana; victim = Enemy(new Vector2(1.4f, 0)); Wall(.65f); Physics2D.SyncTransforms();
        Call("SpecialNova", 10f, false, Color.white); Require(player.Mana == mana && victim.Health == 1000, "+3 blocked melee cannot refund mana");
        foreach (var w in walls) { w.SetActive(false); UnityEngine.Object.Destroy(w); } walls.Clear(); Physics2D.SyncTransforms();
        Call("SpecialNova", 10f, false, Color.white); Require(Mathf.Abs(player.Mana - mana - 2) < .01f && victim.Health < 1000, "+3 actual melee contact refunds two mana once");

        Equip("scrap_pistol", 0, AccessoryType.BerserkerEmblem); UpgradeAccessory(AccessoryType.BerserkerEmblem, 2);
        victim = Enemy(new Vector2(1.4f, 0)); Physics2D.SyncTransforms();
        player.ItemEffects.OnOriginalHit(victim, 301, 100); player.ItemEffects.OnOriginalHit(victim, 302, 100); yield return new WaitForSeconds(.7f);
        Require(player.ItemEffects.BleedTicksResolved > 0, "+2 emblem actually applies bleed after two unique source actions");
        var emblem = bag.OwnedAccessories[0]; bag.TryDisassemble(emblem.Identity, 2, out _); float remainingHealth = victim.Health;
        yield return new WaitForSeconds(.8f); Require(victim.Health == remainingHealth, "dismantling final emblem removes pending bleed immediately");
        Equip("scrap_pistol", 0, AccessoryType.SoulCapacitor); UpgradeAccessory(AccessoryType.SoulCapacitor, 2);
        for (int i = 0; i < 4; i++) Enemy(new Vector2(1f + .7f * i, i % 2 == 0 ? .2f : -.2f)); Physics2D.SyncTransforms();
        player.ItemEffects.OnManaSpent(10, Vector2.right); Require(player.ItemEffects.ChainContactsResolved == 4, "+2 soul capacitor chains four unique real victims");
        var capacitor = bag.OwnedAccessories[0]; bag.TryDisassemble(capacitor.Identity, 2, out _); bag.AddAccessory(capacitor.Definition);
        player.ItemEffects.OnManaSpent(10, Vector2.right); Require(player.ItemEffects.ChainContactsResolved == 4, "remove/reacquire cannot reset shared chain cooldown");

        Equip("scrap_pistol", 0, AccessoryType.BerserkerEmblem, AccessoryType.RubyHeart, AccessoryType.CrimsonRing);
        victim = Enemy(new Vector2(1.4f, 0)); Physics2D.SyncTransforms();
        for (int i = 0; i < 3; i++) player.ItemEffects.OnOriginalHit(victim, 500 + i, 100);
        var removedEmblem = bag.OwnedAccessories[0]; bag.TryDisassemble(removedEmblem.Identity, 0, out _); remainingHealth = victim.Health;
        yield return new WaitForSeconds(.7f);
        Require(bag.GetSetPieceCount(ItemSetType.BloodMoon) == 2 && victim.Health == remainingHealth, "remaining blood pair does not retain dismantled emblem's stronger pending bleed");
        Equip("scrap_pistol", 0, AccessoryType.VampireFang, AccessoryType.VampireFang);
        victim = Enemy(new Vector2(1.4f, 0)); player.ItemEffects.OnEnemyDefeated(victim);
        Require(player.ItemEffects.ActiveShards == 1, "duplicate fang prepares one shared recovery shard");
        bag.TryDisassemble(bag.OwnedAccessories[0].Identity, 0, out _);
        Require(player.ItemEffects.ActiveShards == 0, "removing a fang copy removes former duplicate-strength shard");
        player.ItemEffects.OnEnemyDefeated(victim); Require(player.ItemEffects.ActiveShards == 0, "removing one duplicate does not restart recovery cooldown");

        Equip("scrap_pistol", 0); bag.AddWeapon(WeaponLootTable.Find("rust_sword")); bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));
        for (int i = 0; i < bag.UnlockedCapacity; i++) bag.AddAccessory(AccessoryLootTable.Find(AccessoryType.LuckyDice));
        float beforeLuck = player.Stats.Luck;
        game.Progression.AddExperience(game.Progression.RequiredExperience); var choice = game.CurrentAccessoryChoices[0];
        game.SelectAccessory(choice); game.SelectAccessory(choice);
        var drops = UnityEngine.Object.FindObjectsByType<FieldLootPickup>();
        Require(drops.Length == 1 && bag.StoredItemCount == bag.UnlockedCapacity && player.Stats.Luck == beforeLuck && !game.IsChoosingAccessory,
            "full-bag real level reward becomes one field drop without lost reward, duplicate gain or modal softlock");
        var collect = (Func<bool>)typeof(FieldLootPickup).GetField("collect", P).GetValue(drops[0]);
        Require(!collect() && drops[0] != null, "full-bag production field callback refuses without consuming drop");
        var spare = bag.OwnedAccessories[0]; bag.TryDisassemble(spare.Identity, 0, out _);
        Require(collect() && bag.StoredItemCount == bag.UnlockedCapacity && bag.OwnedAccessories[bag.OwnedAccessories.Count - 1].Definition == choice,
            "after disassembly the deferred selected reward enters the freed real bag cell");
        UnityEngine.Object.Destroy(drops[0].gameObject); yield return null;
        var reward = RunContent.RollChest(game.RunSeed, 0, player.Stats.Luck, false);
        typeof(GameController).GetMethod("OpenRoomChest", P).Invoke(game, new object[] { 0, reward });
        drops = UnityEngine.Object.FindObjectsByType<FieldLootPickup>();
        int expectedDrops = 1 + (reward.BonusWeapon == null ? 0 : 1) + (reward.Accessory == null ? 0 : 1);
        Require(drops.Length == expectedDrops, "full bag still receives every production chest field reward");
        foreach (var drop in drops)
        {
            collect = (Func<bool>)typeof(FieldLootPickup).GetField("collect", P).GetValue(drop);
            Require(!collect(), "full-bag chest reward remains available on failed collection");
            UnityEngine.Object.Destroy(drop.gameObject);
        }
        foreach (var orb in UnityEngine.Object.FindObjectsByType<ExperienceOrb>()) UnityEngine.Object.Destroy(orb.gameObject);
        yield return null;

        Equip("scrap_pistol", 3, AccessoryType.SageSigil, AccessoryType.ArcaneLens, AccessoryType.VampireFang, AccessoryType.BerserkerEmblem, AccessoryType.SoulCapacitor);
        foreach (var type in new[] { AccessoryType.SageSigil, AccessoryType.ArcaneLens, AccessoryType.VampireFang, AccessoryType.BerserkerEmblem, AccessoryType.SoulCapacitor }) UpgradeAccessory(type, 3);
        bag.AddWeapon(WeaponLootTable.Find("scrap_pistol")); var copy = bag.QuickSlots[1].Item;
        var upgraded = bag.QuickSlots[0].Item; player.EquipWeapon(bag.EquippedWeapon);
        game.CameraFollow.FocusRoom(center, new Vector2(20, 14), true); Prop("IsInventoryOpen", true); Time.timeScale = 0;
        var paint = activePaint = game.CameraFollow.gameObject.AddComponent<EquipmentHudPaint20261001>(); paint.Hud = hud; hud.enabled = false;
        foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1280, 800) })
        {
            Resolution(resolution.x, resolution.y); yield return null; yield return new WaitForEndOfFrame();
            Require(Screen.width == resolution.x && Screen.height == resolution.y, "native equipment screen resolution " + resolution);
            game.FocusEquipment(upgraded.Identity); yield return new WaitForEndOfFrame(); Capture("weapon-max-" + resolution.x + "x" + resolution.y);
            game.FocusEquipment(copy.Identity); yield return new WaitForEndOfFrame(); Capture("duplicate-unupgraded-" + resolution.x + "x" + resolution.y);
            game.FocusEquipment(bag.OwnedAccessories[0].Identity); game.RequestEquipmentDisassembly(bag.OwnedAccessories[0].Identity, 3); yield return new WaitForEndOfFrame(); Capture("disassembly-confirm-" + resolution.x + "x" + resolution.y); game.CancelEquipmentDisassembly();
            for (int i = 0; i < bag.OwnedAccessories.Count; i++)
            { paint.Tooltip = "A:" + i; yield return new WaitForEndOfFrame(); Capture("tooltip-" + bag.OwnedAccessories[i].Definition.Id + "-" + resolution.x + "x" + resolution.y); }
            paint.Tooltip = null;
        }
        var style = (GUIStyle)typeof(PrototypeHud).GetField("detailStyle", P).GetValue(hud);
        foreach (var item in bag.OwnedAccessories)
        {
            string text = EquipmentUpgradeRules.AccessoryDescription(item, bag);
            string set = (string)typeof(PrototypeHud).GetMethod("GetSetProgressText", P).Invoke(hud, new object[] { item.Definition.SetType });
            float tooltipHeight = 150 + style.CalcHeight(new GUIContent(text), 392) + style.CalcHeight(new GUIContent(set), 392);
            Require(tooltipHeight <= 704, "upgraded tooltip fits 720px " + item.Definition.Id + " height=" + tooltipHeight);
        }
        paint.enabled = false; hud.enabled = true;
        activeProbe = game.CameraFollow.gameObject.AddComponent<EquipmentMouseProbe20261001>();
        game.FocusEquipment(copy.Identity); int uiWallet = bag.UpgradeMaterials;
        float scale = GameTypography.LayoutScale; float width = Screen.width / scale, height = Screen.height / scale;
        var click = Click(new Vector2(width / 2 - 500 + 650 + 160, height / 2 - 310 + 80 + 280) * scale);
        while (click.MoveNext()) yield return click.Current;
        Require(copy.UpgradeLevel == 1 && upgraded.UpgradeLevel == 3 && bag.UpgradeMaterials == uiWallet - 4,
            "native GameView mouse down/up upgrades only selected copy; level=" + copy.UpgradeLevel + " wallet=" + bag.UpgradeMaterials + " expected=" + (uiWallet - 4) + " ready=" + game.EquipmentOperationReady + " selected=" + game.SelectedEquipment?.Identity + " copy=" + copy.Identity + " events=" + activeProbe.Events);
        // Let native GameView mouse rendering and the hidden tooltip's former surface
        // settle across complete repaint frames before taking the composited screenshot.
        yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame();
        Capture("real-ui-upgrade-1280x800");
        var settled = ScreenCapture.CaptureScreenshotAsTexture(); var settledPixels = settled.GetPixels32();
        int marginHeight = Mathf.FloorToInt(game.CameraFollow.GetComponent<Camera>().pixelRect.y); int stalePixels = 0;
        for (int y = 0; y < marginHeight; y++) for (int x = settled.width - 180; x < settled.width; x++)
        { var pixel = settledPixels[y * settled.width + x]; if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0) stalePixels++; }
        UnityEngine.Object.Destroy(settled);
        Require(marginHeight > 0 && stalePixels == 0, "hidden tooltip leaves no stale colored pixels in native letterbox margin; pixels=" + stalePixels);
        yield return new WaitForSecondsRealtime(.35f);
        click = Click(new Vector2(width / 2 - 500 + 650 + 160, height / 2 - 310 + 80 + 330) * scale);
        while (click.MoveNext()) yield return click.Current;
        Require(game.HasDisassemblyConfirmation, "production IMGUI disassembly opens confirmation without removing copy; events=" + activeProbe.Events);
        click = Click(new Vector2(width / 2 - 500 + 650 + 231, height / 2 - 310 + 80 + 350) * scale);
        while (click.MoveNext()) yield return click.Current;
        Require(!game.HasDisassemblyConfirmation && bag.FindOwned(copy.Identity) == copy, "production IMGUI cancel preserves selected upgraded copy");
        UnityEngine.Object.Destroy(paint); hud.enabled = true;
        UnityEngine.Object.Destroy(activeProbe);
        // Force only the transition entry; verify the actual production teardown/load path preserves owned data.
        Prop("IsInventoryOpen", false); Time.timeScale = 1; bag.TryConsumeAmmo(); bag.StartReload();
        int ammo = upgraded.Ammo; float reload = upgraded.ReloadRemaining; int materials = bag.UpgradeMaterials;
        player.ItemEffects.OnManaSpent(10, Vector2.right);
        var echoReady = typeof(ItemCombatEffects).GetField("echoReady", P); float cooldown = (float)echoReady.GetValue(player.ItemEffects);
        typeof(GameController).GetMethod("BeginNextStage", P).Invoke(game, null);
        float deadline = Time.realtimeSinceStartup + 8;
        while (game.IsStageTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
        game.StopAllCoroutines();
        foreach (var t in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>()) UnityEngine.Object.Destroy(t.gameObject);
        Require(game.CurrentStageIndex == 1 && !game.IsStageTransitioning && ReferenceEquals(player, game.Player) && ReferenceEquals(bag, game.Inventory), "production stage transition retains player and inventory owner");
        Require(ReferenceEquals(upgraded, bag.QuickSlots[0].Item) && ReferenceEquals(copy, bag.QuickSlots[1].Item) && upgraded.UpgradeLevel == 3 && copy.UpgradeLevel == 1 && upgraded.Ammo == ammo && upgraded.ReloadRemaining == reload,
            "stage transition preserves upgraded/duplicate identities, separate levels, partial ammo and reload");
        Require(bag.UpgradeMaterials == materials && (float)echoReady.GetValue(player.ItemEffects) == cooldown, "stage transition preserves material wallet and shared item cooldown");
        game.ReturnToMainMenu();
        Require(bag.UpgradeMaterials == 0 && bag.OwnedAccessories.Count == 0, "return to menu clears run-only equipment and material wallet");
    }
}

[DefaultExecutionOrder(-10000)]
public sealed class EquipmentMouseProbe20261001 : MonoBehaviour
{
    public string Events = "";
    public Vector2 LastMouse; public int MouseEvents; bool resetCapture = true;
    void OnGUI()
    {
        if (resetCapture) { GUIUtility.hotControl = 0; resetCapture = false; }
        if (Event.current.rawType == EventType.MouseMove || Event.current.rawType == EventType.MouseDown || Event.current.rawType == EventType.MouseUp)
        {
            LastMouse = Event.current.mousePosition; MouseEvents++;
            Events += " [" + Event.current.type + "/" + Event.current.rawType + " mouse=" + Event.current.mousePosition + " hot=" + GUIUtility.hotControl + " enabled=" + GUI.enabled + "]";
        }
    }
}

public sealed class EquipmentHudPaint20261001 : MonoBehaviour
{
    public PrototypeHud Hud; public string Tooltip;
    static readonly BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    void OnGUI()
    {
        var matrix = GUI.matrix; var mouse = Event.current.mousePosition;
        float scale = GameTypography.LayoutScale; float width = Screen.width / scale, height = Screen.height / scale;
        Event.current.mousePosition = Vector2.zero;
        typeof(PrototypeHud).GetMethod("OnGUI", P).Invoke(Hud, null);
        if (Tooltip != null)
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1)); Event.current.mousePosition = new Vector2(width - 490, height - 100);
            typeof(PrototypeHud).GetMethod("DrawItemTooltip", P).Invoke(Hud, new object[] { width, height, Tooltip });
        }
        GUI.matrix = matrix; Event.current.mousePosition = mouse;
    }
}
