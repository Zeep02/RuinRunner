using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    public sealed class PresentationValidation : MonoBehaviour
    {
        private IEnumerator sequence;
        private int checks;
        private string directory;
        private void Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--validation-capture-dir");
            directory = index < 0 ? Application.temporaryCachePath : args[index + 1];
            Directory.CreateDirectory(directory);
            sequence = Validate();
        }
        private void Update()
        {
            if (sequence == null) return;
            StageValidationInput.BeginFrame();
            try
            {
                if (sequence.MoveNext()) return;
                Debug.Log("[Presentation QA] PASS " + checks + " checks; isolated camera renders, not a normal stage run or HUD screenshot.");
                sequence = null; Application.Quit(0);
            }
            catch (Exception error) { Debug.LogError("[Presentation QA] FAIL " + error); sequence = null; Application.Quit(3); }
        }
        private IEnumerator Validate()
        {
            foreach (var item in WeaponLootTable.All) CheckIcon(item.Id, InventoryIconKind.Weapon);
            foreach (PassiveItemType item in Enum.GetValues(typeof(PassiveItemType))) CheckIcon(item.ToString(), InventoryIconKind.Passive);
            foreach (var item in AccessoryLootTable.All) CheckIcon(item.IconId, item.IconKind);
            var follow = CameraFollow.Create();
            follow.enabled = false;
            var camera = follow.GetComponent<Camera>();
            camera.enabled = false;
            var inventory = new InventorySystem();
            inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Katana));
            var player = PlayerController.Create(Vector2.zero, () => false, () => true, () => { }, follow, inventory, new GameSettingsService());
            player.EquipWeapon(inventory.EquippedWeapon);
            var room = ScriptableObject.CreateInstance<RoomTemplate>();
            room.decorations = Array.Empty<RoomDecorationData>();
            foreach (var y in new[] { 0f, 36f, 72f, -36f, -72f })
            {
                var center = new Vector2(0, y);
                var root = new GameObject("PresentationRoom");
                ArenaBuilder.BuildRoom(root.transform, room, 0, center, RoomTravelDirection.Right);
                var exit = RoomExit.Create(root.transform, center + room.ExitPoint(RoomTravelDirection.Right), RoomTravelDirection.Right, 1, _ => { });
                exit.Unlock();
                player.Teleport(center + Vector2.left * 5);
                var enemies = new EnemyController[5];
                for (var i = 0; i < enemies.Length; i++)
                    enemies[i] = EnemyController.Create((EnemyType)i, center + new Vector2(-2 + i * 2.7f, 0), player, () => false, _ => { }, follow);
                camera.transform.position = new Vector3(1f, y + 1.2f, -10f);
                for (var frame = 0; frame < 5; frame++) yield return null;
                foreach (var enemy in enemies) enemy.enabled = false;
                var full = Render(camera);
                CheckVisible(camera, player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>(), full, "player y=" + y);
                foreach (var enemy in enemies)
                {
                    var visual = enemy.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                    CheckVisible(camera, visual, full, enemy.Type + " y=" + y);
                    Require(enemy.DamageCollider.isTrigger && EnemyController.DamageTarget(enemy.GetComponent<BoxCollider2D>()) == null,
                        "movement collider does not receive a second hit " + enemy.Type);
                    var hitBounds = enemy.DamageCollider.bounds;
                    var drawnBounds = SpriteVisibleBounds.World(visual);
                    // A body bobs a pixel or two around its feet from frame to frame; the hurtbox follows the idle pose as a whole.
                    const float BobSlack = 2.5f / 32f;
                    Require(hitBounds.min.x <= drawnBounds.min.x + BobSlack && hitBounds.min.y <= drawnBounds.min.y + BobSlack &&
                        hitBounds.max.x >= drawnBounds.max.x - BobSlack && hitBounds.max.y >= drawnBounds.max.y - BobSlack,
                        "hurtbox covers rendered body " + enemy.Type + " hit=" + enemy.DamageCollider.bounds + " art=" + visual.bounds);
                    var health = enemy.transform.Find("HealthBack");
                    Require(enemy.Type == EnemyType.Boss ? !health.gameObject.activeSelf : health.GetComponent<SpriteRenderer>().bounds.min.y > drawnBounds.max.y,
                        "health display outside body " + enemy.Type);
                }
                File.WriteAllBytes(Path.Combine(directory, "world-y-" + y + ".png"), full.EncodeToPNG());
                Destroy(full); Destroy(root);
                foreach (var enemy in enemies) Destroy(enemy.gameObject);
                yield return null;
            }
        }
        private void CheckIcon(string id, InventoryIconKind kind)
        {
            var texture = ItemIconLibrary.Get(id, kind);
            // Signature weapon and accessory icons are cut to their art, not to a square; DrawGUI keeps their proportions.
            Require(texture != null && texture.isReadable, "individual PNG " + kind + "/" + id);
            if (kind != InventoryIconKind.Weapon || SignatureWeaponCatalog.Has(id)) return;
            var bounds = ItemIconLibrary.ContentBounds(texture);
            Require(bounds.width > 0 && bounds.xMin > 2 && bounds.yMin > 2 && bounds.xMax < texture.width - 2 && bounds.yMax < texture.height - 2,
                "transparent padding / no clipping " + id);
            Require(Mathf.Abs(bounds.center.x - texture.width * .5f) <= 1f && Mathf.Abs(bounds.center.y - texture.height * .5f) <= 1f, "centered alpha bounds " + id);
        }
        private void CheckVisible(Camera camera, SpriteRenderer renderer, Texture2D full, string label)
        {
            renderer.enabled = false;
            var without = Render(camera);
            renderer.enabled = true;
            var a = full.GetPixels32(); var b = without.GetPixels32(); var changed = 0;
            for (var i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) changed++;
            Destroy(without);
            Require(changed > 20, "body contributes visible pixels: " + label + " pixels=" + changed);
        }
        private static Texture2D Render(Camera camera)
        {
            var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            return texture;
        }
        private void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++; Debug.Log("[Presentation QA] OK " + message);
        }
    }
}
