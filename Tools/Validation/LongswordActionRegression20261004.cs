using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using RuinRunner;

// Isolated input/collider regression, not a normal run or an art-quality pass.
// Run Main in Play Mode through the parent's existing editor execution tool.
public static class LongswordActionRegression20261004
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static PlayerController player;
    static EnemyController target;
    static CameraFollow camera;
    static Action previousFinisherCallback;
    static int finisherImpacts;
    static bool paused;
    static bool trackRegeneration;
    static float regenerationDelta;
    static int regenerationFrame;
    static string folder;
    static float Clock => Time.realtimeSinceStartup;
    static void Check(bool condition, string label)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + label);
        if (!condition) throw new Exception(label);
    }
    static T Field<T>(string field) => (T)typeof(PlayerController).GetField(field, Private).GetValue(player);
    static void Hold(GameAction action, bool held) => StageValidationInput.SetHeld(action, held);
    static void ObserveFrame()
    {
        StageValidationInput.BeginFrame();
        if (trackRegeneration && regenerationFrame != Time.frameCount)
        { regenerationDelta += Time.deltaTime; regenerationFrame = Time.frameCount; }
    }
    static IEnumerator Wait(float seconds)
    {
        var until = Clock + seconds;
        while (Clock < until) { yield return null; ObserveFrame(); }
    }
    static IEnumerator Frame()
    {
        yield return null;
        ObserveFrame();
    }
    static void Fresh()
    {
        StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        if (target != null) UnityEngine.Object.Destroy(target.gameObject);
        if (player != null) UnityEngine.Object.Destroy(player.gameObject);
        paused = false;
        trackRegeneration = false;
        finisherImpacts = 0;
        var inventory = new InventorySystem();
        inventory.AddWeapon(WeaponLootTable.Find("rust_sword"));
        var settings = new GameSettingsService(folder + "/settings.json", false);
        player = PlayerController.Create(Vector2.zero, () => paused, () => false, () => { }, camera, inventory, settings);
        player.EquipWeapon(WeaponLootTable.Find("rust_sword"));
        camera.Target = player.transform;
        target = EnemyController.Create(EnemyType.Boss, new Vector2(1.05f, 0), player, () => true, _ => { }, null);
        target.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        StageValidationInput.SetAim(Vector2.right);
        Physics2D.SyncTransforms();
    }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-04-sword-action/" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder); checks.Clear();
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu(); game.enabled = false; camera = game.CameraFollow;
        var callback = typeof(CameraFollow).GetField("finisherImpactCallback", Private);
        previousFinisherCallback = (Action)callback.GetValue(camera);
        callback.SetValue(camera, (Action)(() => { finisherImpacts++; previousFinisherCallback?.Invoke(); }));
        typeof(StageValidationInput).GetField("enabled", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        typeof(StageValidationInput).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        Application.runInBackground = true; Time.timeScale = 1; Time.captureFramerate = 0;
        camera.StartCoroutine(Guard(Run(), game));
        return folder;
    }
    static IEnumerator Guard(IEnumerator run, GameController game)
    {
        string error = null;
        // Flatten nested iterators so fixture exceptions also trigger cleanup.
        var stack = new Stack<IEnumerator>(); stack.Push(run);
        while (stack.Count > 0)
        {
            object value = null; bool next;
            try { next = stack.Peek().MoveNext(); if (next) value = stack.Peek().Current; }
            catch (Exception e) { error = e.ToString(); break; }
            if (!next) { stack.Pop(); continue; }
            if (value is IEnumerator child) { stack.Push(child); continue; }
            yield return value;
        }
        paused = false; StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        if (target != null) UnityEngine.Object.Destroy(target.gameObject);
        if (player != null) UnityEngine.Object.Destroy(player.gameObject);
        typeof(CameraFollow).GetField("finisherImpactCallback", Private).SetValue(camera, previousFinisherCallback);
        game.ReturnToMainMenu(); game.enabled = true; Time.timeScale = 1;
        File.WriteAllText(folder + "/result.txt", (error == null ? "PASS" : "FAIL " + error) + "\n" +
            string.Join("\n", checks) + "\nIsolated inputs and damage only; native motion still requires visual review.\n", new UTF8Encoding(false));
        Debug.Log("[Sword Action] " + (error == null ? "PASS " + checks.Count : "FAIL " + error));
    }
    static IEnumerator Run()
    {
        yield return Frame(); Fresh(); yield return Frame();
        var hp = target.Health;
        Hold(GameAction.Attack, true);
        var seen = new HashSet<int>(); var deadline = Clock + 2;
        while (seen.Count < 3 && Clock < deadline)
        {
            yield return Frame();
            if (player.IsMeleeSwingActive)
            {
                if (seen.Add(player.ActiveComboAnimationStep))
                    Check(player.ActiveMeleeMotion == MeleeAttackMotion.Sweep, "ordinary sword uses native sweep at step " + player.ActiveComboAnimationStep);
                if (player.ActiveComboAnimationStep == 2) Hold(GameAction.Attack, false);
            }
        }
        Hold(GameAction.Attack, false); yield return Wait(.42f);
        Check(seen.Count == 3 && player.MeleeContactsResolved == 3, "three held primary steps resolve exactly three contacts");
        Check(target.Health < hp, "sword contacts damage a real collider");
        Check(finisherImpacts == 1, "three-hit combo emphasizes only its landed finisher once");

        Fresh(); yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); Hold(GameAction.Attack, false);
        var committed = player.MeleeAimDirection;
        StageValidationInput.SetAim(Vector2.left); yield return Frame();
        Check(Vector2.Dot(player.MeleeAimDirection, committed) > .99f, "current swing commits aim after input changes");
        Hold(GameAction.MoveUp, true); var start = player.transform.position;
        yield return Wait(.12f); Hold(GameAction.MoveUp, false);
        Check(player.transform.position.y > start.y + .1f, "basic swing leaves strafe movement available");
        yield return Wait(.35f);
        Check(player.MeleeContactsResolved == 1, "released primary does not queue an extra swing");

        Fresh(); yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); Hold(GameAction.Attack, false);
        deadline = Clock + .4f;
        while (Field<float>("attackTimer") > .09f && Clock < deadline) yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); Hold(GameAction.Attack, false);
        yield return Wait(.5f);
        Check(player.MeleeContactsResolved == 2, "a short press in the late buffer links exactly one next cut");

        Fresh(); yield return Frame();
        var mana = player.Mana; var started = Time.time;
        Hold(GameAction.Guard, true); yield return Frame();
        Check(player.IsLongswordCatchActive && player.IsGuarding, "RMB enters a finite catch");
        Check(Mathf.Abs(mana - player.Mana - LongswordActionRules.ManaCost) < .3f, "RMB pays ten mana once on entry");
        yield return Wait(1.15f);
        Check(player.MeleeContactsResolved == 1 && !player.IsGuarding && !player.IsLongswordCatchActive, "held RMB produces one cut and cannot sustain guard or repeat");
        var spent = mana + (Time.time - started) * player.Stats.ManaRegeneration - player.Mana;
        Check(Mathf.Abs(spent - LongswordActionRules.ManaCost) < .35f, "holding RMB does not pay a second cost");
        Hold(GameAction.Guard, false);

        Fresh(); yield return Frame();
        Hold(GameAction.Guard, true); yield return Frame(); Hold(GameAction.Guard, false);
        var health = player.Health; hp = target.Health;
        player.TakeDamage(10, Vector2.left);
        Check(player.Health == health && player.LongswordCatchSucceeded, "frontal catch prevents the caught hit");
        Check(target.Health == hp && player.MeleeContactsResolved == 0, "catch success does not deal invisible instant damage");
        deadline = Clock + .4f;
        while (!player.IsMeleeSwingActive && Clock < deadline) yield return Frame();
        Check(player.IsMeleeSwingActive && player.ActiveMeleeMotion == MeleeAttackMotion.Sweep && player.MotionState == PlayerMotionState.Attack,
            "strong counter retains the native regular sweep wrist arc");
        yield return Wait(.6f);
        Check(player.MeleeContactsResolved == 1 && target.Health < hp, "caught attack releases one actual counter sweep");
        Check(finisherImpacts == 1, "landed counter emits one bounded finisher impact callback");

        Fresh(); yield return Frame();
        UnityEngine.Object.Destroy(target.gameObject); yield return Frame();
        Hold(GameAction.Guard, true); yield return Frame(); Hold(GameAction.Guard, false);
        yield return Wait(.65f);
        Check(player.MeleeContactsResolved == 1 && finisherImpacts == 0, "a whiffed special plays its cut without hit-stop feedback");

        Fresh(); yield return Frame();
        Hold(GameAction.Guard, true); yield return Frame(); Hold(GameAction.Guard, false);
        health = player.Health; player.TakeDamage(10, Vector2.right);
        Check(player.Health < health && !player.LongswordCatchSucceeded, "catch does not protect the player's back");

        Fresh(); yield return Frame();
        mana = player.Mana;
        Hold(GameAction.Guard, true); yield return Frame(); Hold(GameAction.Guard, false);
        var paidMana = player.Mana; var paidAt = Time.time;
        trackRegeneration = true; regenerationDelta = 0; regenerationFrame = Time.frameCount;
        checks.Add("DATA dash-cancel entry mana=" + mana.ToString("F6") + " paidMana=" + paidMana.ToString("F6") +
            " cost=" + (mana - paidMana).ToString("F6") + " regenPerSecond=" + player.Stats.ManaRegeneration.ToString("F6"));
        Check(Mathf.Abs(mana - paidMana - LongswordActionRules.ManaCost) < .01f, "dash-cancel action paid exactly ten mana before cancellation");
        Hold(GameAction.Dash, true); yield return Frame(); Hold(GameAction.Dash, false);
        checks.Add("DATA dash-cancel clocks actionCooldown=" + Field<float>("weaponActionCooldown").ToString("F6") +
            " attackRecovery=" + Field<float>("attackTimer").ToString("F6") + " contacts=" + player.MeleeContactsResolved);
        Check(Field<float>("weaponActionCooldown") > .5f && Field<float>("attackTimer") > .2f, "dash cancellation keeps paid recovery and skill cooldown");
        yield return Wait(.65f);
        var regenElapsed = Time.time - paidAt;
        trackRegeneration = false;
        var expectedMana = Mathf.Min(player.MaxMana, paidMana + regenerationDelta * player.Stats.ManaRegeneration);
        checks.Add("DATA dash-cancel completed contacts=" + player.MeleeContactsResolved + " elapsed=" + regenElapsed.ToString("F6") +
            " observedUpdateDeltaSum=" + regenerationDelta.ToString("F6") +
            " expectedMana=" + expectedMana.ToString("F6") + " actualMana=" + player.Mana.ToString("F6") +
            " unexplainedRefund=" + (player.Mana - expectedMana).ToString("F6"));
        Check(player.MeleeContactsResolved == 0, "dash removes every pending counter contact without delayed damage");
        Check(Mathf.Abs(player.Mana - expectedMana) < .01f, "dash refunds no mana; post-cancel resource changes equal normal regeneration exactly");

        Fresh(); yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); Hold(GameAction.Attack, false);
        Hold(GameAction.Dash, true); yield return Frame(); Hold(GameAction.Dash, false);
        Hold(GameAction.Guard, true); yield return Frame(); Hold(GameAction.Guard, false);
        yield return Wait(.6f);
        Check(player.MeleeContactsResolved == 0, "dash removes pre-contact basic damage and cannot queue RMB through the dash");

        Fresh(); yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); Hold(GameAction.Attack, false);
        var remaining = Field<float>("attackTimer");
        player.EquipWeapon(WeaponLootTable.Find("knight_sword"));
        Check(Field<float>("attackTimer") >= remaining - .001f, "equip preserves the canceled attack's recovery");
        yield return Wait(.6f);
        Check(player.MeleeContactsResolved == 0, "equip removes pending old-weapon damage");

        Fresh(); yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); paused = true;
        yield return Wait(.08f); paused = false; yield return Wait(.6f);
        Check(player.MeleeContactsResolved == 0, "pause cancels the attack and held input cannot resume it");
        Hold(GameAction.Attack, false); yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); Hold(GameAction.Attack, false);
        yield return Wait(.4f);
        Check(player.MeleeContactsResolved == 1, "fresh input after release resumes combat normally");

        Fresh(); yield return Frame();
        Hold(GameAction.Guard, true); yield return Frame(); Hold(GameAction.Guard, false);
        player.SetStageClearSafety(true); yield return Wait(.6f);
        Check(player.MeleeContactsResolved == 0, "stage transition safety cancels pending counter");

        Fresh(); yield return Frame();
        Hold(GameAction.Attack, true); yield return Frame(); Hold(GameAction.Attack, false);
        player.TakeDamage(100000, Vector2.left); yield return Wait(.5f);
        Check(player.IsDead && player.MeleeContactsResolved == 0, "death cancels pending contact");

        Fresh(); yield return Frame();
        player.Stats.TrySpendMana(player.Mana);
        Hold(GameAction.Guard, true); yield return Frame(); Hold(GameAction.Guard, false);
        yield return Wait(.55f);
        Check(!player.IsLongswordCatchActive && player.MeleeContactsResolved == 0, "insufficient mana cannot start a free action");
    }
}
