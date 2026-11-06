using System.Collections;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Swings every melee family both ways at quarter speed and photographs each step, to compare the trail with the blade.
    /// --combat-sandbox --melee-trail-tour folder</summary>
    public sealed class MeleeTrailTour : MonoBehaviour
    {
        public const string Argument = "--melee-trail-tour";
        private const int CaptureEveryFrames = 3;
        private static readonly WeaponFamily[] ComboFamilies = { WeaponFamily.Katana, WeaponFamily.Dagger, WeaponFamily.Spear, WeaponFamily.Greatsword };
        private const float SlowMotion = .25f, SwingSeconds = .6f, SwingMargin = .1f;
        private const int CaptureRate = 60, LiveFrames = 150;
        private static readonly WeaponFamily[] Families = { WeaponFamily.Longsword, WeaponFamily.Dagger, WeaponFamily.Mace, WeaponFamily.Katana, WeaponFamily.Greatsword, WeaponFamily.Spear };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), Argument) < 0)
                return;
            DontDestroyOnLoad(new GameObject("MeleeTrailTour").AddComponent<MeleeTrailTour>());
        }

        private void Update() => StageValidationInput.BeginFrame();

        private IEnumerator Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, Argument);
            var folder = args[at + 1];
            Directory.CreateDirectory(folder);
            // An optional family after the folder limits the tour to that one; "Combo" means every family with a drawn combo.
            var families = at + 2 < args.Length && args[at + 2] == "Combo" ? ComboFamilies
                : at + 2 < args.Length && System.Enum.TryParse<WeaponFamily>(args[at + 2], out var only) ? new[] { only } : Families;
            GameController game = null;
            while (game == null || game.Player == null || !game.IsRunActive)
            {
                game = FindFirstObjectByType<GameController>();
                yield return null;
            }
            Application.runInBackground = true;
            game.Player.SandboxInvulnerable = true;
            // The hero arrives through the ink and can't swing until it has drained away.
            yield return new WaitForSeconds(3f);
            Time.captureFramerate = CaptureRate;
            // The lunges walk the hero into walls, and a blow doesn't pass through one; each family starts back on open floor.
            var start = (Vector2)game.Player.transform.position;
            foreach (var family in families)
            {
                game.Player.Teleport(start);
                var hits = Mathf.Max(2, MeleeComboFrames.StepCount(family));
                game.Inventory.ActiveSlot.Assign(WeaponLootTable.FindFirst(family));
                game.Player.EquipWeapon(game.Inventory.EquippedWeapon);
                yield return new WaitForSeconds(.3f);
                foreach (var aim in new[] { Vector2.right, Vector2.left, Vector2.up })
                {
                    StageValidationInput.SetAim(aim);
                    yield return new WaitForSeconds(.9f);
                    for (var step = 0; step < hits; step++)
                    {
                        StageValidationInput.SetHeld(GameAction.Attack, true);
                        yield return null;
                        StageValidationInput.SetHeld(GameAction.Attack, false);
                        Time.timeScale = SlowMotion;
                        var name = family + "-" + (aim.x > 0 ? "right" : aim.x < 0 ? "left" : "up") + "-" + step + "-";
                        var frame = 0;
                        var swing = MeleeComboFrames.Has(family, step) ? MeleeComboFrames.SwingSeconds(family, step, float.MaxValue) + SwingMargin : SwingSeconds;
                        for (var clock = 0f; clock < swing; clock += Time.deltaTime)
                        {
                            if (frame % CaptureEveryFrames == 0)
                                ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + (frame / CaptureEveryFrames).ToString("D2") + ".png"));
                            frame++;
                            yield return null;
                        }
                        Time.timeScale = 1f;
                        yield return new WaitForSeconds(.08f);
                    }
                    yield return new WaitForSeconds(.7f);
                }
                if (MeleeComboFrames.StepCount(family) > 0)
                {
                    game.Player.Teleport(start);
                    yield return LiveCombo(game, folder, family);
                }
            }
            Time.captureFramerate = 0;
            Application.Quit(0);
        }

        // The whole combo at full speed against a real target: is the blow felt, does the combo land, does it still hurt.
        private static IEnumerator LiveCombo(GameController game, string folder, WeaponFamily family)
        {
            var player = game.Player;
            var home = (Vector2)player.transform.position;
            StageValidationInput.SetAim(Vector2.right);
            yield return new WaitForSeconds(.6f);
            var enemy = EnemyController.Create(EnemyType.Grunt, home + Vector2.right * 1.5f, player, () => game.IsPaused, _ => { }, game.CameraFollow);
            yield return new WaitForSeconds(.2f);
            var contacts = player.MeleeContactsResolved;
            var health = enemy.Health;
            StageValidationInput.SetHeld(GameAction.Attack, true);
            for (var frame = 0; frame < LiveFrames; frame++)
            {
                if (enemy == null || enemy.IsDead)
                {
                    enemy = EnemyController.Create(EnemyType.Grunt, (Vector2)player.transform.position + Vector2.right * 1.5f, player, () => game.IsPaused, _ => { }, game.CameraFollow);
                    health = enemy.Health;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "Live" + family + "-" + frame.ToString("D3") + ".png"));
                yield return null;
                if (player.MeleeContactsResolved != contacts)
                {
                    contacts = player.MeleeContactsResolved;
                    Debug.Log("[Melee Trail Tour] live frame " + frame + " pose " + player.GetComponent<PlayerAnimationController>().PoseName + " time scale " + Time.timeScale.ToString("0.00") + " damage " + (health - (enemy == null ? 0f : enemy.Health)).ToString("0.0"));
                    if (enemy != null)
                        health = enemy.Health;
                }
            }
            StageValidationInput.SetHeld(GameAction.Attack, false);
            if (enemy != null)
                Destroy(enemy.gameObject);
            yield return null;
        }
    }
}
