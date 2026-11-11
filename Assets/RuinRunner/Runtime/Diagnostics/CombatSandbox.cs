using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>A throwaway run for tuning combat: spawn anything, swap weapons, slow time. Never touches a save slot.</summary>
    [DefaultExecutionOrder(500)] // After the player has read this frame's scripted presses.
    public sealed class CombatSandbox : MonoBehaviour
    {
        public const string CommandLineArgument = DiagnosticsHooks.CombatSandboxArgument;
        // Plays every signature weapon through the real input path and writes frames for review: --sandbox-tour <folder> [first] [count]
        public const string TourArgument = "--sandbox-tour";
        // Sets off every passive accessory and writes frames for review: --sandbox-passives <folder>
        public const string PassiveTourArgument = "--sandbox-passives";
        // Uses the skills of the named weapons the way a player would, moving and turning the aim where the skill depends on it: --sandbox-skills <folder> id,id,...
        public const string SkillTourArgument = "--sandbox-skills";
        private static readonly EnemyType[] Types = (EnemyType[])System.Enum.GetValues(typeof(EnemyType));
        private static readonly float[] Speeds = { 1f, .5f, .25f };
        private readonly List<EnemyController> spawned = new List<EnemyController>();
        private GameController game;
        private int typeIndex, weaponIndex, speedIndex;
        private bool god = true, ready, touring;
        private GUIStyle style;

        public void Initialize(GameController controller) => game = controller;

        private IEnumerator Start()
        {
            while (game == null || game.Player == null || !game.IsRunActive)
                yield return null;
            ready = true;
            game.Player.SandboxInvulnerable = god;
            var args = System.Environment.GetCommandLineArgs();
            var tour = System.Array.IndexOf(args, TourArgument);
            var passives = System.Array.IndexOf(args, PassiveTourArgument);
            var skills = System.Array.IndexOf(args, SkillTourArgument);
            if (skills >= 0 && skills + 2 < args.Length)
                StartCoroutine(SkillTour(args[skills + 1], args[skills + 2].Split(',')));
            else if (passives >= 0 && passives + 1 < args.Length)
                StartCoroutine(PassiveTour(args[passives + 1]));
            else if (tour >= 0 && tour + 1 < args.Length)
            {
                var first = tour + 2 < args.Length && int.TryParse(args[tour + 2], out var a) ? a : 0;
                var count = tour + 3 < args.Length && int.TryParse(args[tour + 3], out var b) ? b : SignatureWeaponCatalog.All.Length;
                StartCoroutine(Tour(args[tour + 1], first, count));
            }
            else
                Equip(SignatureWeaponCatalog.All[weaponIndex]);
        }

        private void Equip(WeaponDefinition weapon)
        {
            // Replaces whatever the active slot holds: the bag would fill up after twenty swaps.
            var inventory = game.Inventory;
            inventory.ActiveSlot.Assign(weapon);
            game.Player.EquipWeapon(inventory.EquippedWeapon);
        }

        // Left click and right click of each weapon, in separate folders, for the review gallery.
        // The targets stand still, so nothing but the weapon's own effects is on screen.
        private IEnumerator Tour(string folder, int first, int count)
        {
            touring = true;
            Application.runInBackground = true;
            Time.captureFramerate = 24;
            var home = (Vector2)game.Player.transform.position;
            var stations = new Dictionary<EnemyController, Vector2>();
            void Foe(float x, float y)
            {
                EnemyController foe = null;
                foe = EnemyController.Create(EnemyType.Grunt, (Vector2)game.Player.transform.position + new Vector2(x, y), game.Player, () => foe == null || !foe.IsDead, _ => { }, game.CameraFollow);
                foe.MakeTrainingDummy();
                spawned.Add(foe);
                stations[foe] = new Vector2(x, y);
            }
            // Knockback from the basic attacks carries the dummies out of most skills' reach; put the same dummies back (marks stay on them).
            void Restation()
            {
                foreach (var pair in stations)
                    if (pair.Key != null && !pair.Key.IsDead)
                    {
                        var body = pair.Key.GetComponent<Rigidbody2D>();
                        var at = home + pair.Value;
                        if (body != null)
                        {
                            body.position = at;
                            body.linearVelocity = Vector2.zero;
                        }
                        pair.Key.transform.position = at;
                    }
            }
            void Foes()
            {
                if (spawned.Count >= 3)
                    return;
                Foe(2.2f, .5f);
                Foe(2.6f, -.6f);
                Foe(3.6f, 0f);
            }
            IEnumerator Tap()
            {
                StageValidationInput.SetHeld(GameAction.Guard, true);
                yield return new WaitForSeconds(.08f);
                StageValidationInput.SetHeld(GameAction.Guard, false);
            }
            // Each effect's distance from the hero when it appears, to catch anything still starting at the body.
            SigFx.Created = (clip, point, ground) =>
            {
                if (game.Player != null)
                    Debug.Log("[Sandbox Fx] " + clip + " " + (ground ? "floor " : "air ") + Vector2.Distance(point, game.Player.transform.position).ToString("0.00"));
            };
            // Hits on the dummies per phase: a weapon whose attack or skill lands nothing in front of it has a hitbox problem.
            int hits = 0;
            float dealt = 0;
            var previousHook = DiagnosticsHooks.EnemyDamaged;
            DiagnosticsHooks.EnemyDamaged = (enemy, amount, original, critical) =>
            {
                hits++;
                dealt += amount;
                previousHook?.Invoke(enemy, amount, original, critical);
            };
            // The run opens with the hero rising out of ink; the first weapon waits for that to finish.
            yield return new WaitForSeconds(2f);
            var end = Mathf.Min(SignatureWeaponCatalog.All.Length, first + count);
            for (var w = first; w < end; w++)
            {
                var weapon = SignatureWeaponCatalog.All[w];
                ClearAll();
                game.Player.Teleport(home);
                Equip(weapon);
                game.Player.SandboxRefresh();
                StageValidationInput.SetAim(Vector2.right);
                Foes();
                Debug.Log("[Sandbox Tour] " + w + " " + weapon.Id + " equipped=" + game.Player.EquippedWeapon.Id);
                var name = w.ToString("D2") + "_" + weapon.Id;
                yield return new WaitForSeconds(.15f);
                hits = 0;
                dealt = 0;
                var capture = StartCoroutine(CaptureFrames(System.IO.Path.Combine(folder, name, "L")));
                StageValidationInput.SetHeld(GameAction.Attack, true);
                yield return new WaitForSeconds(1.2f);
                StageValidationInput.SetHeld(GameAction.Attack, false);
                yield return new WaitForSeconds(.2f);
                StopCoroutine(capture);
                int leftHits = hits;
                float leftDealt = dealt;
                hits = 0;
                dealt = 0;
                // Some skills spend what the attacks above built up, so the same targets stay if they survived.
                Foes();
                game.Player.Teleport(home);
                Restation();
                game.Player.SandboxRefresh();
                yield return new WaitForSeconds(.1f);
                capture = StartCoroutine(CaptureFrames(System.IO.Path.Combine(folder, name, "R")));
                yield return Tap();
                yield return new WaitForSeconds(1.1f);
                game.Player.SandboxRefresh();
                yield return Tap();
                yield return new WaitForSeconds(1.1f);
                StopCoroutine(capture);
                Debug.Log("[Sandbox Hits] " + weapon.Id + " L=" + leftHits + " (" + leftDealt.ToString("0") + ") R=" + hits + " (" + dealt.ToString("0") + ")");
            }
            DiagnosticsHooks.EnemyDamaged = previousHook;
            Debug.Log("[Sandbox Tour] Finished");
            Application.Quit(0);
        }

        private IEnumerator SkillTour(string folder, string[] ids)
        {
            touring = true;
            Application.runInBackground = true;
            Time.captureFramerate = 24;
            var player = game.Player;
            var home = (Vector2)player.transform.position;
            void Foe(float x, float y)
            {
                EnemyController foe = null;
                foe = EnemyController.Create(EnemyType.Grunt, home + new Vector2(x, y), player, () => foe == null || !foe.IsDead, _ => { }, game.CameraFollow);
                foe.MakeTrainingDummy();
                spawned.Add(foe);
            }
            IEnumerator Tap()
            {
                StageValidationInput.SetHeld(GameAction.Guard, true);
                yield return new WaitForSeconds(.08f);
                StageValidationInput.SetHeld(GameAction.Guard, false);
            }
            IEnumerator Attack(float seconds)
            {
                StageValidationInput.SetHeld(GameAction.Attack, true);
                yield return new WaitForSeconds(seconds);
                StageValidationInput.SetHeld(GameAction.Attack, false);
            }
            IEnumerator Walk(GameAction way, float seconds)
            {
                StageValidationInput.SetHeld(way, true);
                yield return new WaitForSeconds(seconds);
                StageValidationInput.SetHeld(way, false);
            }
            void Aim(float degrees) => StageValidationInput.SetAim(new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)));
            IEnumerator Sweep(float from, float to, float seconds)
            {
                for (var t = 0f; t < seconds; t += Time.deltaTime)
                {
                    Aim(Mathf.Lerp(from, to, t / seconds));
                    yield return null;
                }
            }
            SigFx.Created = (clip, point, ground) =>
            {
                if (game.Player != null)
                    Debug.Log("[Sandbox Fx] " + clip + " " + (ground ? "floor " : "air ") + Vector2.Distance(point, game.Player.transform.position).ToString("0.00") + " frame " + capturedFrame);
            };
            // The ink entrance holds the hero still at first, on its own clock.
            for (var t = 0f; t < 12f; t += Time.unscaledDeltaTime)
                yield return null;
            foreach (var id in ids)
            {
                var weapon = SignatureWeaponCatalog.Find(id);
                ClearAll();
                player.Teleport(home);
                Equip(weapon);
                player.SandboxRefresh();
                Aim(0);
                Foe(2.6f, .7f);
                Foe(3.4f, -.8f);
                Foe(4.8f, .1f);
                yield return new WaitForSeconds(.25f);
                var capture = StartCoroutine(CaptureFrames(System.IO.Path.Combine(folder, id)));
                var before = player.Mana;
                switch (id)
                {
                    case "hour_minute":
                        yield return Tap();
                        yield return Walk(GameAction.MoveUp, .45f);
                        yield return Walk(GameAction.MoveRight, .6f);
                        yield return Attack(.3f);
                        yield return Tap();
                        yield return new WaitForSeconds(1.2f);
                        break;
                    case "falling_blossom":
                        yield return Tap();
                        yield return new WaitForSeconds(.3f);
                        yield return Tap();
                        yield return new WaitForSeconds(.3f);
                        yield return Tap();
                        yield return new WaitForSeconds(1.4f);
                        break;
                    case "instant_king":
                        yield return Tap();
                        yield return new WaitForSeconds(.22f);
                        Aim(50);
                        yield return Tap();
                        yield return new WaitForSeconds(.22f);
                        Aim(-40);
                        yield return Tap();
                        yield return new WaitForSeconds(1.2f);
                        break;
                    case "thousand_returns":
                        yield return Attack(1.3f);
                        player.SandboxRefresh();
                        yield return Tap();
                        yield return new WaitForSeconds(1.7f);
                        break;
                    case "novice_branch":
                        StageValidationInput.SetHeld(GameAction.Guard, true);
                        StageValidationInput.SetHeld(GameAction.MoveDown, true);
                        yield return Sweep(0, -110, 1f);
                        StageValidationInput.SetHeld(GameAction.MoveDown, false);
                        Aim(-20);
                        StageValidationInput.SetHeld(GameAction.Guard, false);
                        yield return new WaitForSeconds(.7f);
                        break;
                    case "scout_carbine":
                        StageValidationInput.SetHeld(GameAction.Guard, true);
                        yield return Sweep(-60, 20, 1f);
                        StageValidationInput.SetHeld(GameAction.Guard, false);
                        yield return new WaitForSeconds(.7f);
                        break;
                    case "lantern_orb":
                        yield return Tap();
                        yield return new WaitForSeconds(.4f);
                        yield return Attack(1.3f);
                        yield return new WaitForSeconds(.5f);
                        break;
                    case "season_end":
                        for (var i = 0; i < 4; i++)
                        {
                            player.SandboxRefresh();
                            yield return Tap();
                            yield return new WaitForSeconds(1.3f);
                        }
                        break;
                    case "world_pendulum":
                        {
                            EnemyController shooter = null;
                            shooter = EnemyController.Create(EnemyType.Shooter, home + new Vector2(-3.6f, 1.2f), player, () => false, _ => { }, game.CameraFollow);
                            spawned.Add(shooter);
                            yield return Tap();
                            yield return new WaitForSeconds(3f);
                            Aim(100);
                            yield return Tap();
                            yield return new WaitForSeconds(1.3f);
                            break;
                        }
                    case "small_eclipse":
                        yield return Tap();
                        yield return Walk(GameAction.MoveUp, .5f);
                        yield return Walk(GameAction.MoveLeft, .7f);
                        yield return Walk(GameAction.MoveDown, .7f);
                        yield return Walk(GameAction.MoveRight, .7f);
                        yield return Tap();
                        yield return new WaitForSeconds(1.4f);
                        break;
                    case "siege_engineer":
                        yield return Tap();
                        yield return new WaitForSeconds(.5f);
                        yield return Attack(1.3f);
                        yield return new WaitForSeconds(.2f);
                        yield return Tap();
                        yield return Walk(GameAction.MoveLeft, .4f);
                        yield return new WaitForSeconds(.4f);
                        break;
                    case "mirror_eye":
                        yield return Tap();
                        yield return new WaitForSeconds(.5f);
                        yield return Attack(.9f);
                        yield return new WaitForSeconds(.2f);
                        yield return Tap();
                        yield return new WaitForSeconds(1f);
                        break;
                    case "dragon_spine":
                        yield return Tap();
                        yield return new WaitForSeconds(.7f);
                        Aim(70);
                        yield return new WaitForSeconds(.1f);
                        yield return Tap();
                        yield return new WaitForSeconds(1.1f);
                        break;
                    case "warden_door":
                        {
                            EnemyController shooter = null;
                            shooter = EnemyController.Create(EnemyType.Shooter, home + new Vector2(4.5f, .3f), player, () => false, _ => { }, game.CameraFollow);
                            spawned.Add(shooter);
                            for (var i = 0; i < 4; i++)
                            {
                                player.SandboxRefresh();
                                yield return Tap();
                                yield return new WaitForSeconds(.9f);
                            }
                            break;
                        }
                    case "blood_bank":
                    case "ember_heart":
                    case "night_thread":
                    case "meteor_waybill":
                        yield return Attack(1.2f);
                        player.SandboxRefresh();
                        yield return Tap();
                        yield return new WaitForSeconds(1.8f);
                        break;
                    case "lightning_carbine":
                        yield return Tap();
                        yield return new WaitForSeconds(.7f);
                        yield return Tap();
                        yield return new WaitForSeconds(1.2f);
                        break;
                    case "polaris":
                        yield return Tap();
                        yield return new WaitForSeconds(.8f);
                        yield return Attack(.6f);
                        yield return new WaitForSeconds(.5f);
                        break;
                    default:
                        yield return Tap();
                        yield return new WaitForSeconds(1.3f);
                        player.SandboxRefresh();
                        yield return Tap();
                        yield return new WaitForSeconds(1.3f);
                        break;
                }
                StopCoroutine(capture);
                StageValidationInput.ReleaseAll();
                Debug.Log("[Sandbox Skill] " + id + " frames=" + capturedFrame + " actions=" + player.UniqueActionsStarted + " mana " + before.ToString("0") + "->" + player.Mana.ToString("0"));
                yield return new WaitForSeconds(.2f);
            }
            Debug.Log("[Sandbox Tour] Finished");
            Application.Quit(0);
        }

        // Each accessory alone, on what actually sets it off. Reports its proc count so a passive that never fires shows up in the log.
        private IEnumerator PassiveTour(string folder)
        {
            touring = true;
            Application.runInBackground = true;
            Time.captureFramerate = 24;
            var home = (Vector2)game.Player.transform.position;
            var player = game.Player;
            var starter = SignatureWeaponCatalog.Find("trainee_blade");
            var other = SignatureWeaponCatalog.Find("merc_slab");
            var spots = new Dictionary<EnemyController, Vector2>();
            void Foe(float x, float y, float health)
            {
                EnemyController foe = null;
                foe = EnemyController.Create(EnemyType.Grunt, (Vector2)player.transform.position + new Vector2(x, y), player, () => foe == null || !foe.IsDead, dead => player.OnEnemyDefeated(foe), game.CameraFollow);
                foe.MakeTrainingDummy(health);
                spawned.Add(foe);
                spots[foe] = new Vector2(x, y);
            }
            // Blows knock the targets away and thrusts carry the hero past them; a player would keep up, the tour keeps them in reach.
            IEnumerator Attack(float seconds)
            {
                StageValidationInput.SetHeld(GameAction.Attack, true);
                for (var t = 0f; t < seconds; t += Time.deltaTime)
                {
                    foreach (var spot in spots)
                        if (spot.Key != null && !spot.Key.IsDead)
                            spot.Key.transform.position = (Vector2)player.transform.position + spot.Value;
                    yield return null;
                }
                StageValidationInput.SetHeld(GameAction.Attack, false);
            }
            IEnumerator Tap(GameAction action)
            {
                StageValidationInput.SetHeld(action, true);
                yield return new WaitForSeconds(.08f);
                StageValidationInput.SetHeld(action, false);
            }
            SigFx.Created = (clip, point, ground) =>
            {
                if (game.Player != null)
                    Debug.Log("[Sandbox Fx] " + clip + " " + (ground ? "floor " : "air ") + Vector2.Distance(point, game.Player.transform.position).ToString("0.00") + " frame " + capturedFrame);
            };
            var row = 0;
            foreach (var accessory in AccessoryLootTable.All)
            {
                if ((int)accessory.Type < (int)AccessoryType.RetributionThorn)
                    continue;
                ClearAll();
                player.Teleport(home);
                Equip(starter);
                player.SandboxRefresh();
                player.SandboxInvulnerable = true;
                player.Stats.Heal(player.MaxHealth);
                StageValidationInput.SetAim(Vector2.right);
                spots.Clear();
                Foe(1.1f, .2f, 1000000f);
                Foe(2.3f, -.7f, 1000000f);
                Foe(3f, .6f, 1000000f);
                var before = player.ItemEffects.PassiveProcs;
                yield return new WaitForSeconds(.1f);
                var capture = StartCoroutine(CaptureFrames(System.IO.Path.Combine(folder, (row++).ToString("D2") + "_" + accessory.Id)));
                yield return null;
                game.Inventory.AddAccessory(accessory);
                switch (accessory.Type)
                {
                    case AccessoryType.CinderSeed:
                    case AccessoryType.SoulLantern:
                        Foe(1f, -.3f, 1f);
                        yield return Attack(1.2f);
                        yield return new WaitForSeconds(1.2f);
                        break;
                    case AccessoryType.HolyTithe:
                        for (var i = 0; i < 12 && player.ItemEffects.PassiveProcs == before; i++)
                        {
                            Foe(1f, -.3f, 1f);
                            yield return Attack(.9f);
                        }
                        yield return new WaitForSeconds(1.2f);
                        break;
                    case AccessoryType.FrostWaveNecklace:
                        yield return Tap(GameAction.Dash);
                        yield return new WaitForSeconds(1.6f);
                        break;
                    case AccessoryType.MeteorCompass:
                    case AccessoryType.TimeHourglass:
                        yield return Tap(GameAction.Guard);
                        yield return new WaitForSeconds(2f);
                        break;
                    case AccessoryType.GuardianFeather:
                        player.ItemEffects.OnDefenseSuccess(player.AimDirection);
                        yield return new WaitForSeconds(1.4f);
                        break;
                    case AccessoryType.RetributionThorn:
                        player.SandboxInvulnerable = false;
                        player.TakeDamage(1, Vector2.left);
                        player.SandboxInvulnerable = true;
                        yield return new WaitForSeconds(1.4f);
                        break;
                    case AccessoryType.PhoenixAsh:
                        player.SandboxInvulnerable = false;
                        player.TakeDamage(1000000, Vector2.left);
                        player.SandboxInvulnerable = true;
                        yield return new WaitForSeconds(1.6f);
                        break;
                    case AccessoryType.SwapFlash:
                        yield return new WaitForSeconds(.2f);
                        Equip(other);
                        yield return new WaitForSeconds(1.4f);
                        break;
                    case AccessoryType.StormCore:
                        yield return new WaitForSeconds(1.5f);
                        break;
                    case AccessoryType.BloodPact:
                        player.Stats.ReceiveDamage(player.Health * .7f);
                        yield return Attack(3.3f);
                        yield return new WaitForSeconds(1.2f);
                        break;
                    default:
                        yield return Attack(1.1f * accessory.Tuning.EffectCount("hitCount"));
                        yield return new WaitForSeconds(1.2f);
                        break;
                }
                StopCoroutine(capture);
                Debug.Log("[Sandbox Passive] " + accessory.Id + " procs=" + (player.ItemEffects.PassiveProcs - before) + " hp=" + player.Health.ToString("0") + "/" + player.MaxHealth.ToString("0") + " dead=" + player.IsDead);
                var owned = game.Inventory.OwnedAccessories[0];
                game.Inventory.TryDisassemble(owned.Identity, owned.UpgradeLevel, out _);
            }
            Debug.Log("[Sandbox Tour] Finished");
            Application.Quit(0);
        }

        private static int capturedFrame;
        private static IEnumerator CaptureFrames(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            for (var frame = 0; ; frame++)
            {
                capturedFrame = frame;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, frame.ToString("D4") + ".png"));
            }
        }

        private void Update()
        {
            if (!ready || game.Player == null)
                return;
            if (touring)
            {
                StageValidationInput.BeginFrame();
                spawned.RemoveAll(enemy => enemy == null);
                return;
            }
            if (Input.GetKeyDown(KeyCode.F1))
                typeIndex = (typeIndex + Types.Length - 1) % Types.Length;
            if (Input.GetKeyDown(KeyCode.F2))
                typeIndex = (typeIndex + 1) % Types.Length;
            if (Input.GetKeyDown(KeyCode.F3))
                Spawn();
            if (Input.GetKeyDown(KeyCode.F4))
                ClearAll();
            if (Input.GetKeyDown(KeyCode.F5))
                Step(Input.GetKey(KeyCode.LeftShift) ? -1 : 1);
            if (Input.GetKeyDown(KeyCode.F8))
                StepFamily();
            if (Input.GetKeyDown(KeyCode.F6))
            {
                god = !god;
                game.Player.SandboxInvulnerable = god;
            }
            if (Input.GetKeyDown(KeyCode.F7))
                speedIndex = (speedIndex + 1) % Speeds.Length;
            if (Input.GetKeyDown(KeyCode.F9))
                game.Player.SandboxRefresh();
            // Hit stop and pauses keep their own clock at normal speed.
            if (speedIndex != 0 && !game.IsPaused)
                Time.timeScale = Speeds[speedIndex];
            else if (speedIndex == 0 && Mathf.Approximately(Time.timeScale, .5f) || speedIndex == 0 && Mathf.Approximately(Time.timeScale, .25f))
                Time.timeScale = 1f;
            spawned.RemoveAll(enemy => enemy == null);
        }

        private void Spawn()
        {
            var aim = game.Player.AimDirection.sqrMagnitude > .01f ? game.Player.AimDirection.normalized : Vector2.right;
            var at = (Vector2)game.Player.transform.position + aim * 5f;
            spawned.Add(EnemyController.Create(Types[typeIndex], at, game.Player, () => game.IsPaused, _ => { }, game.CameraFollow));
        }

        private void ClearAll()
        {
            foreach (var enemy in spawned)
                if (enemy != null)
                    Destroy(enemy.gameObject);
            spawned.Clear();
        }

        private void Step(int direction)
        {
            var all = SignatureWeaponCatalog.All;
            weaponIndex = (weaponIndex + direction + all.Length) % all.Length;
            Equip(all[weaponIndex]);
            game.Player.SandboxRefresh();
        }

        private void StepFamily()
        {
            var all = SignatureWeaponCatalog.All;
            var current = all[weaponIndex].Family;
            for (var i = 1; i <= all.Length; i++)
            {
                var next = (weaponIndex + i) % all.Length;
                if (all[next].Family == current)
                    continue;
                weaponIndex = next;
                Equip(all[next]);
                game.Player.SandboxRefresh();
                return;
            }
        }

        private void OnGUI()
        {
            if (!ready || touring)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, normal = { textColor = Color.white } };
            var boss = spawned.Find(enemy => enemy != null && enemy.IsBoss);
            var weapon = game.Player.EquippedWeapon;
            var text = "[전투 샌드박스] 무기 " + (weaponIndex + 1) + "/" + SignatureWeaponCatalog.All.Length +
                "\nF1/F2 적 선택: " + Types[typeIndex] + "\nF3 소환   F4 전부 제거\nF5 다음 무기 (Shift 이전)   F8 다음 무기군\nF9 쿨타임·마나 회복\nF6 무적: " + (god ? "켬" : "끔") + "   F7 속도: x" + Speeds[speedIndex] +
                (weapon == null ? "" : "\n\n" + weapon.Name + " [" + WeaponFamilyCatalog.GetName(weapon.Family) + "]\n" + WeaponActionCatalog.Get(weapon).Description) +
                (boss != null ? "\n보스 체력: " + Mathf.CeilToInt(boss.Health) + " / " + Mathf.CeilToInt(boss.MaxHealth) : "");
            GUI.color = new Color(0, 0, 0, .55f);
            GUI.DrawTexture(new Rect(Screen.width - 510, 150, 500, 330), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 500, 155, 485, 320), text, style);
        }
    }
}
