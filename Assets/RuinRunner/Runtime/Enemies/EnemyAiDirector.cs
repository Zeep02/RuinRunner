using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    public enum SquadRole { Frontline, Skirmisher, Flanker, Backline, Support, Rusher, Anchor }
    public enum PacingPhase { Build, Peak, Relax }
    public enum AiTag { Melee, Gapcloser, Aimed, Zone, Retreat, Support, Free }

    /// <summary>The layer above individual enemies: what they know about the player (blackboard), who may attack and from where
    /// (squad), and how hard the room presses right now (pacing). One enemy's brain only ever asks; this decides.</summary>
    public sealed class EnemyAiDirector : MonoBehaviour
    {
        // Pressure gained per share of maximum health lost, and lost per second.
        const float StressPerHealthLost = 2.2f, StressDecay = .06f, RelaxAt = .65f, RelaxUntil = .3f, MinRelaxSeconds = 3f;
        const float PeakAfterUnhurtSeconds = 7f, PeakMinimumHealth = .5f, SlotRefreshSeconds = .4f, RangeSmoothing = 1.2f;

        public static EnemyAiDirector Current { get; private set; }
        public static readonly bool Baseline = HasArgument("--ai-baseline");
        static readonly bool ShowOverlay = HasArgument("--ai-debug");
        static StreamWriter log;

        public readonly List<EnemyController> Members = new List<EnemyController>();
        public PlayerController Player { get; private set; }
        public float HealthRatio { get; private set; } = 1;
        public Vector2 PlayerVelocity { get; private set; }
        public float PlayerSpeed => PlayerVelocity.magnitude;
        public float SecondsSinceDash { get; private set; } = 99;
        public float SecondsSinceHurt { get; private set; } = 99;
        // How far the player has been keeping from the nearest enemy lately.
        public float KeptDistance { get; private set; } = 4;
        public float Stress { get; private set; }
        public PacingPhase Phase { get; private set; }
        public int MeleeTokensInUse => meleeHolders.Count;
        public int RangedTokensInUse => rangedHolders.Count;
        public int MeleeTokenLimit => Phase == PacingPhase.Peak ? 3 : Phase == PacingPhase.Relax ? 1 : 2;
        public int RangedTokenLimit => Phase == PacingPhase.Peak ? 3 : Phase == PacingPhase.Relax ? 1 : 2;
        public float CooldownScale => Baseline ? 1 : Phase == PacingPhase.Peak ? .82f : Phase == PacingPhase.Relax ? 1.2f : 1;
        public bool PlayerDashedRecently => SecondsSinceDash < 1.2f;
        public bool PlayerStandingStill => PlayerSpeed < .6f;
        public bool PlayerKeepsAway => KeptDistance > 5.5f;

        readonly HashSet<EnemyController> meleeHolders = new HashSet<EnemyController>(), rangedHolders = new HashSet<EnemyController>();
        readonly Dictionary<EnemyController, Vector2> slots = new Dictionary<EnemyController, Vector2>();
        Vector2 lastPlayerPoint;
        float lastHealth = -1, nextSlotsAt, relaxedAt;
        bool wasDashing;

        public static EnemyAiDirector For(PlayerController player)
        {
            if (Current == null)
            {
                Current = new GameObject("EnemyAiDirector").AddComponent<EnemyAiDirector>();
                if (ShowOverlay)
                    Current.gameObject.AddComponent<AiDebugOverlay>();
                OpenLog();
            }
            if (Current.Player != player)
                Current.Bind(player);
            return Current;
        }

        void Bind(PlayerController player)
        {
            Player = player;
            Members.Clear();
            meleeHolders.Clear();
            rangedHolders.Clear();
            slots.Clear();
            Stress = 0;
            Phase = PacingPhase.Build;
            lastHealth = -1;
            SecondsSinceHurt = 99;
            SecondsSinceDash = 99;
            lastPlayerPoint = player == null ? Vector2.zero : (Vector2)player.transform.position;
        }

        public void Join(EnemyController enemy)
        {
            if (!Members.Contains(enemy))
                Members.Add(enemy);
        }
        public void Leave(EnemyController enemy)
        {
            Members.Remove(enemy);
            slots.Remove(enemy);
            ReleaseTokens(enemy);
        }

        /// <summary>The squad's answer to "may I attack now". Moves that are not attacks never need one.</summary>
        public bool TryTakeToken(EnemyController enemy, AiTag tag)
        {
            if (Baseline)
                return true;
            var holders = tag == AiTag.Melee || tag == AiTag.Gapcloser ? meleeHolders : tag == AiTag.Aimed || tag == AiTag.Zone ? rangedHolders : null;
            if (holders == null)
                return true;
            var limit = holders == meleeHolders ? MeleeTokenLimit : RangedTokenLimit;
            if (holders.Count >= limit)
                return false;
            holders.Add(enemy);
            return true;
        }
        public void ReleaseTokens(EnemyController enemy)
        {
            meleeHolders.Remove(enemy);
            rangedHolders.Remove(enemy);
        }

        /// <summary>Where the squad wants this enemy to stand: spread around the player by role, never in one clump.</summary>
        public Vector2 SlotFor(EnemyController enemy)
        {
            if (Baseline || Player == null)
                return Player == null ? (Vector2)enemy.transform.position : (Vector2)Player.transform.position;
            return slots.TryGetValue(enemy, out var slot) ? slot : (Vector2)Player.transform.position;
        }

        void Update()
        {
            if (Player == null)
            {
                Members.Clear();
                return;
            }
            if (Time.deltaTime <= 0f)
                return;
            ObservePlayer();
            UpdatePacing();
            if (Time.time >= nextSlotsAt && !Baseline)
            {
                nextSlotsAt = Time.time + SlotRefreshSeconds;
                AssignSlots();
            }
        }

        void ObservePlayer()
        {
            var point = (Vector2)Player.transform.position;
            PlayerVelocity = Vector2.Lerp(PlayerVelocity, (point - lastPlayerPoint) / Time.deltaTime, 8f * Time.deltaTime);
            lastPlayerPoint = point;
            SecondsSinceDash = Player.IsDashing ? 0 : SecondsSinceDash + Time.deltaTime;
            if (Player.IsDashing && !wasDashing)
                Record("player-dash", null, "", 0, "");
            wasDashing = Player.IsDashing;
            HealthRatio = Player.MaxHealth <= 0 ? 1 : Player.Health / Player.MaxHealth;
            if (lastHealth >= 0 && Player.Health < lastHealth - .01f && Player.MaxHealth > 0)
            {
                Stress = Mathf.Clamp01(Stress + (lastHealth - Player.Health) / Player.MaxHealth * StressPerHealthLost);
                SecondsSinceHurt = 0;
            }
            else
                SecondsSinceHurt += Time.deltaTime;
            lastHealth = Player.Health;
            var nearest = float.MaxValue;
            foreach (var member in Members)
                if (member != null)
                    nearest = Mathf.Min(nearest, Vector2.Distance(member.transform.position, point));
            if (nearest < float.MaxValue)
                KeptDistance = Mathf.Lerp(KeptDistance, nearest, RangeSmoothing * Time.deltaTime);
        }

        void UpdatePacing()
        {
            Stress = Mathf.Max(0, Stress - StressDecay * Time.deltaTime);
            var next = Phase;
            if (Baseline)
                next = PacingPhase.Build;
            else if (Phase == PacingPhase.Relax)
            {
                if (Stress <= RelaxUntil && Time.time - relaxedAt >= MinRelaxSeconds)
                    next = PacingPhase.Build;
            }
            else if (Stress >= RelaxAt)
            {
                next = PacingPhase.Relax;
                relaxedAt = Time.time;
            }
            else if (SecondsSinceHurt >= PeakAfterUnhurtSeconds && HealthRatio >= PeakMinimumHealth)
                next = PacingPhase.Peak;
            else
                next = PacingPhase.Build;
            if (next != Phase)
            {
                Phase = next;
                Record("pacing", null, next.ToString(), Stress, "");
            }
        }

        void AssignSlots()
        {
            var alive = 0;
            var centroid = Vector2.zero;
            foreach (var member in Members)
                if (member != null)
                {
                    centroid += (Vector2)member.transform.position;
                    alive++;
                }
            if (alive == 0)
                return;
            var player = (Vector2)Player.transform.position;
            centroid /= alive;
            var awayFromPlayer = centroid - player;
            var front = awayFromPlayer.sqrMagnitude > .01f ? Mathf.Atan2(awayFromPlayer.y, awayFromPlayer.x) * Mathf.Rad2Deg : 90f;
            var aim = Player.AimDirection.sqrMagnitude > .01f ? Player.AimDirection : Vector2.right;
            var behind = Mathf.Atan2(-aim.y, -aim.x) * Mathf.Rad2Deg;
            foreach (SquadRole role in Enum.GetValues(typeof(SquadRole)))
            {
                var group = new List<EnemyController>();
                foreach (var member in Members)
                    if (member != null && member.Role == role)
                        group.Add(member);
                for (var i = 0; i < group.Count; i++)
                {
                    float angle, arc;
                    switch (role)
                    {
                        // Frontline meets the player head on; flankers go for the back the weapon is not pointing at.
                        case SquadRole.Flanker:
                            angle = behind;
                            arc = 70f;
                            break;
                        case SquadRole.Skirmisher:
                            angle = front + 90f;
                            arc = 200f;
                            break;
                        case SquadRole.Backline:
                            angle = front;
                            arc = 80f;
                            break;
                        case SquadRole.Support:
                            angle = front;
                            arc = 50f;
                            break;
                        default:
                            angle = front;
                            arc = 100f;
                            break;
                    }
                    var spread = group.Count > 1 ? arc * (i / (group.Count - 1f) - .5f) : 0f;
                    slots[group[i]] = Fit(player, angle + spread, group[i].DesiredRange);
                }
            }
        }

        // A slot inside a wall is no use: pull it in toward the player until it is on open floor.
        static Vector2 Fit(Vector2 player, float angle, float radius)
        {
            var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var wall = Physics2D.Raycast(player, direction, radius, 1 << GameLayers.Ground);
            return player + direction * (wall.collider == null ? radius : Mathf.Max(.6f, wall.distance - .5f));
        }

        static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        static void OpenLog()
        {
            if (log != null)
                return;
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "--ai-log");
            if (index < 0 || index + 1 >= arguments.Length)
                return;
            try
            {
                log = new StreamWriter(arguments[index + 1], false) { AutoFlush = true };
                log.WriteLine("time,event,enemy,role,choice,score,alternatives,phase,stress,player_health,kept_distance,melee_tokens,ranged_tokens,alive");
            }
            catch (IOException e)
            {
                Debug.LogWarning("AI log unavailable: " + e.Message);
            }
        }

        /// <summary>One line per decision, so a run can be charted afterwards.</summary>
        public void Record(string kind, EnemyController enemy, string choice, float score, string alternatives)
        {
            if (log == null)
                return;
            log.WriteLine(string.Join(",", Time.time.ToString("0.00"), kind, enemy == null ? "" : enemy.Type.ToString(), enemy == null ? "" : enemy.Role.ToString(), choice, score.ToString("0.00"),
                alternatives, Phase, Stress.ToString("0.00"), HealthRatio.ToString("0.00"), KeptDistance.ToString("0.0"), MeleeTokensInUse, RangedTokensInUse, Members.Count));
        }
    }
}
