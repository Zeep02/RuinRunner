using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>
    /// Graphical end-to-end pilot. Enabled only with --automated-stage-validation.
    /// It completes the stage through normal movement, combat, interaction, loot,
    /// upgrade, weapon switching, and portal input paths.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed partial class AutomatedStageValidation : MonoBehaviour
    {
        private const float RunTimeoutSeconds = 2700f;
        private GameController game;
        private float startedAt;
        private float nextAttackAt;
        private float nextDashAt;
        private float nextWeaponActionAt;
        private float nextInteractAt;
        private float attackReleaseAt;
        private bool attackHeld;
        private bool completionLogged;
        public bool Complete=>completionLogged;
        private int lastRoom = -1;
        private int lastStage = -1;
        private readonly HashSet<string> visitedRegions=new HashSet<string>();
        private int branchVisits,clearedRevisits;
        private bool leavingEquipment;
        private bool preferRanged;
        private bool preferSkills;
        private int maintenanceRoom = -1, maintenancePhase, upgradeCount, disassemblyCount, firstStageUpgrades, firstStageDisassemblies;
        private long maintenanceIdentity;
        private int maintenanceLevel, maintenanceMaterials, maintenanceDelta;
        private bool maintenanceActive;
        private float maintenanceDeadline;
        private int lastKills = -1;
        private Vector2 lastPosition;
        private float stalledFor;
        private float nextStatusAt;
        private string currentIntent = "boot";
        private string captureDirectory;
        private bool captureScreenshots;
        private int captureIndex;
        private float nextCaptureAt;
        private readonly List<Vector2> route = new List<Vector2>();
        private Vector2 routeDestination;
        private int routeRoom = -1;
        private float nextRouteAt;
        private bool brakingAtWaypoint;
        private Vector2 telegraphMovementStart;
        private float telegraphMovementStartedAt;
        private bool telegraphMovementStarted;
        private bool telegraphMovementChecked;

        public void Initialize(GameController controller, string outputDirectory = null, bool rangedBuild = false, bool record = false)
        {
            game = controller;
            Application.runInBackground = true;
            startedAt = Time.unscaledTime;
            nextStatusAt = startedAt + 4f;
            preferRanged = rangedBuild || GetCommandLineValue("--validation-build") == "ranged";
            preferSkills = GetCommandLineValue("--validation-build") == "skill";
            backendJourney=!game.UsesLegacyFixture;
            preferMobility=GetCommandLineValue("--validation-build")=="mobility";
            captureDirectory = outputDirectory ?? GetCommandLineValue("--validation-capture-dir");
            captureScreenshots = System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--validation-no-screenshots") < 0;
            if (!string.IsNullOrWhiteSpace(captureDirectory))
            {
                System.IO.Directory.CreateDirectory(captureDirectory);
                if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--combat-pressure-trace")>=0)
                    game.gameObject.AddComponent<BossCombatObservation>().Initialize(game,captureDirectory);
                nextCaptureAt = startedAt + 1.5f;
                if(captureScreenshots && (record || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--validation-record")>=0))
                    game.gameObject.AddComponent<RunEvidenceRecorder>().Initialize(game,captureDirectory);
            }
            if (InitializeRecoveryMode()) return;
            Debug.Log("[Stage E2E] START — "+(backendJourney?"server profile/town journey":"LEGACY SUPPLIED FIXTURE; not a normal campaign")+", automated GameAction input and public UI button handlers");
            Debug.Log("[Stage E2E] BUILD " + (preferSkills ? "skill" : preferRanged ? "ranged" : "melee") + "; ordinary drops and player-facing inventory keys only");
            if (Application.isBatchMode) Debug.Log("[Stage E2E] HEADLESS LOGIC ONLY — no rendered play, video, visual completion or performance evidence.");
        }

        private void Update()
        {
            StageValidationInput.BeginFrame();
            if (game == null || !StageValidationInput.Enabled) return;

            ReleaseTimedInputs();
            CaptureValidationFrame();
            if (completionLogged) return;
            if (recoveryTicket != null) { DriveRecoveryJourney(); return; }

            if (Time.unscaledTime - startedAt > RunTimeoutSeconds)
            {
                Finish(false, "timeout");
                return;
            }

            if(backendJourney&&DriveSessionJourney())return;
            if(game.Phase==GameSessionPhase.BossIntro){StageValidationInput.ReleaseAll();return;}
            if(DriveAdversityState())return;
            if(DriveInputObservations())return;
            ObserveAwakenedEquipment();
            if(backendJourney&&game.IsServiceOpen){DriveFieldFacility();return;}

            if (!game.IsRunActive || game.Player == null)
            {
                currentIntent = "waiting for player";
                ReleaseMovement();
                return;
            }

            if (game.IsStageTransitioning)
            {
                currentIntent = "stage loading";
                StageValidationInput.ReleaseAll();
                route.Clear(); routeRoom = -1;
                return;
            }
            if (game.CurrentStageIndex != lastStage)
            {
                lastStage = game.CurrentStageIndex; lastRoom = -1;
                route.Clear(); routeRoom = -1; stalledFor = 0;
                leavingEquipment = false;
                maintenanceRoom = -1;
                Debug.Log("[Stage E2E] STAGE " + (lastStage+1) + "/3 seed=" + game.RunSeed);
            }

            if (game.CurrentRoom != lastRoom)
            {
                lastRoom = game.CurrentRoom;
                var regionKey=game.CurrentStageIndex+":"+game.DungeonMap.GetRegionId(lastRoom);
                if(!visitedRegions.Add(regionKey)&&game.DungeonMap.Regions[lastRoom].Phase==EncounterRegionPhase.Cleared)clearedRevisits++;
                else if(game.DungeonMap.IsProcedural&&!game.DungeonMap.RequiresCombat(lastRoom)&&lastRoom!=game.DungeonMap.EntranceRoomIndex)branchVisits++;
                Debug.Log("[Stage E2E] ROOM " + (lastRoom + 1) + "/" + game.DungeonMap.RoomCount + " entered " + game.DungeonMap.GetRoom(lastRoom).roomId);
            }
            if (!telegraphMovementChecked && game.IsEncounterTransitioning)
            {
                if (!telegraphMovementStarted)
                {
                    telegraphMovementStarted = true;
                    telegraphMovementStart = game.Player.transform.position;
                    telegraphMovementStartedAt = Time.fixedTime;
                }
                currentIntent = "verify movement during spawn telegraph";
                SetMove(Vector2.up);
                return;
            }
            if (!telegraphMovementChecked && telegraphMovementStarted)
            {
                telegraphMovementChecked = true;
                var distance = Vector2.Distance(telegraphMovementStart, game.Player.transform.position);
                var duration = Time.fixedTime - telegraphMovementStartedAt;
                // The first rendered frame can follow a long asset/shader load.
                // Measure the simulation opportunity after input was actually issued,
                // not wall time since Initialize. A short sample is not a PASS.
                if (duration < .45f)
                    Debug.Log("[Stage E2E] Telegraph movement INCONCLUSIVE startup sample=" + duration.ToString("0.00") + "s distance=" + distance.ToString("0.00"));
                else if (distance < 1f) { Finish(false, "movement blocked during telegraph: input=" + duration.ToString("0.00") + "s distance=" + distance.ToString("0.00")); return; }
                else Debug.Log("[Stage E2E] Telegraph movement PASS input=" + duration.ToString("0.00") + "s distance=" + distance.ToString("0.00"));
            }
            if (game.Stats != null && game.Stats.EnemiesDefeated != lastKills)
            {
                lastKills = game.Stats.EnemiesDefeated;
                Debug.Log("[Stage E2E] KILLS " + lastKills + " / HP " + game.Player.Health.ToString("0.0"));
            }

            if (game.IsChoosingAccessory)
            {
                currentIntent = "choose level upgrade";
                ReleaseMovement();
                var choice = SelectUpgrade();
                Tap(GameSettingsService.AccessoryChoiceAction(choice));
                return;
            }

            if (game.IsGameOver)
            {
                if (!game.IsVictory && game.Player.DeathProgress < 1f) { StageValidationInput.ReleaseAll(); return; }
                Finish(game.IsVictory && game.StagesCleared == CampaignStage.Count && (!game.DungeonMap.IsProcedural || branchVisits>=3&&clearedRevisits>=3),
                    game.IsVictory ? "all three guardians cleared; final ending reached; branches="+branchVisits+" cleared revisits="+clearedRevisits+"; legacy maintenance counts are not new growth-system validation" : "player defeated");
                return;
            }
            if (leavingEquipment)
            {
                ObserveAdversityExit();
                if (game.PendingStageRewards == 0)
                {
                    StageValidationInput.SetHeld(GameAction.Interact,false);
                    leavingEquipment = false;
                    nextInteractAt = Time.unscaledTime + .3f;
                }
                else StageValidationInput.SetHeld(GameAction.Interact,true);
                return;
            }

            if (!backendJourney&&maintenanceActive) { DriveEquipmentMaintenance(); return; }

            if (game.IsInventoryOpen || game.EnemiesAlive == 0 && !game.IsEncounterTransitioning)
                if (EquipStoredUpgrade()) return;

            if (game.EnemiesAlive > 0)
            {
                DriveCombat();
                return;
            }

            DriveRoomCleanup();
        }

        private void DriveCombat()
        {
            if(DriveAdversityCombat())return;
            var player = game.Player;
            SelectCombatWeapon();
            var target = FindNearestEnemy(player.transform.position);
            if (target == null)
            {
                ReleaseMovement();
                return;
            }

            var delta = (Vector2)(target.DamageCollider.bounds.center - player.transform.position);
            currentIntent = "fight " + target.Type + " at " + Format(target.transform.position);
            StageValidationInput.SetAim(delta);
            UpdateStallTimer(player.transform.position);

            var ranged = player.EquippedWeapon != null && player.EquippedWeapon.AttackType == WeaponAttackType.Ranged;
            var desiredDistance = ranged ? 4.8f : 1.15f;
            var distance = delta.magnitude;
            var direction = distance > .001f ? delta / distance : Vector2.zero;
            // Use a real clearance corridor. At a masonry corner the center ray
            // can be open even though the gun muzzle and shot cannot pass.
            var lineOfSight = StageNavigation.HasCombatLane(player.transform.position,
                target.transform.position, .32f);
            var move = Vector2.zero;
            if (!lineOfSight || distance > desiredDistance + .35f) move = direction;
            else if (ranged && distance < desiredDistance - .9f) move = -direction;
            else if (!ranged && distance < .55f) move = -direction;
            // React only to visible, already committed attack warnings. No enemy state is changed.
            if (target.IsBoss && target.IsActing && target.AttackVisualPhase < 2 && distance < 3.4f)
                move = -direction;
            var escape = HazardEscape(player.transform.position);
            if (escape.sqrMagnitude < .01f) escape = ProjectileEscape(player.transform.position);
            if (escape.sqrMagnitude < .01f) escape = VisibleWarningEscape(player.transform.position);
            if (escape.sqrMagnitude > .01f) move = escape;
            // Visible, unobstructed targets do not need a whole-stage BFS every time
            // they move. Reserve route finding for blocked approaches.
            var combatMove = escape.sqrMagnitude > .01f ? escape : move.sqrMagnitude > .01f && Vector2.Dot(move, direction) > 0f
                ? lineOfSight ? SteerAroundObstacles(player.transform.position, move) : NavigateToward(target.transform.position)
                : SteerAroundObstacles(player.transform.position, move);
            if (!lineOfSight && distance < 1.5f && escape.sqrMagnitude < .01f)
            {
                var clearance = StageNavigation.WallClearanceDirection(player.transform.position, .34f);
                if (clearance.sqrMagnitude > .01f) combatMove = clearance;
            }
            SetMove(combatMove);

            if (ranged)
            {
                var slot = player.ActiveWeaponState;
                if (slot != null && slot.Ammo <= 0)
                {
                    Tap(GameAction.Reload);
                }
                else if (lineOfSight && Time.unscaledTime >= nextAttackAt)
                {
                    TapAttack(.035f);
                    nextAttackAt = Time.unscaledTime + .24f;
                }
            }
            else if (distance < 1.7f && (!target.IsActing || target.AttackVisualPhase >= 2) && Time.unscaledTime >= nextAttackAt)
            {
                TapAttack(.055f);
                nextAttackAt = Time.unscaledTime + .27f;
            }

            // Keep basic finishers intact while pursuing their oath. Repeated RMB
            // buffers cancel the combo; this is a player-input policy, not a rule change.
            var finishingOath = player.ActiveWeaponState?.Item?.Oath == EquipmentOath.FinisherHunt &&
                player.ActiveWeaponState.Item.Awakened == false;
            var guarding = !finishingOath && !ranged && player.EquippedWeapon.Family == WeaponFamily.Longsword &&
                           distance < 1.35f && !attackHeld && Time.unscaledTime < nextAttackAt - .08f;
            StageValidationInput.SetHeld(GameAction.Guard, guarding);

            if (!finishingOath && Time.unscaledTime >= nextWeaponActionAt && distance < (ranged ? 7f : 2.4f))
            {
                Tap(GameAction.Guard);
                nextWeaponActionAt = Time.unscaledTime + (preferSkills ? 1.6f : 5.5f);
            }

            if (Time.unscaledTime >= nextDashAt && (escape.sqrMagnitude > .01f || distance > 5.5f || player.Health <= player.MaxHealth * .45f && distance < 2.5f))
            {
                var dashDirection = escape.sqrMagnitude > .01f ? escape : player.Health <= player.MaxHealth * .45f
                    ? -direction
                    : preferMobility && distance < 3.5f ? new Vector2(-direction.y,direction.x) : direction;
                if (Physics2D.CircleCast(player.transform.position,.35f,dashDirection,1.1f,1<<GameLayers.Ground)) dashDirection=-dashDirection;
                // Space prioritizes movement, not the cursor. Merely aiming
                // away while holding approach keys dashed the pilot INTO danger.
                SetMove(dashDirection);
                Tap(GameAction.Dash);
                nextDashAt = Time.unscaledTime + (preferMobility ? .95f : 1.15f);
            }
        }

        private void DriveRoomCleanup()
        {
            StageValidationInput.SetHeld(GameAction.Guard, false);
            var player = game.Player;

            RewardChest chest = game.ActiveChest;
            if (chest == null || chest.IsOpened)
            {
                var nearest = float.PositiveInfinity;
                foreach (var candidate in RuntimeSceneObjects.FindAll<RewardChest>())
                {
                    if (!candidate.CanOpen) continue;
                    var d = Vector2.SqrMagnitude(candidate.transform.position-player.transform.position);
                    if (d < nearest) { nearest = d; chest = candidate; }
                }
            }
            if (chest != null && !chest.IsOpened)
            {
                currentIntent = "open chest at " + Format(chest.transform.position);
                MoveTo(chest.transform.position, true);
                if (chest.PlayerInRange) Tap(GameAction.Interact);
                return;
            }

            if(ProbeAdversityFullBag())return;
            FieldLootPickup pickup=null;float pickupDistance=float.PositiveInfinity;
            foreach(var candidate in RuntimeSceneObjects.FindAll<FieldLootPickup>())
            {
                if(!game.Inventory.HasRoomFor(candidate.InventoryKind))continue;
                float distance=Vector2.SqrMagnitude(candidate.transform.position-player.transform.position);
                if(distance<pickupDistance){pickup=candidate;pickupDistance=distance;}
            }
            if (pickup != null)
            {
                currentIntent = "collect " + pickup.name + " at " + Format(pickup.transform.position);
                var distance = Vector2.Distance(player.transform.position, pickup.transform.position);
                MoveTo(pickup.transform.position, true);
                if (distance <= 1.25f) Tap(GameAction.Interact);
                return;
            }

            if (backendJourney ? DriveFieldFacility() : BeginEquipmentMaintenance()) return;

            if (game.IsStageResultReady)
            {
                currentIntent = "walk to stage seal at " + Format(game.StageExitPosition);
                MoveTo(game.StageExitPosition,true);
                if (game.IsNearStageExit)
                {
                    if (game.PendingStageRewards > 0) { leavingEquipment = true; ReleaseMovement(); }
                    else Tap(GameAction.Interact);
                }
                return;
            }

            var orb = FindNearest<ExperienceOrb>(player.transform.position);
            if (orb != null)
            {
                currentIntent = "collect XP at " + Format(orb.transform.position);
                MoveTo(orb.transform.position, false);
                return;
            }

            if (game.IsExitOpen)
            {
                int next=game.DungeonMap.NextUnclearedCombat(game.CurrentRoom);
                if(game.DungeonMap.IsProcedural)
                    foreach(int neighbor in game.DungeonMap.GetNeighbors(game.CurrentRoom))
                        if(!game.DungeonMap.RequiresCombat(neighbor)&&!game.DungeonMap.Regions[neighbor].Visited){next=neighbor;break;}
                if(next>=0&&next!=game.CurrentRoom)
                {
                    var destination=game.DungeonMap.GetEntryPosition(game.CurrentRoom,next);
                    currentIntent = "enter "+game.DungeonMap.GetRegionId(next)+" at " + Format(destination);
                    MoveTo(destination, false);
                    return;
                }
            }

            ReleaseMovement();
            currentIntent = "waiting for room state";
        }

        private void LateUpdate()
        {
            if (completionLogged || game == null || game.Player == null || game.DungeonMap==null || game.IsStageTransitioning || Time.unscaledTime < nextStatusAt) return;
            nextStatusAt = Time.unscaledTime + 4f;
            var body = game.Player.GetComponent<Rigidbody2D>();
            var velocity = body == null ? Vector2.zero : body.linearVelocity;
            if (game.IsExitOpen && !game.DungeonMap.IsProcedural && game.CurrentRoom < game.DungeonMap.RoomCount - 1)
            {
                var exit = FindNearest<RoomExit>(game.DungeonMap.GetRoomCenter(game.CurrentRoom));
                if (exit != null)
                {
                    var bounds = exit.GetComponent<BoxCollider2D>();
                    var distance = bounds.Distance(game.Player.GetComponent<Collider2D>());
                    Debug.Log("[Portal] State target=" + exit.TargetRoom + " unlocked=" + exit.IsUnlocked + " consumed=" + exit.IsConsumed +
                        " overlap=" + distance.isOverlapped + " distance=" + distance.distance + " bounds=" + bounds.bounds);
                }
            }
            Debug.Log("[Stage E2E] STATUS t=" + (Time.unscaledTime - startedAt).ToString("0.0") +
                      " stage=" + (game.CurrentStageIndex+1) + " room=" + (game.CurrentRoom + 1) + "/" + game.DungeonMap.RoomCount +
                      " alive=" + game.EnemiesAlive + " hp=" + game.Player.Health.ToString("0.0") +
                      " weapon=" + (game.Player.EquippedWeapon == null ? "none" : game.Player.EquippedWeapon.Id) +
                      " bossHp=" + (game.Boss == null ? "none" : game.Boss.Health.ToString("0.0")) +
                      " pos=" + Format(game.Player.transform.position) + " vel=" + Format(velocity) +
                      " scale=" + Time.timeScale.ToString("0.00") + " paused=" + game.IsPaused +
                      " intent=" + currentIntent);
        }

        private void MoveTo(Vector2 destination, bool interactWhenClose)
        {
            var player = game.Player;
            var delta = destination - (Vector2)player.transform.position;
            StageValidationInput.SetAim(delta.sqrMagnitude > .001f ? delta : Vector2.right);
            var stopDistance = interactWhenClose ? .45f : .22f;
            SetMove(delta.magnitude > stopDistance ? NavigateToward(destination) : Vector2.zero);
            UpdateStallTimer(player.transform.position);

            if (stalledFor > .8f && delta.magnitude > .5f && Time.unscaledTime >= nextDashAt)
            {
                StageValidationInput.SetAim(delta);
                Tap(GameAction.Dash);
                nextDashAt = Time.unscaledTime + 1.1f;
                stalledFor = 0f;
            }
        }

        private void SetMove(Vector2 direction)
        {
            StageValidationInput.SetHeld(GameAction.MoveLeft, direction.x < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveRight, direction.x > .1f);
            StageValidationInput.SetHeld(GameAction.MoveDown, direction.y < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveUp, direction.y > .1f);
        }

        private Vector2 NavigateToward(Vector2 destination)
        {
            if (game.DungeonMap.IsAuthored) return NavigateContinuous(destination);
            var center = game.DungeonMap.GetRoomCenter(game.CurrentRoom);
            var room = game.DungeonMap.GetRoom(game.CurrentRoom);
            var position = (Vector2)game.Player.transform.position;
            var localDestination = destination - center;
            if (!room.LocalBounds.Contains(localDestination) && game.IsExitOpen)
            {
                var direction = game.DungeonMap.GetExitDirection(game.CurrentRoom);
                var axis = direction == RoomTravelDirection.Right ? Vector2.right : direction == RoomTravelDirection.Left ? Vector2.left :
                    direction == RoomTravelDirection.Up ? Vector2.up : Vector2.down;
                var approach = center + room.Entrance(direction);
                if (Vector2.Dot(position - approach, axis) > -.5f)
                {
                    route.Clear();
                    // A nearly horizontal normalized vector drops the small Y
                    // correction when converted to digital keys. Center in the
                    // corridor first, then advance using axis dead zones.
                    var cross = new Vector2(-axis.y, axis.x);
                    var lateral = Vector2.Dot(destination - position, cross);
                    if (Mathf.Abs(lateral) > .18f) return cross * Mathf.Sign(lateral);
                    return axis;
                }
                localDestination = approach - center;
            }
            if (!RoomLayoutRules.IsClear(room, position - center, RoomLayoutRules.FootHalfSize))
            {
                // Physics uses a capsule; the authoring grid intentionally uses
                // a conservative box. After combat, step out of a legal rounded
                // corner using real capsule clearance before asking that grid.
                var bounds = game.Player.GetComponent<Collider2D>().bounds;
                var best = float.PositiveInfinity;
                var recovery = Vector2.zero;
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Mathf.PI / 4f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var candidate = position + direction * .5f;
                    if (!RoomLayoutRules.IsClear(room, candidate - center, RoomLayoutRules.FootHalfSize)) continue;
                    var hit = Physics2D.CapsuleCast(bounds.center, bounds.size, CapsuleDirection2D.Horizontal, 0,
                        direction, .5f, 1 << GameLayers.Ground);
                    if (hit.collider != null) continue;
                    var score = Vector2.SqrMagnitude(candidate - destination);
                    if (score >= best) continue;
                    best = score;
                    recovery = direction;
                }
                route.Clear();
                return recovery;
            }
            // Portals trigger before their visual center and loot can scatter close
            // to a wall. Navigate to the nearest reachable interaction approach.
            localDestination.x = Mathf.Clamp(localDestination.x, room.LocalBounds.xMin + .65f, room.LocalBounds.xMax - .65f);
            localDestination.y = Mathf.Clamp(localDestination.y, room.LocalBounds.yMin + .65f, room.LocalBounds.yMax - .5f);
            if (!RoomLayoutRules.IsClear(room, localDestination, RoomLayoutRules.FootHalfSize))
            {
                var found = false;
                for (var radius = .5f; radius <= 1.25f && !found; radius += .25f)
                for (var index = 0; index < 8 && !found; index++)
                {
                    var angle = index * Mathf.PI / 4f;
                    var candidate = localDestination + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!RoomLayoutRules.IsClear(room, candidate, RoomLayoutRules.FootHalfSize)) continue;
                    localDestination = candidate;
                    found = true;
                }
            }
            if (routeRoom != game.CurrentRoom || Vector2.Distance(routeDestination, localDestination) > .75f ||
                ((route.Count == 0 || stalledFor > .6f) && Time.unscaledTime >= nextRouteAt))
            {
                routeRoom = game.CurrentRoom;
                routeDestination = localDestination;
                nextRouteAt = Time.unscaledTime + .6f;
                route.Clear();
                var path = RoomLayoutRules.FindPath(room, position - center, localDestination);
                if (path != null) foreach (var point in path) route.Add(point + center);
                for (var i = 1; i + 1 < route.Count;)
                {
                    if (Vector2.Dot((route[i] - route[i - 1]).normalized, (route[i + 1] - route[i]).normalized) > .999f) route.RemoveAt(i);
                    else i++;
                }
            }
            if (route.Count == 0) return Vector2.zero;
            var velocity = game.Player.GetComponent<Rigidbody2D>().linearVelocity;
            if (Vector2.Distance(position, route[0]) < .18f)
            {
                if (velocity.sqrMagnitude > .04f) return Vector2.zero;
                route.RemoveAt(0);
                if (route.Count == 0) return Vector2.zero;
            }
            var delta = route[0] - position;
            if (delta.magnitude < .5f && Vector2.Dot(velocity, delta) > 0 && velocity.sqrMagnitude / 128f + .06f >= delta.magnitude)
                return Vector2.zero;
            // Digital keys need world-space axis dead zones, not an arbitrary
            // normalized segment which can cut across a blocked grid corner.
            return new Vector2(Mathf.Abs(delta.x) > .055f ? Mathf.Sign(delta.x) : 0f,
                Mathf.Abs(delta.y) > .055f ? Mathf.Sign(delta.y) : 0f);
        }

        private static Vector2 SteerAroundObstacles(Vector2 origin, Vector2 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude < .01f) return Vector2.zero;
            desiredDirection.Normalize();
            var hit = Physics2D.Raycast(origin, desiredDirection, .9f, 1 << GameLayers.Ground);
            if (hit.collider == null) return desiredDirection;

            // The generated rooms use axis-aligned solid modules. Follow the
            // obstacle edge until the direct route opens again.
            return new Vector2(desiredDirection.y, -desiredDirection.x).normalized;
        }

        private Vector2 NavigateContinuous(Vector2 destination)
        {
            var body = game.Player.GetComponent<Rigidbody2D>();
            var position = body.position;
            if (routeRoom != game.CurrentRoom || Vector2.Distance(routeDestination, destination) > .75f ||
                (route.Count == 0 || stalledFor > .6f) && Time.unscaledTime >= nextRouteAt)
            {
                routeRoom = game.CurrentRoom; routeDestination = destination; nextRouteAt = Time.unscaledTime + .6f;
                var seals = RuntimeSceneObjects.FindAll<EncounterBarrier>(); var blocked = new List<Bounds>();
                foreach (var seal in seals) if (seal.IsClosed) blocked.Add(seal.GetComponent<Collider2D>().bounds);
                var path = StageNavigation.FindPath(game.DungeonMap.Terrain, position, destination, blocked.ToArray());
                route.Clear(); if (path != null) route.AddRange(path);
                brakingAtWaypoint = false;
            }
            if (route.Count == 0) return Vector2.zero;
            var velocity = body.linearVelocity;
            var delta = route[0] - position;
            // FixedUpdate consumes the previous Update's digital input. Include
            // that response time, then hold the released keys until stopped.
            // Reversing a key while still braking caused an endless overshoot
            // around short waypoints at 20 Hz.
            if (brakingAtWaypoint)
            {
                if (velocity.sqrMagnitude > .04f) return Vector2.zero;
                brakingAtWaypoint = false;
            }
            if (delta.magnitude < .2f)
            {
                if (velocity.sqrMagnitude > .04f) { brakingAtWaypoint = true; return Vector2.zero; }
                route.RemoveAt(0); if (route.Count == 0) return Vector2.zero;
                delta = route[0] - position;
            }
            // Also cover a caller running after Player.Update (the isolated route
            // fixture), and acceleration while the previous key is still applied.
            var responseTime = 2f * Mathf.Min(Time.unscaledDeltaTime, .1f) + Time.fixedDeltaTime;
            var speedAtRelease = Mathf.Min(game.Player.Stats.MoveSpeed, velocity.magnitude + 44f * responseTime);
            var stoppingDistance = speedAtRelease * speedAtRelease / 128f + velocity.magnitude * responseTime + 22f * responseTime * responseTime + .03f;
            if (Vector2.Dot(velocity, delta) > 0 && stoppingDistance >= delta.magnitude)
            { brakingAtWaypoint = true; return Vector2.zero; }
            return new Vector2(Mathf.Abs(delta.x) > .055f ? Mathf.Sign(delta.x) : 0, Mathf.Abs(delta.y) > .055f ? Mathf.Sign(delta.y) : 0);
        }

        private void ReleaseMovement()
        {
            SetMove(Vector2.zero);
            StageValidationInput.SetHeld(GameAction.Guard, false);
        }

        private bool Tap(GameAction action)
        {
            if (Time.unscaledTime < nextInteractAt && action == GameAction.Interact) return false;
            StageValidationInput.SetHeld(action, true);
            StageValidationInput.SetHeld(action, false);
            if (action == GameAction.Interact) nextInteractAt = Time.unscaledTime + .22f;
            return true;
        }

        private void TapAttack(float holdSeconds)
        {
            StageValidationInput.SetHeld(GameAction.Attack, true);
            attackHeld = true;
            attackReleaseAt = Time.unscaledTime + holdSeconds;
        }

        private void ReleaseTimedInputs()
        {
            if (attackHeld && Time.unscaledTime >= attackReleaseAt)
            {
                StageValidationInput.SetHeld(GameAction.Attack, false);
                attackHeld = false;
            }
        }

        private void UpdateStallTimer(Vector2 currentPosition)
        {
            if (Vector2.Distance(currentPosition, lastPosition) < .015f) stalledFor += Time.unscaledDeltaTime;
            else stalledFor = 0f;
            lastPosition = currentPosition;
        }

        private static EnemyController FindNearestEnemy(Vector2 origin)
        {
            EnemyController nearest=null; var best=float.PositiveInfinity;
            foreach(var enemy in RuntimeSceneObjects.FindAll<EnemyController>())
            {
                if(enemy.IsDead)continue;
                var distance=Vector2.SqrMagnitude((Vector2)enemy.transform.position-origin);
                if(distance<best){best=distance;nearest=enemy;}
            }
            return nearest;
        }

        private void SelectCombatWeapon()
        {
            var best=-1;var score=float.NegativeInfinity;
            for(var i=0;i<game.Inventory.QuickSlots.Count;i++)
            {
                var item=game.Inventory.QuickSlots[i].Item;
                if(item==null)continue;
                var rank=OwnedWeaponRank(item);
                if(rank>score){score=rank;best=i;}
            }
            if(best>=0 && best!=game.Inventory.ActiveSlotIndex) Tap(best==0?GameAction.Weapon1:best==1?GameAction.Weapon2:GameAction.Weapon3);
        }

        private float WeaponRank(WeaponDefinition weapon)
        {
            if (weapon == null) return float.NegativeInfinity;
            if (preferMobility)
                return (weapon.Family==WeaponFamily.Dagger||weapon.Family==WeaponFamily.Katana?1400:weapon.AttackType==WeaponAttackType.Melee?700:0)+(int)weapon.Rarity*100+weapon.PowerScore;
            if (preferSkills)
            {
                var manaAction = weapon.Family == WeaponFamily.Mace || weapon.Family == WeaponFamily.Crossbow || weapon.Family == WeaponFamily.Cannon;
                return (manaAction ? 1200 : 0) + (int)weapon.Rarity * 100 + weapon.PowerScore;
            }
            return ((weapon.AttackType == WeaponAttackType.Ranged) == preferRanged ? 1000 : 0) +
                   (int)weapon.Rarity * 100 + weapon.PowerScore;
        }

        private float OwnedWeaponRank(OwnedWeapon item)
        {
            if(item==null)return float.NegativeInfinity;
            // A player who accepts an oath keeps using that copy until the
            // condition and its awakened attack have actually been experienced.
            return WeaponRank(item.Definition)+(backendJourney&&FitsJourneyBuild(item.Definition)&&item.Oath!=EquipmentOath.None&&
                (!item.Awakened||!awakenedCopiesUsed.Contains(item.Identity))?2200:0);
        }

        private bool FitsJourneyBuild(WeaponDefinition weapon)
        {
            if (weapon == null) return false;
            if (preferMobility) return weapon.Family == WeaponFamily.Dagger || weapon.Family == WeaponFamily.Katana;
            if (preferSkills) return weapon.Family == WeaponFamily.Mace || weapon.Family == WeaponFamily.Crossbow || weapon.Family == WeaponFamily.Cannon;
            return (weapon.AttackType == WeaponAttackType.Ranged) == preferRanged;
        }

        private bool EquipStoredUpgrade()
        {
            var bestRank = float.NegativeInfinity;
            var weakestRank = float.PositiveInfinity;
            var replaceSlot = 0;
            for (var i = 0; i < game.Inventory.QuickSlots.Count; i++)
            {
                var rank = OwnedWeaponRank(game.Inventory.QuickSlots[i].Item);
                bestRank = Mathf.Max(bestRank, rank);
                if (rank < weakestRank) { weakestRank = rank; replaceSlot = i; }
            }
            var cell = -1;
            for (var i = 0; i < game.Inventory.UnlockedCapacity; i++)
            {
                var entry = game.Inventory.GetCell(i);
                if (entry == null || entry.Kind != InventoryEntryKind.Weapon) continue;
                var rank = OwnedWeaponRank(game.Inventory.OwnedWeapons[entry.ItemIndex]);
                if (rank <= bestRank) continue;
                bestRank = rank; cell = i;
            }
            if (cell < 0)
            {
                if (!game.IsInventoryOpen) return false;
                StageValidationInput.ReleaseAll();
                Tap(GameAction.Inventory);
                return true;
            }
            StageValidationInput.ReleaseAll();
            attackHeld = false;
            currentIntent = "equip stored weapon through inventory keys";
            if (!game.IsInventoryOpen) { Tap(GameAction.Inventory); return true; }
            var focus = game.InventoryFocusCell;
            if (game.InventoryFocusQuickSlot >= 0) { Tap(GameAction.MoveDown); return true; }
            var dx = InventorySystem.BagColumn(cell) - InventorySystem.BagColumn(focus);
            var dy = InventorySystem.BagRow(cell) - InventorySystem.BagRow(focus);
            if (dx != 0) Tap(dx < 0 ? GameAction.MoveLeft : GameAction.MoveRight);
            else if (dy != 0) Tap(dy < 0 ? GameAction.MoveUp : GameAction.MoveDown);
            else
            {
                Debug.Log("[Stage E2E] Inventory input equip cell=" + cell + " slot=" + (replaceSlot + 1));
                Tap(replaceSlot == 0 ? GameAction.Weapon1 : replaceSlot == 1 ? GameAction.Weapon2 : GameAction.Weapon3);
            }
            return true;
        }

        private Vector2 VisibleWarningEscape(Vector2 position)
        {
            // Read the warnings visible to the player, including an enemy other
            // than the current attack target. Do not inspect future actions.
            var camera = Camera.main;
            if (camera == null) return Vector2.zero;
            var closestThreat = float.PositiveInfinity;
            var escape = Vector2.zero;
            foreach (var enemy in RuntimeSceneObjects.FindAll<EnemyController>())
            {
                if (enemy.IsDead || !enemy.IsActing || enemy.AttackVisualPhase >= 2) continue;
                var viewport = camera.WorldToViewportPoint(enemy.transform.position);
                if (viewport.z < 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
                var delta = (Vector2)enemy.transform.position - position;
                var distance = delta.magnitude;
                var reach = enemy.IsBoss ? 6f : enemy.Type == EnemyType.Shooter ? 7f :
                    enemy.Type == EnemyType.Charger || enemy.Type == EnemyType.Leaper ? 6f :
                    enemy.Type == EnemyType.Grunt ? 2.6f : 0f;
                if (reach <= 0f || distance >= reach || distance < .001f ||
                    !StageNavigation.HasCombatLane(position, enemy.transform.position, .2f)) continue;
                var threat = distance / reach;
                if (threat >= closestThreat) continue;
                var direction = delta / distance;
                var dodge = enemy.Type == EnemyType.Grunt || enemy.IsBoss && distance < 3.4f
                    ? -direction : new Vector2(-direction.y, direction.x);
                if (Physics2D.CircleCast(position, .35f, dodge, 1.1f, 1 << GameLayers.Ground)) dodge = -dodge;
                if (Physics2D.CircleCast(position, .35f, dodge, .65f, 1 << GameLayers.Ground)) continue;
                closestThreat = threat;
                escape = dodge;
            }
            return escape;
        }

        private Vector2 HazardEscape(Vector2 position)
        {
            var hazards=RuntimeSceneObjects.FindAll<ArenaHazard>();
            var unsafeHere=false;
            foreach(var hazard in hazards)if(hazard.Contains(position)){unsafeHere=true;break;}
            if(!unsafeHere)return Vector2.zero;
            var best=float.PositiveInfinity;var direction=Vector2.zero;
            for(var ring=1;ring<=5;ring++)for(var i=0;i<16;i++)
            {
                var angle=i*Mathf.PI/8;var candidateDirection=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                var candidate=position+candidateDirection*ring*.7f;var safe=true;
                foreach(var hazard in hazards)if(hazard.Contains(candidate)){safe=false;break;}
                if(!safe || Physics2D.CircleCast(position,.35f,candidateDirection,ring*.7f,1<<GameLayers.Ground))continue;
                var distance=Vector2.SqrMagnitude(candidate-position);
                if(distance>=best)continue;
                best=distance;direction=candidateDirection;
            }
            return direction;
        }

        private Vector2 ProjectileEscape(Vector2 position)
        {
            foreach(var projectile in RuntimeSceneObjects.FindAll<EnemyProjectile>())
            {
                var delta=position-(Vector2)projectile.transform.position;
                var forward=Vector2.Dot(delta,projectile.TravelDirection);
                var side=new Vector2(-projectile.TravelDirection.y,projectile.TravelDirection.x);
                var lateral=Vector2.Dot(delta,side);
                if(forward<0 || forward>2.8f || Mathf.Abs(lateral)>.65f)continue;
                var dodge=side*(lateral>=0?1:-1);
                if(Physics2D.CircleCast(position,.35f,dodge,1.3f,1<<GameLayers.Ground))dodge=-dodge;
                if(!Physics2D.CircleCast(position,.35f,dodge,.65f,1<<GameLayers.Ground))return dodge;
            }
            return Vector2.zero;
        }

        private int SelectUpgrade()
        {
            var best=0;var score=float.NegativeInfinity;
            for(var i=0;i<game.CurrentAccessoryChoices.Length;i++)
            {
                var item=game.CurrentAccessoryChoices[i];
                var value=AccessoryRank(item);
                if(value>score){score=value;best=i;}
            }
            Debug.Log("[Stage E2E] CHOICE stage=" + (game.CurrentStageIndex+1) + " id=" + game.CurrentAccessoryChoices[best].Id + " build=" + (preferMobility ? "mobility" : preferSkills ? "skill" : preferRanged ? "ranged" : "melee"));
            return best;
        }

        private float AccessoryRank(AccessoryDefinition item)
        {
            float value = (int)item.Rarity * 3;
            if (preferMobility && (item.Id=="rush_spur"||item.Id=="storm_feather"||item.Id=="trail_spindle")) value += 50;
            else if (preferSkills && (item.SetType == ItemSetType.Arcanist || item.Id == "arcane_lens")) value += 45;
            else if (preferRanged && (item.SetType == ItemSetType.Storm || item.Id == "steady_locket" || item.Id == "powder_satchel")) value += 45;
            else if (!preferSkills && !preferRanged && (item.SetType == ItemSetType.BloodMoon || item.SetType == ItemSetType.Guardian)) value += 40;
            if (item.Id == "vampire_fang") value += 16;
            if (item.Id == "ruby_heart" && game.Player.Health < game.Player.MaxHealth * .5f) value += 45;
            int copies = 0;
            foreach (var owned in game.Inventory.OwnedAccessories) if (owned.Definition.Id == item.Id) copies++;
            if(backendJourney&&copies==0)
                foreach(var recipe in game.ExchangeRecipes)
                    for(int i=0;i<recipe.InputIds.Count;i++)if(recipe.InputIds[i]==item.Id)
                        foreach(var owned in game.Inventory.OwnedAccessories)if(owned.Definition.Id==recipe.InputIds[1-i]){value+=80;break;}
            return value - (copies > 0 ? 12 : 0) - (copies >= 3 ? 60 : 0);
        }

        // Read inventory state to choose, then use only ordinary input. No grant,
        // removal, upgrade, selection or material API is called by this pilot.
        private bool BeginEquipmentMaintenance()
        {
            int roomKey = game.CurrentStageIndex * 100 + game.CurrentRoom;
            if (maintenanceRoom == roomKey || game.EnemiesAlive != 0 || game.IsEncounterTransitioning) return false;
            maintenanceRoom = roomKey;
            OwnedWeapon spare = null;
            foreach (var item in game.Inventory.OwnedWeapons)
                if (item.UpgradeLevel == 0 && (spare == null || WeaponRank(item.Weapon) < WeaponRank(spare.Weapon))) spare = item;
            var upgrade = FindMaintenanceUpgrade();
            if (spare == null && upgrade == null) return false;
            StageValidationInput.ReleaseAll(); attackHeld = false;
            maintenanceIdentity = spare?.Identity ?? upgrade.Identity;
            maintenancePhase = spare == null ? 4 : 1;
            maintenanceActive = true;
            maintenanceDeadline = Time.unscaledTime + 20;
            currentIntent = "normal inventory maintenance";
            Tap(GameAction.Inventory);
            return true;
        }

        private OwnedEquipment FindMaintenanceUpgrade()
        {
            OwnedEquipment best = null; float rank = float.NegativeInfinity;
            foreach (var slot in game.Inventory.QuickSlots)
            {
                var item = slot.Item;
                if (item == null || item.UpgradeLevel >= 3 || item.UpgradeCost > game.Inventory.UpgradeMaterials) continue;
                float score = WeaponRank(item.Weapon) + 2000;
                if (score > rank) { rank = score; best = item; }
            }
            foreach (var item in game.Inventory.OwnedAccessories)
            {
                if (item.UpgradeLevel >= 3 || item.UpgradeCost > game.Inventory.UpgradeMaterials) continue;
                float score = AccessoryRank(item.Definition);
                if (score > rank) { rank = score; best = item; }
            }
            return best;
        }

        private bool FocusOwnedWithKeys(long identity)
        {
            if (game.SelectedEquipment?.Identity == identity) return true;
            for (int slot = 0; slot < game.Inventory.QuickSlots.Count; slot++)
            {
                if (game.Inventory.QuickSlots[slot].Item?.Identity != identity) continue;
                if (game.InventoryFocusQuickSlot < 0) Tap(GameAction.MoveUp);
                else Tap(slot < game.InventoryFocusQuickSlot ? GameAction.MoveLeft : GameAction.MoveRight);
                return false;
            }
            if (game.InventoryFocusQuickSlot >= 0) { Tap(GameAction.MoveDown); return false; }
            for (int cell = 0; cell < game.Inventory.UnlockedCapacity; cell++)
            {
                if (game.Inventory.GetOwnedCell(cell)?.Identity != identity) continue;
                int dx = InventorySystem.BagColumn(cell) - InventorySystem.BagColumn(game.InventoryFocusCell);
                int dy = InventorySystem.BagRow(cell) - InventorySystem.BagRow(game.InventoryFocusCell);
                if (dx != 0) Tap(dx < 0 ? GameAction.MoveLeft : GameAction.MoveRight);
                else if (dy != 0) Tap(dy < 0 ? GameAction.MoveUp : GameAction.MoveDown);
                else Tap(GameAction.MoveUp); // Re-enter a cell after its previous copy was removed.
                return false;
            }
            return false;
        }

        private void DriveEquipmentMaintenance()
        {
            StageValidationInput.ReleaseAll(); attackHeld = false;
            if (Time.unscaledTime > maintenanceDeadline) { Finish(false, "inventory keyboard maintenance stalled phase=" + maintenancePhase); return; }
            if (!game.IsInventoryOpen) { Tap(GameAction.Inventory); return; }
            if (!game.EquipmentOperationReady) return;
            var item = game.Inventory.FindOwned(maintenanceIdentity);
            switch (maintenancePhase)
            {
                case 1:
                    if (!FocusOwnedWithKeys(maintenanceIdentity)) return;
                    maintenanceMaterials = game.Inventory.UpgradeMaterials; maintenanceDelta = item.DisassemblyYield;
                    Tap(GameAction.Dismantle); maintenancePhase = 2; break;
                case 2:
                    if (!game.HasDisassemblyConfirmation) { Finish(false, "disassembly request did not show confirmation"); return; }
                    // Loot pickup can still be inside the pilot's interaction interval.
                    // Stay on confirmation until an E press was actually issued.
                    if (!Tap(GameAction.Interact)) return;
                    CaptureGrowth("disassembly-request"); maintenancePhase = 3; break;
                case 3:
                    if (item != null || game.Inventory.UpgradeMaterials != maintenanceMaterials + maintenanceDelta) { Finish(false, "disassembly result/material mismatch identity=" + maintenanceIdentity + " remains=" + (item != null) + " expected=" + (maintenanceMaterials + maintenanceDelta) + " actual=" + game.Inventory.UpgradeMaterials); return; }
                    disassemblyCount++; if (game.CurrentStageIndex == 0) firstStageDisassemblies++;
                    Debug.Log("[Stage E2E] DISASSEMBLED stage=" + (game.CurrentStageIndex+1) + " identity=" + maintenanceIdentity + " materials=" + maintenanceMaterials + "->" + game.Inventory.UpgradeMaterials);
                    CaptureGrowth("disassembly-complete");
                    item = FindMaintenanceUpgrade(); maintenanceIdentity = item?.Identity ?? 0; maintenancePhase = item == null ? 6 : 4; break;
                case 4:
                    if (!FocusOwnedWithKeys(maintenanceIdentity)) return;
                    maintenanceLevel = item.UpgradeLevel; maintenanceMaterials = game.Inventory.UpgradeMaterials; maintenanceDelta = item.UpgradeCost;
                    Tap(GameAction.Reload); maintenancePhase = 5; break;
                case 5:
                    if (item == null || item.UpgradeLevel != maintenanceLevel + 1 || game.Inventory.UpgradeMaterials != maintenanceMaterials - maintenanceDelta) { Finish(false, "upgrade result/material mismatch"); return; }
                    upgradeCount++; if (game.CurrentStageIndex == 0) firstStageUpgrades++;
                    Debug.Log("[Stage E2E] UPGRADED stage=" + (game.CurrentStageIndex+1) + " identity=" + maintenanceIdentity + " item=" + item.DisplayName + " materials=" + maintenanceMaterials + "->" + game.Inventory.UpgradeMaterials);
                    CaptureGrowth("upgrade-complete"); maintenancePhase = 6; break;
                default:
                    Tap(GameAction.Inventory); maintenanceActive = false; break;
            }
        }

        private void CaptureGrowth(string label)
        {
            if (!captureScreenshots || string.IsNullOrEmpty(captureDirectory) || Application.isBatchMode) return;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureDirectory, "growth-stage" + (game.CurrentStageIndex+1) + "-room" + (game.CurrentRoom+1) + "-" + label + ".png"));
        }

        private static T FindNearest<T>(Vector2 origin, float maxDistance = float.PositiveInfinity) where T : Component
        {
            var candidates = Object.FindObjectsByType<T>();
            T nearest = null;
            var best = maxDistance * maxDistance;
            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
                var distance = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = candidate;
            }
            return nearest;
        }

        private static string Format(Vector2 value)
        {
            return "(" + value.x.ToString("0.00") + "," + value.y.ToString("0.00") + ")";
        }

        private void CaptureValidationFrame()
        {
            if (!captureScreenshots || string.IsNullOrWhiteSpace(captureDirectory) || captureIndex >= 3 ||
                Time.unscaledTime < nextCaptureAt || game == null || game.Player == null ||
                Time.unscaledTime < game.StageIntroUntil)
                return;

            var motion = game.Player.MotionState;
            var validPose = captureIndex == 0
                ? motion == PlayerMotionState.Run || motion == PlayerMotionState.Brake
                : captureIndex == 1
                    ? motion == PlayerMotionState.Attack
                    : motion == PlayerMotionState.Attack && game.Player.ActiveComboAnimationStep >= 1;
            if (!validPose) return;

            captureIndex++;
            var path = System.IO.Path.Combine(captureDirectory, "animation-validation-" + captureIndex.ToString("00") + ".png");
            var captureType = System.Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var captureMethod = captureType == null ? null : captureType.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (captureMethod == null)
            {
                Debug.LogWarning("[Stage E2E] ScreenCapture module unavailable; graphical capture skipped.");
                captureIndex = 3;
                return;
            }
            captureMethod.Invoke(null, new object[] { path, 1 });
            Debug.Log("[Stage E2E] CAPTURE " + path);
            nextCaptureAt = Time.unscaledTime + .45f;
        }

        private static string GetCommandLineValue(string argumentName)
        {
            var arguments = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], argumentName, System.StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return null;
        }

        private void Finish(bool passed, string reason)
        {
            passed=ValidateAdversityCompletion(passed,reason);
            completionLogged = true;
            ReleaseMovement();
            StageValidationInput.ReleaseAll();
            var elapsed = Time.unscaledTime - startedAt;
            if(backendJourney)WriteJourneyEvidence(passed,reason);
            else Debug.Log("[Stage E2E] LEGACY FIXTURE upgrades=" + upgradeCount + " disassemblies=" + disassemblyCount);
            Debug.Log((passed ? "[Stage E2E] PASS" : "[Stage E2E] FAIL") +
                      " — " + reason + ", elapsed=" + elapsed.ToString("0.0") + "s, rooms=" +
                      (game == null ? 0 : game.CurrentRoom + 1) + ", kills=" +
                      (game == null || game.Stats == null ? 0 : game.Stats.EnemiesDefeated));
            if (!backendJourney&&passed && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--validation-lifecycle") >= 0)
            {
                game.gameObject.AddComponent<SaveLifecycleValidation>().Initialize(game, false);
                return;
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--validation-quit") >= 0)
                Application.Quit(passed ? 0 : 2);
        }
    }
}
