using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PlayerController
    {
        const float PinnedSeconds = .5f, SlipSeconds = .45f;
        static readonly Collider2D[] nearbyBodies = new Collider2D[24];
        Collider2D footCollider;
        float pinnedFor, slipUntil;

        // Walking into an enemy stops the hero and leaves the enemy where it stands. A dash passes through, and so does
        // a hero who has pushed against living bodies for a moment: a crowd must never hold someone in place to die.
        void StopAtEnemyBodies(Vector2 desired)
        {
            if (Time.time < slipUntil) return;
            if (footCollider == null) footCollider = GetComponent<Collider2D>();
            var moving = LinearVelocity;
            var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << GameLayers.Enemy };
            var reach = .5f + moving.magnitude * Time.fixedDeltaTime;
            var count = Physics2D.OverlapCircle(footCollider.bounds.center, reach, filter, nearbyBodies);
            var free = moving;
            var heldByMover = false;
            for (var i = 0; i < count; i++)
            {
                free = BodyContact.Stop(footCollider, nearbyBodies[i], free, Time.fixedDeltaTime, out var stopped);
                var body = nearbyBodies[i].attachedRigidbody;
                heldByMover |= stopped && (body == null || body.constraints != RigidbodyConstraints2D.FreezeAll);
            }
            pinnedFor = heldByMover && desired.magnitude > 1f && free.magnitude < desired.magnitude * .25f ? pinnedFor + Time.fixedDeltaTime : 0f;
            if (pinnedFor >= PinnedSeconds) { pinnedFor = 0f; slipUntil = Time.time + SlipSeconds; return; }
            LinearVelocity = free;
        }
    }
}
