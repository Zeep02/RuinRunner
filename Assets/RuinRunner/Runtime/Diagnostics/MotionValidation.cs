using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Isolated input/physics regression scene, NOT a normal-run completion test.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed partial class MotionValidation : MonoBehaviour
    {
        private PlayerController player;
        private IEnumerator sequence;
        private bool paused;
        private int checks;
        private string captureDirectory;
        private MethodInfo screenshot;
        private bool recording;
        private int recordedFrames;
        private int renderFrames;
        private bool fourWeapons;
        private CameraFollow follow;

        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Time.captureFramerate = 60;
            var settings = new GameSettingsService();
            var inventory = new InventorySystem();
            inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
            follow = CameraFollow.Create();
            player = PlayerController.Create(Vector2.zero, () => paused, () => true, () => { }, follow, inventory, settings);
            player.EquipWeapon(inventory.EquippedWeapon);
            var args = Environment.GetCommandLineArgs();
            fourWeapons = Array.IndexOf(args, "--four-weapon-validation") >= 0;
            var captureIndex = Array.IndexOf(args, "--validation-capture-dir");
            if (captureIndex >= 0 && captureIndex + 1 < args.Length)
            {
                captureDirectory = args[captureIndex + 1];
                Directory.CreateDirectory(captureDirectory);
                if (File.Exists(Path.Combine(captureDirectory, "motion.csv"))) throw new InvalidOperationException("Capture directory must be new; preserve earlier evidence.");
                screenshot = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?
                    .GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            }
            if (fourWeapons) StartCoroutine(RecordWorldFrames());
            sequence = Validate();
        }

        private void Update()
        {
            if (sequence == null) return;
            StageValidationInput.BeginFrame();
            try
            {
                if (sequence.MoveNext()) return;
                Debug.Log("[Motion Validation] PASS " + checks + " assertions; isolated scene, not a full-stage run.");
                sequence = null;
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Motion Validation] FAIL " + exception);
                sequence = null;
                Application.Quit(3);
            }
        }

        private IEnumerator Validate()
        {
            yield return null;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--thrust-options-validation") >= 0)
            {
                var update = ValidateThrustOptions();
                while (update.MoveNext()) yield return null;
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--demo-polish-validation") >= 0)
            {
                var polish = ValidateDemoPolish();
                while (polish.MoveNext()) yield return null;
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--combo-probe") >= 0)
            {
                var probe = ProbeComboInputs();
                while (probe.MoveNext()) yield return null;
                yield break;
            }
            var enemy = EnemyController.Create(EnemyType.Boss, new Vector2(1.1f, .15f), player, () => true, _ => { }, null);
            enemy.enabled = false;
            var health = enemy.Health;
            StageValidationInput.SetAim(Vector2.right);
            Tap(GameAction.Attack);
            yield return null;
            Require(enemy.Health == health, "no damage on attack input / anticipation");
            for (var i = 0; i < 12; i++) yield return null;
            Require(enemy.Health < health && player.MeleeContactsResolved == 1, "one damage contact at strike phase");
            health = enemy.Health;
            for (var i = 0; i < 20; i++) yield return null;
            Require(enemy.Health == health && player.MeleeContactsResolved == 1, "no duplicate hit in recovery");

            Tap(GameAction.Attack);
            yield return null;
            Tap(GameAction.Dash);
            for (var i = 0; i < 24; i++) yield return null;
            Require(player.MeleeContactsResolved == 1, "dash cancels pending contact");
            player.Teleport(Vector2.zero);
            Tap(GameAction.Attack);
            yield return null;
            player.EquipWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
            Tap(GameAction.Attack);
            yield return null;
            Require(!player.IsMeleeSwingActive, "weapon switch does not reset attack cooldown");
            for (var i = 0; i < 30; i++) yield return null;
            Require(player.MeleeContactsResolved == 1, "weapon switch cancels pending contact and stale buffer");

            Tap(GameAction.Attack);
            yield return null;
            paused = true;
            var progress = player.AttackAnimationProgress;
            for (var i = 0; i < 20; i++) yield return null;
            Require(player.MeleeContactsResolved == 1 && player.AttackAnimationProgress == progress, "pause freezes contact timeline");
            paused = false;
            for (var i = 0; i < 27; i++) yield return null;
            Require(player.MeleeContactsResolved == 2, "resume resolves contact exactly once");

            var attackSequence = player.AttackAnimationSequence;
            Tap(GameAction.Attack);
            for (var i = 0; i < 17; i++) yield return null;
            Tap(GameAction.Attack);
            for (var i = 0; i < 30; i++) yield return null;
            Require(player.AttackAnimationSequence == attackSequence + 2, "early combo input is buffered once");
            Destroy(enemy.gameObject);
            player.Teleport(Vector2.zero);
            StageValidationInput.SetHeld(GameAction.Guard, true);
            yield return null;
            Require(player.IsGuarding, "held right-click enters guard");
            health = player.Health;
            player.TakeDamage(4f, Vector2.left);
            var frontLoss = health - player.Health;
            for (var i = 0; i < 70; i++) yield return null;
            health = player.Health;
            player.TakeDamage(4f, Vector2.right);
            Require(health - player.Health > frontLoss, "guard reduces only frontal damage");
            StageValidationInput.SetHeld(GameAction.Guard, false);
            yield return null;
            Require(!player.IsGuarding, "right-click release clears guard");
            for (var i = 0; i < 70; i++) yield return null;

            StageValidationInput.SetHeld(GameAction.MoveRight, true);
            for (var i = 0; i < 20; i++) yield return null;
            var body = player.GetComponent<Rigidbody2D>();
            var cardinalSpeed = body.linearVelocity.magnitude;
            StageValidationInput.SetHeld(GameAction.MoveUp, true);
            for (var i = 0; i < 20; i++) yield return null;
            Require(Mathf.Abs(cardinalSpeed - body.linearVelocity.magnitude) < .05f, "diagonal movement speed is normalized");
            StageValidationInput.ReleaseAll();
            player.Teleport(Vector2.zero);
            for (var i = 0; i < 10; i++) yield return null;
            var wall = new GameObject("ValidationWall");
            wall.layer = GameLayers.Ground;
            wall.transform.position = new Vector3(1f, 0f, 0f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 4f);
            Tap(GameAction.Dash);
            for (var i = 0; i < 20; i++) yield return null;
            Require(player.transform.position.x < .6f, "dash stops before wall footprint");
            Destroy(wall);

            StageValidationInput.ReleaseAll();
            yield return null;
            var corner = new GameObject("RoundedFootCornerRegression");
            corner.layer = GameLayers.Ground;
            corner.transform.position = new Vector3(1f, 1f, 0f);
            var cornerCollider = corner.AddComponent<BoxCollider2D>();
            cornerCollider.size = Vector2.one;
            player.Teleport(new Vector2(.24f, .58f));
            for (var i = 0; i < 3; i++) yield return null;
            var foot = player.GetComponent<Collider2D>();
            Require(!foot.Distance(cornerCollider).isOverlapped && foot.bounds.Intersects(cornerCollider.bounds),
                "rounded foot can legally occupy a corner intersecting its bounding box");
            while (player.DashCharges == 0) yield return null;
            StageValidationInput.SetAim(Vector2.left);
            Tap(GameAction.Dash);
            for (var i = 0; i < 20; i++) yield return null;
            Require(player.transform.position.x < -1.5f, "dash can leave a legal rounded corner without false box overlap");
            Destroy(corner);
            StageValidationInput.SetAim(Vector2.right);
            yield return null;
            var charge = typeof(EnemyController).GetMethod("ChargeAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            var createLanding = typeof(EnemyController).GetMethod("CreateLeapShadow", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var facing in new[] { 1f, -1f })
            {
                player.Teleport(new Vector2(-8f * facing, 0f));
                var charger = EnemyController.Create(EnemyType.Charger, Vector2.zero, player, () => false, _ => { }, null);
                charger.StartCoroutine((IEnumerator)charge.Invoke(charger, new object[] { new Vector2(facing, 0f), false }));
                var consistent = true;
                for (var i = 0; i < 45; i++)
                {
                    yield return null;
                    consistent &= charger.FacingDirection == facing;
                    consistent &= charger.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().flipX == (facing < 0);
                }
                Require(consistent, "committed charge keeps facing " + facing + " while player is behind");
                Destroy(charger.gameObject);
                yield return null;
            }
            var leaper = EnemyController.Create(EnemyType.Leaper, Vector2.zero, player, () => true, _ => { }, null);
            foreach (var bossArea in new[] { false, true })
            {
                var landing = (GameObject)createLanding.Invoke(leaper, new object[] { Vector2.zero, bossArea });
                var area = landing.GetComponent<SpriteRenderer>().bounds.size;
                Require(Mathf.Abs(area.x - EnemyController.SlamRadius(bossArea) * 2f) < .001f && Mathf.Abs(area.y - area.x) < .001f,
                    "landing indicator diameter matches XY damage radius (boss=" + bossArea + ")");
            }
            Destroy(leaper.gameObject);
            yield return null;
            yield return null;
            var leftover = false;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>())
                leftover |= renderer.name == "LeapLandingTelegraph";
            Require(!leftover, "destroyed enemy leaves no landing telegraph");

            if (fourWeapons)
            {
                var four = ValidateFourWeapons();
                while (four.MoveNext()) yield return null;
                yield break;
            }

            // Continuous rendered sequence: the same runtime body/weapon components
            // and GameAction input, in an isolated room with no progression writes.
            recording = true;
            for (var directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                StageValidationInput.ReleaseAll();
                var angle = directionIndex * Mathf.PI / 4f;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                player.Teleport(-direction * 2.4f);
                StageValidationInput.SetAim(direction);
                SetMovement(direction);
                var shownFrames = new System.Collections.Generic.HashSet<int>();
                var animation = player.GetComponent<PlayerAnimationController>();
                for (var i = 0; i < 36; i++)
                {
                    yield return null;
                    if (player.MotionState == PlayerMotionState.Run) shownFrames.Add(animation.FrameIndex);
                }
                Require(animation.FacingDirection == directionIndex && shownFrames.Count == 4,
                    "8-direction live walking direction=" + directionIndex + " animated frames=" + shownFrames.Count);
                Tap(GameAction.Attack);
                for (var i = 0; i < 26; i++) yield return null;
                Tap(GameAction.Attack);
                for (var i = 0; i < 26; i++) yield return null;
                SetMovement(Vector2.zero);
                for (var i = 0; i < 12; i++) yield return null;
                StageValidationInput.SetHeld(GameAction.Guard, true);
                for (var i = 0; i < 14; i++) yield return null;
                StageValidationInput.SetHeld(GameAction.Guard, false);
                Tap(GameAction.Dash);
                for (var i = 0; i < 22; i++) yield return null;
            }
            recording = false;
            yield return null;
        }

        private void LateUpdate()
        {
            if (fourWeapons) return;
            if (!recording || screenshot == null || string.IsNullOrEmpty(captureDirectory)) return;
            if ((renderFrames++ & 1) != 0) return;
            var frame = recordedFrames++;
            screenshot.Invoke(null, new object[] { Path.Combine(captureDirectory, "motion-" + frame.ToString("D4") + ".png"), 1 });
            var visual = player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            var line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7},{8},{9}\n", frame, player.MotionState,
                player.transform.position.x, player.transform.position.y, player.AimDirection.x, player.AimDirection.y,
                player.AttackAnimationProgress, player.MeleeContactsResolved, visual.sprite.name, visual.flipX);
            File.AppendAllText(Path.Combine(captureDirectory, "motion.csv"), line);
        }

        private IEnumerator ValidateFourWeapons()
        {
            var room = ScriptableObject.CreateInstance<RoomTemplate>();
            room.roomSize = new Vector2(38f, 26f);
            room.decorations = Array.Empty<RoomDecorationData>();
            var root = new GameObject("FourWeaponMotionRoom");
            ArenaBuilder.BuildRoom(root.transform, room, 0, Vector2.zero, null);
            follow.Target = player.transform;
            follow.FocusRoom(Vector2.zero, room.Size, true);
            var families = new[] { WeaponFamily.Longsword, WeaponFamily.Katana, WeaponFamily.Dagger, WeaponFamily.Greatsword };
            foreach (var family in families)
            for (var directionIndex = 0; directionIndex < 8; directionIndex++)
            {
                recording = false;
                StageValidationInput.ReleaseAll();
                player.EquipWeapon(WeaponLootTable.FindFirst(family));
                var angle = directionIndex * Mathf.PI / 4f;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                player.Teleport(-direction * 2.4f); // Between clips only; no teleport within an action chain.
                StageValidationInput.SetAim(direction);
                for (var i = 0; i < 80; i++) yield return null; // Fresh cooldowns for the next independent direction fixture.
                follow.FocusRoom(Vector2.zero, room.Size, true);
                Debug.Log("[Four Weapons] BEGIN " + family + " direction=" + directionIndex + " frame=" + recordedFrames);
                recording = true;
                SetMovement(direction);
                for (var i = 0; i < 24; i++) yield return null;
                var before = player.MeleeContactsResolved;
                Tap(GameAction.Attack);
                var wait = family == WeaponFamily.Greatsword
                    ? Mathf.CeilToInt(player.AttackDelay * MeleeComboCatalog.Get(player.EquippedWeapon).LightSteps[0].DelayMultiplier * 60f) + 2 : 22;
                for (var i = 0; i < wait; i++) yield return null;
                Require(player.MeleeContactsResolved > before, family + " basic strike resolves while moving direction=" + directionIndex);
                SetMovement(Vector2.zero);
                StageValidationInput.SetHeld(GameAction.Guard, true);
                for (var i = 0; i < 3; i++) yield return null;
                Require(player.IsWeaponActionActive, family + " right-click action starts direction=" + directionIndex);
                for (var i = 0; i < (family == WeaponFamily.Greatsword ? 42 : 13); i++) yield return null;
                StageValidationInput.SetHeld(GameAction.Guard, false);
                for (var i = 0; i < (family == WeaponFamily.Greatsword ? 38 : 20); i++) yield return null;
                var charges = player.DashCharges;
                Tap(GameAction.Dash);
                for (var i = 0; i < 4; i++) yield return null;
                Require(player.DashCharges < charges, family + " dash follows weapon action direction=" + directionIndex);
                for (var i = 0; i < 14; i++) yield return null;
                before = player.MeleeContactsResolved;
                Tap(GameAction.Attack);
                for (var i = 0; i < 45; i++) yield return null;
                Require(player.MeleeContactsResolved > before, family + " attack resumes after dash direction=" + directionIndex);
                var switched = WeaponLootTable.FindFirst(family == WeaponFamily.Longsword ? WeaponFamily.Dagger : WeaponFamily.Longsword);
                player.EquipWeapon(switched); // The same event used by the game's equipment handler; not a UI drag test.
                for (var i = 0; i < 20; i++) yield return null;
                Require(player.GetComponent<HeldWeaponView>().CurrentWeaponId == switched.Id, family + " held art follows equipment change direction=" + directionIndex);
                Debug.Log("[Four Weapons] END " + family + " direction=" + directionIndex + " frame=" + recordedFrames);
            }
            recording = false;
            StageValidationInput.ReleaseAll();
            yield return null;
        }

        private IEnumerator RecordWorldFrames()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (!recording || string.IsNullOrEmpty(captureDirectory) || (renderFrames++ & 1) != 0) continue;
                // Capture the actually presented PixelPerfect frame. A second
                // Camera.Render into 1280x720 retained a 640x360 internal viewport.
                if(screenshot==null)screenshot=Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?
                    .GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)});
                if(screenshot==null)throw new InvalidOperationException("ScreenCapture module unavailable");
                var camera = follow.GetComponent<Camera>();
                var frame = recordedFrames++;
                screenshot.Invoke(null,new object[]{Path.Combine(captureDirectory,"motion-"+frame.ToString("D4")+".png"),1});
                var visual = player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                var line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7},{8},{9},{10},{11:F3},{12:F3}\n", frame,
                    player.MotionState, player.transform.position.x, player.transform.position.y, player.AimDirection.x, player.AimDirection.y,
                    player.AttackAnimationProgress, player.MeleeContactsResolved, visual.sprite.name, visual.flipX,
                    player.EquippedWeapon.Family, camera.transform.position.x, camera.transform.position.y);
                File.AppendAllText(Path.Combine(captureDirectory, "motion.csv"), line);
            }
        }

        private IEnumerator ProbeComboInputs()
        {
            StageValidationInput.SetAim(Vector2.right);
            var schedules = new[] { new[] { 0, 6 }, new[] { 0, 16, 36 }, new[] { 0 } };
            var names = new[] { "rapid-double-click-100ms", "three-clicks-in-buffer-window", "hold-attack-2200ms" };
            for (var test = 0; test < schedules.Length; test++)
            {
                StageValidationInput.ReleaseAll();
                player.EquipWeapon(DemoContent.Weapon("rust_sword"));
                for (var wait = 0; wait < 70; wait++) yield return null;
                var before = player.AttackAnimationSequence; var previous = before;
                var steps = new System.Collections.Generic.List<int>();
                var attacksAtRelease = -1;
                for (var tick = 0; tick < (test == 2 ? 195 : 100); tick++)
                {
                    if (Array.IndexOf(schedules[test], tick) >= 0)
                    {
                        if (test == 2) StageValidationInput.SetHeld(GameAction.Attack, true);
                        else Tap(GameAction.Attack);
                    }
                    if (test == 2 && tick == 132)
                    {
                        StageValidationInput.SetHeld(GameAction.Attack, false);
                        attacksAtRelease = player.AttackAnimationSequence;
                    }
                    yield return null;
                    if (player.AttackAnimationSequence != previous)
                    {
                        previous = player.AttackAnimationSequence;
                        steps.Add(player.ActiveComboAnimationStep + 1);
                    }
                }
                Debug.Log("[Combo Probe] " + names[test] + " attacks=" + (player.AttackAnimationSequence - before) + " steps=" + string.Join(",", steps));
                if (test == 1) Require(steps.Count == 3 && steps[0] == 1 && steps[1] == 2 && steps[2] == 3, "three combo steps execute with timed clicks");
                if (test == 2)
                {
                    Require(steps.Count >= 4, "holding primary attack repeats beyond one combo");
                    for (var i = 0; i < steps.Count; i++) Require(steps[i] == i % 3 + 1, "held combo repeats 1-2-3, swing=" + i);
                    Require(player.AttackAnimationSequence == attacksAtRelease, "release prevents any new automatic swing");
                }
            }
        }

        private static void Tap(GameAction action)
        {
            StageValidationInput.SetHeld(action, true);
            StageValidationInput.SetHeld(action, false);
        }

        private static void SetMovement(Vector2 direction)
        {
            StageValidationInput.SetHeld(GameAction.MoveLeft, direction.x < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveRight, direction.x > .1f);
            StageValidationInput.SetHeld(GameAction.MoveUp, direction.y > .1f);
            StageValidationInput.SetHeld(GameAction.MoveDown, direction.y < -.1f);
        }

        private void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks++;
            Debug.Log("[Motion Validation] OK " + description);
        }
    }
}
