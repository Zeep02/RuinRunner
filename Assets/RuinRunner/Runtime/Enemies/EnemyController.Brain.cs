using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Each enemy's own decisions: a utility score for every move it knows, against the distance and what the player has been doing.
    /// Standing where the squad asked and waiting for an attack token are the director's half of the bargain.</summary>
    public sealed partial class EnemyController
    {
        sealed class AiMove
        {
            public string Name;
            public AiTag Tag;
            public float Min, Max, Ideal, Weight;
            public Func<Vector2, IEnumerator> Run;
            public Func<bool> Allowed;
            public float LastUsed = -99f;
        }

        const float FreshAfterSeconds = 6f, RepeatPenalty = .8f, SameRoomSpacing = 1.1f, Lead = .25f;
        EnemyAiDirector director;
        List<AiMove> moves;
        AiMove lastMove;
        float moveScale = 1f, nextChoiceLog;
        public SquadRole Role { get; private set; }
        public float DesiredRange { get; private set; } = 2f;
        // What the debug view shows: the move taken and how the others scored.
        public string LastDecision { get; private set; } = "";

        void EnsureBrain()
        {
            if (director != null)
                return;
            director = EnemyAiDirector.For(player);
            director.Join(this);
            BuildMoves();
        }
        void LeaveSquad()
        {
            if (director != null)
            {
                director.Leave(this);
                director = null;
            }
        }

        void Add(string name, AiTag tag, float min, float max, float ideal, float weight, Func<Vector2, IEnumerator> run, Func<bool> allowed = null) =>
            moves.Add(new AiMove { Name = name, Tag = tag, Min = min, Max = max, Ideal = ideal, Weight = weight, Run = run, Allowed = allowed });
        void Stance(SquadRole role, float range, float scale = 1f)
        {
            Role = role;
            DesiredRange = range;
            moveScale = scale;
        }

        void BuildMoves()
        {
            moves = new List<AiMove>();
            const float Any = 99f;
            switch (Type)
            {
                case EnemyType.Grunt:
                    Stance(SquadRole.Frontline, 1.9f);
                    Add("lunge", AiTag.Melee, 0, 2.6f, 1.8f, 1, GruntLunge);
                    Add("rock", AiTag.Zone, 2.6f, 6.5f, 4.5f, .9f, d => GruntRockToss());
                    break;
                case EnemyType.Shooter:
                    Stance(SquadRole.Backline, 5.6f);
                    Add("hop", AiTag.Retreat, 0, 2.7f, 1f, 1.6f, ShooterHop);
                    Add("burst", AiTag.Aimed, 2.7f, 9, 6, 1, ShooterBurst);
                    Add("fan", AiTag.Zone, 2.7f, 9, 5, .8f, ShooterFan);
                    break;
                case EnemyType.Charger:
                    Stance(SquadRole.Skirmisher, 4.6f, .8f);
                    Add("tusk", AiTag.Melee, 0, 2.3f, 1.4f, 1, ChargerTusk);
                    Add("ram", AiTag.Gapcloser, 2.3f, 9, 5, 1, d => ChargerRam(d, Enraged ? 2 : 1));
                    break;
                case EnemyType.Leaper:
                    Stance(SquadRole.Skirmisher, 3.6f, .55f);
                    Add("slam", AiTag.Gapcloser, 1.5f, 8, 4, 1, d => LeapSlam(d, false));
                    Add("hops", AiTag.Zone, 0, 4.5f, 2.5f, .9f, d => LeaperHops());
                    break;
                case EnemyType.Tidecaller:
                    Stance(SquadRole.Backline, 5.5f);
                    Add("tide", AiTag.Zone, 0, 10, 5, 1, d => CallTide(false));
                    Add("ripple", AiTag.Aimed, 0, 10, 5, 1, TideRipple);
                    break;
                case EnemyType.Sentinel:
                    Stance(SquadRole.Frontline, 3f, .75f);
                    Add("lunge", AiTag.Gapcloser, 3.6f, 7.5f, 5, 1, SentinelLunge);
                    Add("cut", AiTag.Melee, 0, 3.6f, 2.5f, 1, SentinelCut);
                    Add("cross", AiTag.Melee, 0, 3.6f, 2.5f, .9f, SentinelCross);
                    break;
                case EnemyType.Sporeling:
                    Stance(SquadRole.Rusher, 0f);
                    Add("burst", AiTag.Free, 0, 1.7f, 1, 3, d => SporelingBurst());
                    break;
                case EnemyType.RuneTotem:
                    Stance(SquadRole.Anchor, 0f, 0f);
                    Add("spiral", AiTag.Zone, 0, Any, 5, 1, d => TotemSpiral());
                    Add("beams", AiTag.Aimed, 0, Any, 5, 1, d => TotemBeams());
                    break;
                case EnemyType.TideCrab:
                    Stance(SquadRole.Skirmisher, 3.2f);
                    Add("sweep", AiTag.Melee, 0, 3.1f, 2, 1, CrabSweep);
                    Add("bubbles", AiTag.Aimed, 3.1f, 8, 5, 1, CrabBubbles);
                    break;
                case EnemyType.Wisp:
                    Stance(SquadRole.Flanker, 4.5f);
                    Add("blink", AiTag.Aimed, 0, Any, 4, 1, d => WispBlink());
                    break;
                case EnemyType.SealKnight:
                    Stance(SquadRole.Frontline, 2.4f);
                    Add("combo", AiTag.Melee, 0, 3.3f, 2, 1, d => KnightCombo());
                    Add("spikes", AiTag.Zone, 3.3f, 9, 5, 1, KnightSpikes);
                    break;
                case EnemyType.Hexer:
                    Stance(SquadRole.Backline, 6.5f);
                    Add("ring", AiTag.Zone, 0, 3.4f, 2, 1.3f, d => HexerRing());
                    Add("curses", AiTag.Zone, 3.4f, 11, 6, 1, d => HexerCurses());
                    break;
                default:
                    BuildStageMobMoves();
                    break;
            }
        }

        private void UpdateBrain(Vector2 delta, Vector2 direction)
        {
            EnsureBrain();
            if (acting)
                return;
            director.ReleaseTokens(this);
            MoveToSlot(delta, direction);
            if (specialTimer > 0f)
                return;
            ChooseMove(delta.magnitude, direction);
        }

        void MoveToSlot(Vector2 delta, Vector2 direction)
        {
            if (Role == SquadRole.Anchor)
            {
                SetPlanar(Vector2.zero, true);
                return;
            }
            if (Role == SquadRole.Rusher || EnemyAiDirector.Baseline)
            {
                var chase = Role == SquadRole.Rusher || delta.magnitude > DesiredRange ? direction : delta.magnitude < DesiredRange * .7f ? -direction : Vector2.zero;
                SetPlanar(SteerAround(chase) * speed * moveScale);
                return;
            }
            var toSlot = director.SlotFor(this) - body.position;
            var heading = toSlot.sqrMagnitude > .16f ? toSlot.normalized : Vector2.zero;
            // Bodies keep apart, so a squad reads as a formation instead of a pile.
            foreach (var other in director.Members)
            {
                if (other == this || other == null)
                    continue;
                var away = body.position - (Vector2)other.transform.position;
                var distance = away.magnitude;
                if (distance > .01f && distance < SameRoomSpacing)
                    heading += away / distance * (1f - distance / SameRoomSpacing) * 1.4f;
            }
            SetPlanar(heading.sqrMagnitude < .001f ? Vector2.zero : SteerAround(heading) * speed * moveScale);
        }

        void ChooseMove(float distance, Vector2 direction)
        {
            AiMove best = null, second = null;
            float bestScore = float.NegativeInfinity, secondScore = float.NegativeInfinity;
            var report = new StringBuilder();
            foreach (var move in moves)
            {
                if (distance < move.Min || distance > move.Max)
                    continue;
                if (move.Allowed != null && !move.Allowed())
                    continue;
                var score = Score(move, distance);
                report.Append(move.Name).Append('=').Append(score.ToString("0.0")).Append(' ');
                if (score > bestScore)
                {
                    second = best;
                    secondScore = bestScore;
                    best = move;
                    bestScore = score;
                }
                else if (score > secondScore)
                {
                    second = move;
                    secondScore = score;
                }
            }
            if (best == null)
            {
                specialTimer = .2f;
                return;
            }
            // Close calls go either way, or the same situation would always get the same answer and be easy to read.
            var chosen = best;
            if (second != null && !EnemyAiDirector.Baseline && bestScore - secondScore < .35f && UnityEngine.Random.value < .4f)
            {
                chosen = second;
                bestScore = secondScore;
            }
            if (!director.TryTakeToken(this, chosen.Tag))
            {
                LastDecision = "대기 · " + report;
                if (Time.time >= nextChoiceLog)
                {
                    nextChoiceLog = Time.time + 1f;
                    director.Record("wait-token", this, chosen.Name, bestScore, report.ToString().Trim());
                }
                specialTimer = .25f;
                return;
            }
            chosen.LastUsed = Time.time;
            lastMove = chosen;
            LastDecision = chosen.Name + " · " + report;
            director.Record("decide", this, chosen.Name, bestScore, report.ToString().Trim());
            StartCoroutine(chosen.Run(Aim(chosen, direction)));
        }

        float Score(AiMove move, float distance)
        {
            var span = Mathf.Max(1f, move.Max < 90f ? move.Max - move.Min : 8f);
            var score = move.Weight + 1f - Mathf.Abs(distance - move.Ideal) / span;
            score += Mathf.Clamp01((Time.time - move.LastUsed) / FreshAfterSeconds) * .8f;
            if (move == lastMove)
                score -= RepeatPenalty;
            if (EnemyAiDirector.Baseline)
                return score;
            switch (move.Tag)
            {
                case AiTag.Zone:
                    // A floor attack lands best where someone just finished dashing or is standing still.
                    if (director.PlayerDashedRecently)
                        score += .6f;
                    if (director.PlayerStandingStill)
                        score += .4f;
                    break;
                case AiTag.Aimed:
                    if (director.PlayerStandingStill)
                        score += .5f;
                    if (director.PlayerDashedRecently)
                        score -= .4f;
                    break;
                case AiTag.Melee:
                    if (director.HealthRatio < .4f)
                        score += .5f;
                    break;
                case AiTag.Gapcloser:
                    if (director.PlayerKeepsAway)
                        score += .8f;
                    break;
                case AiTag.Retreat:
                    if (director.Phase == PacingPhase.Relax)
                        score += .4f;
                    break;
            }
            if (director.Phase == PacingPhase.Peak && move.Tag != AiTag.Retreat)
                score += .2f;
            return score;
        }

        // Aimed shots go where the player is heading, with the same warning line as always: a dodge across it still works.
        Vector2 Aim(AiMove move, Vector2 direction)
        {
            if (move.Tag != AiTag.Aimed || EnemyAiDirector.Baseline || director.Phase == PacingPhase.Relax)
                return direction;
            var ahead = PlayerPoint + director.PlayerVelocity * Lead - (Vector2)transform.position;
            return ahead.sqrMagnitude > .01f ? ahead.normalized : direction;
        }
    }
}
