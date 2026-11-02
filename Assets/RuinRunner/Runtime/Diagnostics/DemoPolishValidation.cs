using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class MotionValidation
    {
        private void PolishCapture(string name)
        {
            if (screenshot != null) screenshot.Invoke(null, new object[] { Path.Combine(captureDirectory, name + ".png"), 1 });
        }

        private IEnumerator ValidateDemoPolish()
        {
            var settings = new GameSettingsService();
            player.EquipWeapon(DemoContent.Weapon("rust_sword"));
            var floor = new GameObject("ValidationFloor");
            floor.transform.localScale = new Vector3(35, 20, 1);
            PrototypeVisuals.AddSprite(floor, new Color(.29f, .35f, .46f), -100);
            var steps = MeleeComboCatalog.Get(player.EquippedWeapon).LightSteps;
            Require(steps[0].Motion == MeleeAttackMotion.Sweep && steps[1].Motion == MeleeAttackMotion.Sweep &&
                steps[2].Motion == MeleeAttackMotion.Thrust, "authored sweep / reverse sweep / thrust profile");
            Require(MeleeMotionTiming.BladeAngle(.3f, false, 0) * MeleeMotionTiming.BladeAngle(.3f, false, 1) < 0,
                "first two sweeps have opposite rotation");
            StageValidationInput.SetAim(Vector2.right);
            StageValidationInput.SetHeld(GameAction.Attack, true);
            var budget = 140;
            while (!player.IsThrustActive && budget-- > 0) yield return null;
            Require(player.IsThrustActive && player.AttackAnimationSequence == 3, "held input reaches third attack");
            StageValidationInput.ReleaseAll();
            var origin = player.transform.position;
            var contacts = player.MeleeContactsResolved;
            var charges = player.DashCharges;
            var enemy = EnemyController.Create(EnemyType.Boss, new Vector2(1.55f, .05f), player, () => true, _ => { }, null);
            enemy.enabled = false;
            var health = enemy.Health;
            var waited = 0;
            while (player.AttackAnimationProgress < .32f) { waited++; yield return null; }
            Require(waited >= 4 && waited <= 12 && player.MeleeContactsResolved == contacts && enemy.Health == health,
                "third attack visibly waits in anticipation without early damage");
            Require(Vector3.Distance(origin, player.transform.position) < .01f, "preparation does not drift forward");
            PolishCapture("01-thrust-ready");
            paused = true;
            var progress = player.AttackAnimationProgress;
            for (var i = 0; i < 12; i++) yield return null;
            Require(player.AttackAnimationProgress == progress && Vector3.Distance(origin, player.transform.position) < .01f,
                "pause freezes thrust preparation and travel");
            paused = false;
            StageValidationInput.SetAim(Vector2.left);
            Require(player.MeleeAimDirection == Vector2.right, "thrust retains its committed aim");
            while (player.AttackAnimationProgress < .69f) yield return null;
            PolishCapture("02-thrust-contact");
            Require(player.MeleeContactsResolved == contacts + 1 && enemy.Health < health, "thrust deals one hit at contact");
            while (player.IsThrustActive) yield return null;
            for (var i = 0; i < 5; i++) yield return null;
            Require(Vector3.Distance(player.transform.position,origin)<.05f,
                "standing thrust does not force movement; movement freedom is covered by CombatFeelFixture");
            Require(player.AttackAnimationSequence == 3 && player.MeleeContactsResolved == contacts + 1,
                "release does not start another combo or duplicate the thrust");
            Require(player.DashCharges == charges && player.InvincibilityTicksRemaining == 0,
                "thrust neither spends dash charges nor grants invulnerability");
            Destroy(enemy.gameObject);

            player.Teleport(Vector2.zero);
            var wall = new GameObject("ThrustWall"); wall.layer = GameLayers.Ground;
            wall.transform.position = new Vector3(.75f, -.2f, 0);
            var collider = wall.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.2f, 4);
            StageValidationInput.SetAim(Vector2.right);
            typeof(PlayerController).GetField("comboIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 2);
            typeof(PlayerController).GetField("comboResetTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 2f);
            Tap(GameAction.Attack);
            yield return null;
            Require(player.IsThrustActive, "wall fixture starts a thrust through normal attack input");
            for (var i = 0; i < 58; i++)
            {
                yield return null;
                Require(!player.GetComponent<Collider2D>().Distance(collider).isOverlapped,
                    "thrust preserves wall clearance throughout travel/recovery: " + i);
            }
            Require(player.transform.position.x < .5f && !player.GetComponent<Collider2D>().Distance(collider).isOverlapped,
                "thrust capsule sweep stops before wall without overlap");
            Destroy(wall);
            player.Teleport(Vector2.zero);

            var openCount = 0;
            FieldLootPickup loot = null;
            var chest = RewardChest.Create(new Vector2(-2.2f, 0f), () =>
            {
                openCount++;
                loot = FieldLootPickup.Create(new Vector2(-2.2f, 0), "검증용 활", Color.cyan, player, settings, () => true, 0,
                    InventoryIconFactory.GetWorldSprite("hand_crossbow", Color.white, InventoryIconKind.Weapon));
            }, settings);
            var chestPosition = chest.transform.position;
            var seen = new HashSet<int> { 0 };
            PolishCapture("03-chest-0");
            yield return null;
            typeof(RewardChest).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chest, null);
            for (var i = 0; i < 90; i++)
            {
                yield return null;
                if (seen.Add(chest.FrameIndex)) PolishCapture("03-chest-" + chest.FrameIndex);
                if (chest.FrameIndex < 3) Require(openCount == 0, "no loot before lid opens");
                Require(chest.transform.position == chestPosition && chest.transform.localScale == Vector3.one, "chest anchor and scale stay fixed");
                if (loot != null) Require(Vector3.Distance(loot.transform.position, loot.HaloCenter) < .001f,
                    "loot marker stays on the floor while the item lifts");
            }
            Require(seen.Count == 6 && chest.FrameIndex == 5 && openCount == 1, "all six native chest poses shown; one reward; final pose retained");
            Require(loot != null && loot.IsSettled && Vector2.Distance(loot.transform.position, loot.LandingPosition) < .001f, "loot settles at an accessible floor anchor");
            typeof(RewardChest).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chest, null);
            for (var i = 0; i < 35; i++) yield return null;
            Require(openCount == 1, "reopening an open chest cannot duplicate reward");
            PolishCapture("04-loot-settled");

            var gauge = player.GetComponentInChildren<DashChargeGraphic>();
            Tap(GameAction.Dash);
            for (var i = 0; i < 14; i++) yield return null;
            Tap(GameAction.Dash);
            for (var i = 0; i < 14; i++) yield return null;
            player.Teleport(Vector2.zero);
            Require(player.DashCharges == 0, "two dashes consume two charges");
            var lastFill = gauge.FirstFill;
            for (var i = 0; i < 60 && player.DashCharges == 0; i++)
            {
                yield return null;
                Require(gauge.FirstFill >= lastFill && gauge.SecondFill == 0f, "first chevron fills monotonically before second");
                lastFill = gauge.FirstFill;
            }
            while (player.DashCharges < 2) yield return null;
            yield return null;
            Require(gauge.FirstFill == 1 && gauge.SecondFill == 1, "both chevrons fully bright when recharged");
            for (var i = 0; i < 5; i++)
            {
                var example = new GameObject("GaugePreview" + i);
                example.transform.position = new Vector3(-2 + i, -2, 0);
                example.AddComponent<DashChargeGraphic>().SetCharges(1, i * .25f);
            }
            for (var i = 0; i < 5; i++) yield return null;
            PolishCapture("05-dash-fill-0-25-50-75-100");
            for (var region = 0; region < 4; region++) foreach (var placement in DemoContent.Enemies(region))
                Require(placement.type != EnemyType.Shooter, "seedling excluded from demo region " + region);
            var radial = EnemyProjectile.Create(new Vector2(2, 1), Vector2.up, 0, 1, Color.red, .27f, EnemyProjectileArt.BossRadial);
            var aimed = EnemyProjectile.Create(new Vector2(3, 1), new Vector2(-1, 1), 0, 1, Color.red, .24f, EnemyProjectileArt.BossAimed);
            var radialFrames = new HashSet<int>(); var aimedFrames = new HashSet<int>();
            var aimedRotation = aimed.transform.rotation;
            for (var i = 0; i < 50; i++)
            {
                yield return null;
                if (radialFrames.Add(radial.FrameIndex)) PolishCapture("06-projectiles-frame-" + radial.FrameIndex);
                aimedFrames.Add(aimed.FrameIndex);
                Require(aimed.transform.rotation == aimedRotation, "aimed projectile never spins away from flight direction");
            }
            Require(radialFrames.Count == 8 && aimedFrames.Count == 8, "both supplied projectile rows animate all eight frames");
            Require(Mathf.Approximately(radial.GetComponent<CircleCollider2D>().radius, .135f) &&
                Mathf.Approximately(aimed.GetComponent<CircleCollider2D>().radius, .12f), "new projectile art retains original hit radii");
            var savedFrame = radial.FrameIndex; Time.timeScale = 0;
            for (var i = 0; i < 10; i++) yield return null;
            Require(radial.FrameIndex == savedFrame, "projectile animation freezes while paused");
            Time.timeScale = 1;
            for (var i = 0; i < 20; i++) yield return null;
        }
    }
}
