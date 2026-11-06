using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Per-frame behaviour, navigation, leaps and rushes.</summary>
    public sealed partial class EnemyController
    {
        private void Update()
        {
            if (dead || player == null || AnimationPaused)
                return;
            TickHealthBar();

            attackTimer -= Time.deltaTime;
            specialTimer -= Time.deltaTime;
            decisionTimer -= Time.deltaTime;
            var delta = (Vector2)(player.transform.position - transform.position);
            var direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right;

            // Scripted moves carry their momentum in instead of stopping on a dime.
            if (acting)
                LinearVelocity = Vector2.MoveTowards(LinearVelocity, Vector2.zero, 26f * Time.deltaTime);
            // A struck enemy reels: it slides with the blow and cannot walk or start a move.
            if (hitStun > 0f && !acting)
            {
                hitStun -= Time.deltaTime;
                LinearVelocity = Vector2.MoveTowards(LinearVelocity, Vector2.zero, 20f * Time.deltaTime);
                spriteRenderer.sortingOrder = 900 - Mathf.RoundToInt(transform.position.y * 100f);
                return;
            }

            if (collisionAttackActive && Vector2.Distance(player.FeetPoint, transform.position) < ContactRadius &&
                !Physics2D.Linecast(transform.position, player.transform.position, 1 << GameLayers.Ground).collider)
                ContactAttack(direction);

            // Being in attack range does not mean the target is reachable.
            // Reposition around cover instead of repeatedly charging/shooting
            // into the same solid module forever.
            if (!acting && Type != EnemyType.RuneTotem && !HasLaneToPlayer)
            {
                SetPlanar(NavigateAroundCover(player.transform.position) * speed);
                SetFacing(LinearVelocity.x);
                spriteRenderer.sortingOrder = 900 - Mathf.RoundToInt(transform.position.y * 100f);
                return;
            }

            switch (Type)
            {
                case EnemyType.Boss:
                    UpdateBoss(delta, direction);
                    break;
                case EnemyType.Tidekeeper:
                    UpdateTidekeeper(delta, direction);
                    break;
                case EnemyType.SealHeart:
                    UpdateSealHeart(delta, direction);
                    break;
                default:
                    UpdateBrain(delta, direction);
                    break;
            }

            if (!acting)
                SetFacing(Mathf.Abs(LinearVelocity.x) > .05f ? LinearVelocity.x : direction.x);
            spriteRenderer.sortingOrder = 900 - Mathf.RoundToInt(transform.position.y * 100f);
        }

        private void UpdateBoss(Vector2 delta, Vector2 direction)
        {
            if (acting)
                return;

            if (TryBeginPhaseShift())
                return;
            var distance = delta.magnitude;
            if (specialTimer <= 0f)
            {
                WardenChooseMove(distance, direction);
                return;
            }

            // It hunts: it stalks in fast from afar, never backs away, and answers anyone hugging it.
            hugTime = distance < 2.7f ? hugTime + Time.deltaTime : 0f;
            if (hugTime > .85f)
            {
                hugTime = 0f;
                StartCoroutine(WardenShove());
                return;
            }
            if (distance > 5.2f)
                SetPlanar(SteerAround(direction) * speed * 1.55f);
            else if (distance > 2.7f)
                SetPlanar(SteerAround(direction) * speed);
            else
                SetPlanar(Vector2.zero);
        }

        private IEnumerator LeapSlam(Vector2 direction, bool isBoss)
        {
            SetFacing(direction.x);
            acting = true;
            AttackVisualPhase = 0;
            specialTimer = isBoss ? 2.4f : 1.85f;
            SetPlanar(Vector2.zero, true);
            if (!isBoss)
                SetPresentationTint(new Color(1f, .92f, .82f));
            else
                bossAnimator.Play(BossPose.Leap, 0, 1, .38f);
            var desiredDistance = Mathf.Min(isBoss ? 5.8f : 4.2f, Vector2.Distance(transform.position, player.transform.position));
            var travelDistance = GetSafeTravelDistance(direction, desiredDistance);
            var start = body.position;
            var target = start + direction * travelDistance;
            CreateLeapShadow(target, isBoss);
            // Fills up over the whole wind-up and flight, reaching the rim as the body lands.
            ArenaHazard.Create(this, player, target, Vector2.one * (2f * SlamRadius(isBoss)), 0, true, (isBoss ? .38f + .52f : .44f + .42f), .02f, 0, DangerTint);
            yield return new WaitForSeconds(isBoss ? .38f : .44f);

            AttackVisualPhase = 1;
            if (isBoss)
                bossAnimator.Play(BossPose.Leap, 2, 4, .52f);
            var baseVisualPosition = visualRoot == null ? Vector3.zero : visualRoot.localPosition;
            leapVisualBasePosition = baseVisualPosition;
            airborneVisual = visualRoot != null;
            var elapsed = 0f;
            var duration = isBoss ? .52f : .42f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                MoveSafely(Vector2.Lerp(start, target, progress));
                if (visualRoot != null)
                {
                    var height = Mathf.Sin(progress * Mathf.PI) * (isBoss ? 1.6f : 1.1f);
                    // The physics root has legacy nonuniform scales. Elevation is
                    // authored in world units, like the landing circle and travel.
                    var parentHeightScale = Mathf.Max(.001f, Mathf.Abs(visualRoot.parent.lossyScale.y));
                    visualRoot.localPosition = baseVisualPosition + Vector3.up * (height / parentHeightScale);
                }
                yield return new WaitForFixedUpdate();
            }
            if (visualRoot != null)
                visualRoot.localPosition = baseVisualPosition;
            airborneVisual = false;
            ClearLandingTelegraph();
            AttackVisualPhase = 2;
            if (isBoss)
                bossAnimator.Play(BossPose.Leap, 5, 7, .32f);
            LinearVelocity = Vector2.zero;
            var projectileCount = isBoss ? 0 : 6; // Boss slam is a landing-area attack, with no bullets.
            for (var index = 0; index < projectileCount; index++)
            {
                var angle = index * Mathf.PI * 2f / projectileCount;
                Shoot(transform.position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 5.5f, contactDamage * .85f, .25f);
            }

            if (Vector2.Distance(player.FeetPoint, transform.position) < SlamRadius(isBoss))
                ContactAttack(direction);
            GroundSlam(transform.position, SlamRadius(isBoss));
            if (isBoss)
            {
                for (int i = 0; i < 4; i++)
                {
                    float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
                    var ground = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SlamRadius(true) * .65f;
                    BossNativeEffect.Create(this, "StoneChip", ground, .28f);
                }
            }
            GameAudio.Play(GameSound.Hit, AudioChannel.Monster, isBoss ? .65f : .4f);
            if (cameraFollow != null)
                cameraFollow.Shake(isBoss ? .35f : .18f);
            yield return new WaitForSeconds(.32f);
            EndAction();
        }

        private Rect? arenaBounds;

        public void ConfigureArena(Rect bounds) => arenaBounds = bounds;

        public void ConfigureNavigation(StageTerrainLayout terrain)
        {
            navigationTerrain = terrain;
            coverRoute.Clear();
            nextCoverRouteAt = 0f;
        }

        // Measured feet to feet, thinner than either pair of feet. A body's root sits at the top of its feet, so from the roots
        // anyone standing with their back to a wall or a pillar looked cut off from everything: enemies would crowd a hero
        // doing that without ever attacking.
        private const float LaneRadius = .12f;
        public bool HasLaneToPlayer => StageNavigation.HasCombatLane(enemyCollider.bounds.center, player.FeetPoint, LaneRadius);
        internal string RouteReport()
        {
            var path = navigationTerrain == null ? null : StageNavigation.FindPath(navigationTerrain, body.position, player.transform.position);
            var cast = Physics2D.CircleCast(enemyCollider.bounds.center, LaneRadius, player.FeetPoint - (Vector2)enemyCollider.bounds.center, Vector2.Distance(player.FeetPoint, enemyCollider.bounds.center), 1 << GameLayers.Ground);
            return "foot " + enemyCollider.bounds.size + " velocity " + LinearVelocity + " acting " + acting + " route " + (path == null ? "none" : path.Count + " points, next " + (path.Count > 1 ? path[1] : path[0])) +
                " kept " + coverRoute.Count + (coverRoute.Count > 0 ? " toward " + coverRoute[0] : "") + " wall " + (cast.collider == null ? "none" : cast.collider.name + " at " + cast.point);
        }

        private Vector2 NavigateAroundCover(Vector2 destination)
        {
            // Long cover routes may deliberately pass along a wall. Only the
            // close-contact case needs separation before ordinary route following.
            if ((destination - body.position).sqrMagnitude < 2.25f)
            {
                var clearance = StageNavigation.WallClearanceDirection(body.position, .32f);
                if (clearance.sqrMagnitude > .001f)
                    return clearance;
            }
            if (navigationTerrain == null)
                return SteerAround(destination - body.position);
            if (Time.time >= nextCoverRouteAt)
            {
                // Spread out, so a room of blocked enemies doesn't all search on one frame.
                nextCoverRouteAt = Time.time + .85f + (GetHashCode() & 7) * .04f;
                var path = StageNavigation.FindPath(navigationTerrain, body.position, destination);
                coverRoute.Clear();
                if (path != null)
                    coverRoute.AddRange(path);
            }
            while (coverRoute.Count > 0 && Vector2.Distance(body.position, coverRoute[0]) < .35f)
                coverRoute.RemoveAt(0);
            if (coverRoute.Count == 0)
                return SteerAround(destination - body.position);
            // Follow grid-clear waypoints directly; local tangent steering can
            // repeatedly choose the same blocked side of a large island.
            var heading = (coverRoute[0] - body.position).normalized;
            // The grid doesn't know about other bodies. Walking square into a totem would otherwise go on forever.
            if (Time.time - lastRouteStepAt > .2f || (body.position - routeProgressPoint).sqrMagnitude > .04f)
            {
                routeProgressPoint = body.position;
                routeProgressAt = Time.time;
            }
            lastRouteStepAt = Time.time;
            if (Time.time - routeProgressAt > .5f)
            {
                routeProgressAt = Time.time;
                sidestepUntil = Time.time + .35f;
                strafeDirection *= -1f;
            }
            return Time.time < sidestepUntil ? Rotate(heading, 75f * strafeDirection) : heading;
        }

        private Vector2 ClampToArena(Vector2 root)
        {
            if (!arenaBounds.HasValue || enemyCollider == null)
                return root;
            var bounds = arenaBounds.Value;
            var offset = Vector2.Scale(enemyCollider.offset, transform.lossyScale);
            var half = Vector2.Scale(enemyCollider.size, transform.lossyScale) * .5f;
            root.x = Mathf.Clamp(root.x, bounds.xMin + half.x - offset.x + .02f, bounds.xMax - half.x - offset.x - .02f);
            root.y = Mathf.Clamp(root.y, bounds.yMin + half.y - offset.y + .02f, bounds.yMax - half.y - offset.y - .02f);
            return root;
        }

        private void FixedUpdate()
        {
            if (dead || body == null)
                return;
            StopAtPlayerBody();
            KeepOnFloor();
            if (!arenaBounds.HasValue)
                return;
            var bounded = ClampToArena(body.position);
            if ((bounded - body.position).sqrMagnitude > .0001f)
                body.position = bounded;
            LinearVelocity = (ClampToArena(body.position + LinearVelocity * Time.fixedDeltaTime) - body.position) / Time.fixedDeltaTime;
        }

        private Collider2D playerFeet;
        private Vector2 lastFloorPoint;
        private bool hasFloorPoint;
        private float offFloorSeconds;

        private void StopAtPlayerBody()
        {
            if (player == null || enemyCollider == null)
                return;
            if (playerFeet == null)
                playerFeet = player.GetComponent<Collider2D>();
            if (((Vector2)player.transform.position - body.position).sqrMagnitude > 36f)
                return;
            LinearVelocity = BodyContact.Stop(enemyCollider, playerFeet, LinearVelocity, Time.fixedDeltaTime, out _);
        }

        // Nothing should carry a body past the walls, but one that gets out can never be hit again and holds the room shut.
        private void KeepOnFloor()
        {
            if (navigationTerrain == null || enemyCollider == null)
                return;
            if (navigationTerrain.IsOpen(enemyCollider.bounds.center))
            {
                lastFloorPoint = body.position;
                hasFloorPoint = true;
                offFloorSeconds = 0f;
                return;
            }
            offFloorSeconds += Time.fixedDeltaTime;
            if (!hasFloorPoint || offFloorSeconds < .2f)
                return;
            body.position = lastFloorPoint;
            LinearVelocity = Vector2.zero;
            offFloorSeconds = 0f;
        }

        private void MoveSafely(Vector2 destination)
        {
            destination = ClampToArena(destination);
            var delta = destination - body.position;
            if (delta.sqrMagnitude < .000001f)
                return;
            var distance = GetSafeTravelDistance(delta.normalized, delta.magnitude);
            body.MovePosition(body.position + delta.normalized * distance);
        }

        private void EndAction()
        {
            ClearLandingTelegraph();
            acting = false;
            collisionAttackActive = false;
            AttackVisualPhase = 2;
            if (!dead && spriteRenderer != null)
                SetPresentationTint(RestingColor);
        }

        private void SetPlanar(Vector2 target, bool immediate = false)
        {
            var next = immediate
                ? target
                : Vector2.MoveTowards(LinearVelocity, target, speed * 7f * Time.deltaTime);
            LinearVelocity = next;
        }

        private Vector2 SteerAround(Vector2 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude < .001f)
                return Vector2.zero;
            desiredDirection.Normalize();
            var origin = enemyCollider == null ? (Vector2)transform.position : (Vector2)enemyCollider.bounds.center;
            if (!Physics2D.Raycast(origin, desiredDirection, .85f, 1 << GameLayers.Ground))
                return desiredDirection;
            var left = Rotate(desiredDirection, 72f);
            var right = Rotate(desiredDirection, -72f);
            var leftBlocked = Physics2D.Raycast(origin, left, .75f, 1 << GameLayers.Ground);
            var rightBlocked = Physics2D.Raycast(origin, right, .75f, 1 << GameLayers.Ground);
            if (!leftBlocked && !rightBlocked)
                return strafeDirection > 0f ? left : right;
            if (!leftBlocked)
                return left;
            if (!rightBlocked)
                return right;
            return -desiredDirection;
        }

        private IEnumerator Rush(Vector2 direction, float desiredDistance, float duration)
        {
            SetFacing(direction.x);
            direction = direction.sqrMagnitude < .001f ? Vector2.right : direction.normalized;
            var distance = GetSafeTravelDistance(direction, desiredDistance);
            var start = body.position;
            var target = start + direction * distance;
            var elapsed = 0f;
            var lastDust = start;
            var trailClip = collisionAttackActive && distance > 1f ? "ERushDust" : null;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Bursts out and bleeds speed at the end, rather than gliding at one rate.
                var progress = Mathf.Clamp01(elapsed / duration);
                MoveSafely(Vector2.Lerp(start, target, 1f - Mathf.Pow(1f - progress, 2.4f)));
                if (trailClip != null && Vector2.Distance(body.position, lastDust) >= .7f)
                {
                    BossNativeEffect.CreateOnFloor(trailClip, body.position, .34f);
                    lastDust = body.position;
                }
                yield return new WaitForFixedUpdate();
            }
            MoveSafely(target);
        }

        private float GetSafeTravelDistance(Vector2 direction, float desiredDistance)
        {
            if (enemyCollider == null)
                return desiredDistance;
            var size = Vector2.Scale(enemyCollider.size, transform.lossyScale);
            var center = body.position + Vector2.Scale(enemyCollider.offset, transform.lossyScale);
            var boundedDestination = ClampToArena(body.position + direction * desiredDistance);
            if (Mathf.Abs(direction.x) > .0001f)
                desiredDistance = Mathf.Min(desiredDistance, Mathf.Abs((boundedDestination.x - body.position.x) / direction.x));
            if (Mathf.Abs(direction.y) > .0001f)
                desiredDistance = Mathf.Min(desiredDistance, Mathf.Abs((boundedDestination.y - body.position.y) / direction.y));
            var hit = Physics2D.BoxCast(center, size, 0f, direction, desiredDistance, 1 << GameLayers.Ground);
            return hit.collider == null ? desiredDistance : Mathf.Max(0f, hit.distance - .06f);
        }

        private GameObject CreateLeapShadow(Vector2 target, bool isBoss)
        {
            ClearLandingTelegraph();
            var shadow = new GameObject("LeapLandingTelegraph");
            landingTelegraph = shadow;
            shadow.transform.position = new Vector3(target.x, target.y, 0f);
            shadow.transform.localScale = Vector3.one * (2f * SlamRadius(isBoss));
            var renderer = GameVisuals.AddSprite(shadow, new Color(1f, .55f, .2f, 1f), -350);
            renderer.sprite = GameVisuals.AttackRing;
            return shadow;
        }

        private void ClearLandingTelegraph()
        {
            if (landingTelegraph != null)
                Destroy(landingTelegraph);
            landingTelegraph = null;
        }

        private void OnDestroy()
        {
            ClearLandingTelegraph();
            LeaveSquad();
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            return new Vector2(direction.x * cosine - direction.y * sine, direction.x * sine + direction.y * cosine);
        }

        private void SetFacing(float direction)
        {
            // Vertical actions retain the last horizontal facing. Do not pivot
            // toward a player who crosses behind an already committed attack.
            if (Mathf.Abs(direction) > .05f)
                facingDirection = Mathf.Sign(direction);
        }

        private void ContactAttack(Vector2 direction)
        {
            if (attackTimer > 0f)
                return;
            attackTimer = .75f;
            player.TakeDamage(contactDamage, direction.sqrMagnitude > .001f ? direction.normalized : Vector2.zero);
        }

        // A weapon that draws its own contact asks for the next hit to skip the shared spark and death flash.
    }
}
