using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Opt-in isolated traversal through real colliders/input. Not a stage completion test.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class RoomTraversalValidation : MonoBehaviour
    {
        private PlayerController player;
        private IEnumerator sequence;
        private int checks;

        private void Start()
        {
            Application.runInBackground = true;
            GameBalance.ApplyLive();
            var inventory = new InventorySystem();
            inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Katana));
            var camera = CameraFollow.Create();
            player = PlayerController.Create(Vector2.zero, () => false, () => true, () => { }, camera, inventory, new GameSettingsService());
            player.EquipWeapon(inventory.EquippedWeapon);
            camera.FocusRoom(Vector2.zero, true);
            sequence = Validate();
        }

        private void Update()
        {
            if (sequence == null) return;
            StageValidationInput.BeginFrame();
            try
            {
                if (sequence.MoveNext()) return;
                Debug.Log("[Room Traversal] PASS " + checks + " routes/wall dashes through real Rigidbody2D and GameAction input; isolated rooms, not a normal run.");
                sequence = null;
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Room Traversal] FAIL " + exception);
                StageValidationInput.ReleaseAll();
                sequence = null;
                Application.Quit(3);
            }
        }

        private IEnumerator Validate()
        {
            var propTestRoot = new GameObject("EnvironmentInteractionRegression");
            var urn = EnvironmentProp.Create(propTestRoot.transform, new Vector2(.85f, 0f), EnvironmentPropKind.Urn);
            player.Teleport(Vector2.zero);
            StageValidationInput.SetAim(Vector2.right);
            StageValidationInput.SetHeld(GameAction.Attack, true);
            yield return null;
            StageValidationInput.SetHeld(GameAction.Attack, false);
            var contactDeadline = Time.time + .7f;
            while (Time.time < contactDeadline) yield return null;
            if (urn.GetComponent<Collider2D>().enabled) throw new InvalidOperationException("Ordinary sword attack did not break the urn");
            Debug.Log("[Room Traversal] Urn broken by ordinary sword input/contact; no direct Hit() call.");
            checks++;
            Destroy(propTestRoot);
            yield return null;
            foreach (var room in Resources.LoadAll<RoomTemplate>("Rooms"))
            {
                var root = new GameObject("Traversal_" + room.roomId);
                ArenaBuilder.BuildRoom(root.transform, room, 0, Vector2.zero, room.preferredExitDirection);
                player.Teleport(room.playerSpawn); // Test setup only. Every route below uses movement input.
                yield return null;
                var targets = new List<Vector2>
                {
                    room.Entrance(RoomTravelDirection.Left), room.Entrance(RoomTravelDirection.Right),
                    room.Entrance(RoomTravelDirection.Down), room.Entrance(RoomTravelDirection.Up), RoomLayoutRules.RewardPosition
                };
                foreach (var enemy in room.enemies) targets.Add(enemy.position);
                foreach (var target in targets)
                {
                    var walk = WalkTo(room, target);
                    while (walk.MoveNext()) yield return null;
                    checks++;
                }
                foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                {
                    var approach = Vector2.Scale(direction, room.Size * .5f - Vector2.one);
                    var walk = WalkTo(room, approach);
                    while (walk.MoveNext()) yield return null;
                    while (player.DashCharges == 0) yield return null;
                    StageValidationInput.SetAim(direction);
                    SetMove(direction);
                    StageValidationInput.SetHeld(GameAction.Dash, true);
                    yield return null;
                    StageValidationInput.SetHeld(GameAction.Dash, false);
                    SetMove(Vector2.zero);
                    var until = Time.time + .5f;
                    while (Time.time < until) yield return null;
                    if (!RoomLayoutRules.IsClear(room, player.transform.position, RoomLayoutRules.FootHalfSize - Vector2.one * .015f))
                        throw new InvalidOperationException(room.roomId + " dash penetrated room geometry at " + player.transform.position);
                    checks++;
                }
                Debug.Log("[Room Traversal] " + room.roomId + " routes and four wall dashes OK");
                Destroy(root);
                yield return null;
            }
            var bossRoom = System.Array.Find(Resources.LoadAll<RoomTemplate>("Rooms"), candidate => candidate.bossRoom);
            var arena = new GameObject("BossBoundaryRegression");
            ArenaBuilder.BuildRoom(arena.transform, bossRoom, 0, Vector2.zero, null);
            var boundary = bossRoom.LocalBounds;
            player.Teleport(new Vector2(-9f, 0f));
            var rushMethod = typeof(EnemyController).GetMethod("Rush", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var leapMethod = typeof(EnemyController).GetMethod("LeapSlam", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (var pattern = 0; pattern < 2; pattern++)
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Mathf.PI / 4f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var origin = Vector2.Scale(direction, bossRoom.Size * .5f - Vector2.one * 1.8f);
                var boss = EnemyController.Create(EnemyType.Boss, origin, player, () => true, _ => { }, null);
                boss.ConfigureArena(boundary);
                yield return null;
                var action = pattern == 0
                    ? (IEnumerator)rushMethod.Invoke(boss, new object[] { direction, 30f, .4f })
                    : (IEnumerator)leapMethod.Invoke(boss, new object[] { direction, true });
                boss.StartCoroutine(action);
                var deadline = Time.time + (pattern == 0 ? .8f : 1.5f);
                while (Time.time < deadline)
                {
                    var bounds = boss.GetComponent<BoxCollider2D>().bounds;
                    if (bounds.min.x < boundary.xMin - .02f || bounds.max.x > boundary.xMax + .02f ||
                        bounds.min.y < boundary.yMin - .02f || bounds.max.y > boundary.yMax + .02f)
                        throw new InvalidOperationException("Boss escaped during " + (pattern == 0 ? "rush" : "leap") + " direction " + index);
                    yield return null;
                }
                checks++;
                Destroy(boss.gameObject);
                RuntimeSceneObjects.ClearProjectiles();
                yield return null;
            }
            Debug.Log("[Room Traversal] Boss eight-direction rush/leap boundary checks OK (16 forced isolated patterns).");
        }

        private IEnumerator WalkTo(RoomTemplate room, Vector2 target)
        {
            var route = RoomLayoutRules.FindPath(room, player.transform.position, target);
            if (route == null) throw new InvalidOperationException(room.roomId + " no route to " + target + " from " + player.transform.position);
            // Collapse straight grid runs only; never shortcut an arbitrary diagonal.
            for (var i = 1; i + 1 < route.Count;)
            {
                if (Vector2.Dot((route[i] - route[i - 1]).normalized, (route[i + 1] - route[i]).normalized) > .999f) route.RemoveAt(i);
                else i++;
            }
            var deadline = Time.time + 15f + room.Size.magnitude * .3f;
            while (Vector2.Distance(player.transform.position, target) > .3f)
            {
                if (Time.time > deadline) throw new InvalidOperationException(room.roomId + " physical movement stalled toward " + target + " at " + player.transform.position);
                var position = (Vector2)player.transform.position;
                var velocity = player.GetComponent<Rigidbody2D>().linearVelocity;
                if (route.Count > 1 && Vector2.Distance(position, route[0]) < .18f)
                {
                    SetMove(Vector2.zero);
                    if (velocity.sqrMagnitude > .04f) { yield return null; continue; }
                    route.RemoveAt(0);
                }
                var direction = route[0] - position;
                StageValidationInput.SetAim(direction);
                // Release keys early enough for normal braking. Otherwise the pilot
                // oscillates around sub-step waypoints forever at full run speed.
                if (direction.magnitude < .5f && Vector2.Dot(velocity, direction) > 0f &&
                    velocity.sqrMagnitude / 128f + .06f >= direction.magnitude)
                { SetMove(Vector2.zero); yield return null; continue; }
                // Respect grid corners, and stop each axis in world units rather
                // than normalizing a tiny error back into full diagonal input.
                SetMove(new Vector2(Mathf.Abs(direction.x) > .055f ? Mathf.Sign(direction.x) : 0f,
                    Mathf.Abs(direction.y) > .055f ? Mathf.Sign(direction.y) : 0f));
                yield return null;
            }
            SetMove(Vector2.zero);
            // Allow ordinary braking to settle before planning the next route.
            var until = Time.time + .15f;
            while (Time.time < until) yield return null;
        }

        private static void SetMove(Vector2 direction)
        {
            StageValidationInput.SetHeld(GameAction.MoveLeft, direction.x < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveRight, direction.x > .1f);
            StageValidationInput.SetHeld(GameAction.MoveDown, direction.y < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveUp, direction.y > .1f);
        }
    }
}
