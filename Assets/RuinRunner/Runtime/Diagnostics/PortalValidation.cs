using System;
using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Floor-anchored portal contacts through actual physics, including unlock while overlapping.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class PortalValidation : MonoBehaviour
    {
        private IEnumerator sequence;
        private PlayerController player;
        private int checks;
        private void Start()
        {
            Application.runInBackground = true;
            GameBalance.ApplyLive();
            var inventory = new InventorySystem();
            var camera = CameraFollow.Create();
            player = PlayerController.Create(Vector2.zero, () => false, () => true, () => { }, camera, inventory, new GameSettingsService());
            sequence = Validate();
        }
        private void Update()
        {
            StageValidationInput.BeginFrame();
            if (sequence == null) return;
            try
            {
                if (sequence.MoveNext()) return;
                Debug.Log("[Portal Validation] PASS " + checks + " directional locked/unlocked contacts, actual Rigidbody2D input and one callback each.");
                sequence = null;
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Portal Validation] FAIL " + exception);
                StageValidationInput.ReleaseAll();
                sequence = null;
                Application.Quit(3);
            }
        }
        private IEnumerator Validate()
        {
            var room = Resources.Load<RoomTemplate>("Rooms/hall_broken");
            foreach (RoomTravelDirection direction in Enum.GetValues(typeof(RoomTravelDirection)))
            for (var unlockLate = 0; unlockLate < 2; unlockLate++)
            {
                var root = new GameObject("PortalFixture_" + direction);
                ArenaBuilder.BuildRoom(root.transform, room, 0, Vector2.zero, direction);
                var callbacks = 0;
                var portal = RoomExit.Create(root.transform, room.ExitPoint(direction), direction, 1, _ => callbacks++);
                if (!portal.IsGateVisible) throw new InvalidOperationException("Locked trail has no brush gate");
                if (portal.GetComponent<BoxCollider2D>().offset != Vector2.zero)
                    throw new InvalidOperationException("Sprite pivot contaminated portal contact origin");
                var outward = direction == RoomTravelDirection.Left ? Vector2.left : direction == RoomTravelDirection.Up ? Vector2.up :
                    direction == RoomTravelDirection.Down ? Vector2.down : Vector2.right;
                player.Teleport(room.ExitPoint(direction) - outward * 2f); // Fixture setup only.
                if (unlockLate == 0) portal.Unlock();
                var deadline = Time.time + 1.5f;
                while (Vector2.Distance(player.transform.position, portal.transform.position) > .2f && Time.time < deadline)
                {
                    Move(outward);
                    yield return null;
                }
                Move(Vector2.zero);
                var settle = Time.time + .2f;
                while (Time.time < settle) yield return null;
                if (unlockLate == 1)
                {
                    if (callbacks != 0) throw new InvalidOperationException("Locked portal accepted contact");
                    portal.Unlock();
                }
                settle = Time.time + .5f;
                while (Time.time < settle) yield return null;
                if (callbacks != 1 || !portal.IsConsumed)
                    throw new InvalidOperationException(direction + " callbacks=" + callbacks + " lateUnlock=" + unlockLate);
                if (portal.IsGateVisible) throw new InvalidOperationException("Unlocked trail still visually obstructed");
                Debug.Log("[Portal Validation] " + direction + " lateUnlock=" + unlockLate + " accepted once at " + player.transform.position);
                checks++;
                Destroy(root);
                yield return null;
            }
        }
        private static void Move(Vector2 direction)
        {
            StageValidationInput.SetHeld(GameAction.MoveLeft, direction.x < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveRight, direction.x > .1f);
            StageValidationInput.SetHeld(GameAction.MoveUp, direction.y > .1f);
            StageValidationInput.SetHeld(GameAction.MoveDown, direction.y < -.1f);
        }
    }
}
