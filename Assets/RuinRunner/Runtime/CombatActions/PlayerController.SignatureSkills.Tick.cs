using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Per-frame upkeep of running skills and held charges.</summary>
    public sealed partial class PlayerController
    {
        // ---- things that run every frame
        void SigTick()
        {
            if (sig == null)
                return;
            if (sig.Holding)
            {
                sig.A += Time.deltaTime;
                if (settings.GetKeyUp(GameAction.Guard) || sig.A >= 1.4f)
                {
                    sig.Holding = false;
                    SigReleaseHold();
                }
            }
            sig.Marks.RemoveAll(enemy => enemy == null || enemy.IsDead);
            switch (sig.Id)
            {
                case "frost_fang":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        sig.Target = null;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                    }
                    break;
                case "viper_tongue":
                    if (sig.View != null && (sig.Target == null || sig.Target.IsDead))
                    {
                        sig.View.Stop();
                        sig.View = null;
                        sig.Target = null;
                    }
                    break;
                case "shadow_suture":
                    if (sig.Stage == 2)
                    {
                        if (sig.Marks.Count == 0 || combatClock >= sig.Until2)
                        {
                            sig.Stage = 0;
                            sig.Marks.Clear();
                            sig.ClearViews();
                            break;
                        }
                        while (sig.Views.Count > sig.Marks.Count)
                        {
                            var extra = sig.Views.Count - 1;
                            if (sig.Views[extra] != null)
                                sig.Views[extra].Stop();
                            sig.Views.RemoveAt(extra);
                        }
                        for (int i = 0; i < sig.Views.Count; i++)
                            if (sig.Views[i] != null)
                                sig.Views[i].SetLine(SkillOrigin, sig.Marks[i].transform.position);
                    }
                    break;
                case "hour_minute":
                    if (sig.Stage == 1)
                    {
                        if (combatClock >= sig.Until)
                        {
                            sig.Stage = 0;
                            sig.ClearViews();
                        }
                        else if (combatClock >= sig.Timer)
                        {
                            sig.Timer = combatClock + .12f;
                            var last = sig.Points[sig.Points.Count - 1];
                            var now = (Vector2)transform.position;
                            // The road home stays on the floor as a thin line, so the press has something to show for itself.
                            if (Vector2.Distance(last, now) > .2f)
                            {
                                sig.Views.Add(SigFx.Line(C("C"), last, now, -1, true, true));
                                sig.Points.Add(now);
                            }
                        }
                    }
                    break;
                case "night_thread":
                    for (int i = sig.Views.Count - 1; i >= 0; i--)
                        if (i >= sig.Marks.Count)
                        {
                            if (sig.Views[i] != null)
                                sig.Views[i].Stop();
                            sig.Views.RemoveAt(i);
                        }
                    break;
                case "warden_door":
                    if (sig.Stage == 1 && (combatClock >= sig.Until || sig.Flag))
                    {
                        sig.Stage = 0;
                        IsGuarding = false;
                        weaponActionTimer = 0;
                        sigLockUntil = 0;
                        var aim = SigAim;
                        var front = Ahead(1.4f);
                        SigSeg(transform.position, (Vector2)transform.position + aim * 1.9f, 1.7f, D(.7f), null, C("Hit"), 9);
                        SigSwing(MeleeAttackMotion.Cleave, sig.Flag ? 1.8f : 1.05f);
                        SigAfter(.12f, () => SigFx.Play(C("C"), front, 0, .5f, true));
                    }
                    break;
                case "executioner_scale":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 2;
                        sig.Until2 = combatClock + .5f;
                        SigSwing(MeleeAttackMotion.Cleave, 1f);
                    }
                    if (sig.Stage == 2 && combatClock >= sig.Until2)
                        sig.Stage = 0;
                    break;
                case "trainee_blade":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        var end = Ahead(3.4f);
                        BeginActionMelee(MeleeAttackMotion.Sweep, .1f);
                        SigSeg(transform.position, end, .65f, D(1.7f), C("A"), C("Hit"), 4);
                        SigFx.Play(C("B"), end, 0, .5f);
                        ActionShake(.08f);
                    }
                    break;
                case "wind_cutter":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                        SigWindRelease();
                    break;
                case "mu_eum":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        var clip = C("C");
                        var hit = C("Hit");
                        var turn = C("D");
                        var marks = new List<EnemyController>(sig.Marks);
                        foreach (var view in sig.Views)
                            if (view != null)
                                view.Stop();
                        sig.Views.Clear();
                        sig.Marks.Clear();
                        if (!sigHurt)
                            foreach (var enemy in marks)
                                if (enemy != null)
                                {
                                    var p = (Vector2)enemy.transform.position;
                                    SigFx.Line(clip, p + new Vector2(-1.2f, .8f), p + new Vector2(1.2f, -.8f), .34f);
                                    SigFx.Line(clip, p + new Vector2(-1.2f, -.8f), p + new Vector2(1.2f, .8f), .34f);
                                    SigFx.Play(turn, p, 0, -1);
                                    SigHit(enemy, D(1.9f), Vector2.zero, hit);
                                }
                        if (marks.Count > 0)
                            ActionShake(.12f);
                    }
                    break;
                case "instant_king":
                    if (sig.Count > 0 && combatClock >= sig.Until)
                        SigInstantShatter();
                    break;
                case "scout_carbine":
                    if (sig.Holding && sig.View2 != null)
                        sig.View2.SetLine(SkillOrigin, AimPoint(ScoutRange));
                    break;
                case "novice_branch":
                    if (sig.Holding && sig.View != null)
                        sig.View.SetScale(.65f + Mathf.Min(1, sig.A / .75f) * .6f);
                    break;
                case "mirror_eye":
                    if (sig.Stage == 1)
                    {
                        if (combatClock >= sig.Until)
                        {
                            SigMirrorBreak(false);
                            break;
                        }
                        for (int i = 0; i < sig.Views.Count; i++)
                            if (sig.Views[i] != null)
                                sig.Views[i].transform.position = SigMirrorAt(i);
                    }
                    break;
                case "bannerless":
                    if (sig.Stage == 1)
                    {
                        if (combatClock >= sig.Until)
                        {
                            sig.Stage = 0;
                            sig.ClearViews();
                            break;
                        }
                        if (combatClock >= sig.Timer)
                        {
                            sig.Timer = combatClock + .5f;
                            foreach (var enemy in CombatActionGeometry.Circle(sig.Point, BannerRadius, 12))
                            {
                                EnemyStatus.On(enemy).Slow(.55f, .7f);
                                SigHit(enemy, D(.25f), Vector2.zero, C("Hit"));
                            }
                        }
                    }
                    break;
                case "thunder_needle":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                    }
                    break;
                case "polaris":
                case "refractor_staff":
                case "star_fold":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        sig.ClearViews();
                    }
                    break;
                case "dragon_spine":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        sig.ClearViews();
                    }
                    break;
                case "whetstone_orb":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        orb.Radius = 1.05f;
                        orb.Spin = 150;
                        break;
                    }
                    // The orbit reads as one spinning blade whose edge is where the orb cuts.
                    if (sig.Stage == 1 && combatClock >= sig.Timer)
                    {
                        sig.Timer = combatClock + .38f;
                        SigFx.Loop(C("D"), transform.position, combatClock * 260, true, 60, transform, 1, .5f);
                        ActionShake(.03f);
                    }
                    break;
                case "lantern_orb":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        sig.Stage = 0;
                        orb.Release();
                        sig.ClearViews();
                    }
                    break;
                case "frost_satellite":
                    if (sig.Stage == 1)
                    {
                        if (combatClock >= sig.Until)
                        {
                            sig.Stage = 0;
                            orb.Radius = 1.05f;
                            orb.Spin = 150;
                            break;
                        }
                        if (combatClock >= sig.Timer)
                        {
                            sig.Timer = combatClock + .16f;
                            var p = orb.Position;
                            SigFx.Play(C("A"), p, 0, -1, true);
                            foreach (var enemy in CombatActionGeometry.Circle(p, .8f))
                            {
                                EnemyStatus.On(enemy).Slow(.5f, 1.4f);
                                if (!sig.Stacks.TryGetValue(enemy, out var count) || count % 5 == 0)
                                    SigHit(enemy, D(.2f), Vector2.zero);
                                sig.Stacks[enemy] = count + 1;
                            }
                        }
                    }
                    break;
                case "twin_star":
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        // They crash together where the beam last ran.
                        sig.Stage = 0;
                        orb.Radius = 1.05f;
                        orbTwo.Radius = 1.5f;
                        var crash = (orb.Position + orbTwo.Position) * .5f;
                        sig.ClearViews();
                        SigFx.Play(C("C"), crash, 0, -1, true);
                        SigArea(crash, 1.8f, D(1.2f), C("Hit"), 4, .16f);
                        break;
                    }
                    if (sig.Stage == 1)
                    {
                        if (sig.View != null)
                            sig.View.SetLine(orb.Position, orbTwo.Position);
                        if (combatClock >= sig.Timer)
                        {
                            sig.Timer = combatClock + .25f;
                            SigSeg(orb.Position, orbTwo.Position, .5f, D(.18f), null, C("Hit"), 0);
                        }
                    }
                    break;
                case "small_eclipse":
                    if (sig.Stage == 1)
                    {
                        if (combatClock >= sig.Until)
                        {
                            sig.Stage = 0;
                            sig.ClearViews();
                            orb.Release();
                            orb.GetComponent<SpriteRenderer>().enabled = true;
                            break;
                        }
                        var now = Mathf.Atan2(transform.position.y - sig.Point.y, transform.position.x - sig.Point.x) * Mathf.Rad2Deg;
                        sig.A = Mathf.Min(720, sig.A + Mathf.Abs(Mathf.DeltaAngle(sig.B, now)));
                        sig.B = now;
                        var charged = sig.A / 360f;
                        sig.View.SetScale(EclipseScale(charged));
                        sig.View2.SetScale(EclipseRadius(charged) / EclipseRingUnits);
                    }
                    break;
                case "world_pendulum":
                    // The weights let go by themselves when the ring closes.
                    if (sig.Stage == 1 && combatClock >= sig.Until)
                    {
                        if (sig.Count > 0)
                        {
                            SigRecast();
                            break;
                        }
                        sig.Stage = 0;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                    }
                    for (int i = 0; i < sig.Views.Count; i++)
                        if (sig.Views[i] != null)
                        {
                            var a = (combatClock * 200 + i * 360f / Mathf.Max(1, sig.Views.Count)) * Mathf.Deg2Rad;
                            sig.Views[i].transform.position = (Vector2)transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.3f;
                        }
                    break;
                case "siege_engineer":
                    // While the gun is planted a sight line runs from the muzzle along the aim.
                    if (sig.Stage == 1 && sig.Views.Count > 0 && sig.Views[0] != null)
                        sig.Views[0].SetLine(SkillOrigin, AimPoint(ScoutRange));
                    if (sig.Flag && combatClock >= sig.Until2)
                    {
                        sig.Flag = false;
                        sig.A = 0;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                    }
                    if (!sig.Flag && attackTimer <= -.2f)
                        sig.A = Mathf.Max(0, sig.A - Time.deltaTime * .45f);
                    break;
                case "last_bell":
                    if (sig.Stage == 1 && (sig.Target == null || sig.Target.IsDead || combatClock >= sig.Until2))
                    {
                        sig.Stage = 0;
                        sig.Target = null;
                        sig.ClearViews();
                    }
                    break;
            }
        }
        Vector2 HeldTip => SkillOrigin;
        void SigReleaseHold()
        {
            var aim = SigAim;
            if (sig.Id == "scout_carbine" && sig.View != null)
            {
                var shotAt = AimPoint(ScoutRange) - SkillOrigin;
                if (shotAt.sqrMagnitude > .04f)
                    aim = shotAt.normalized;
            }
            sig.ClearViews();
            SigShotPose(aim);
            var hit = C("Hit");
            switch (sig.Id)
            {
                case "novice_branch":
                    {
                        SigFx.Play(C("Muzzle"), committedShotOrigin, SigAngle);
                        if (shotObstructed)
                            break;
                        bool full = sig.A >= .75f;
                        var bolt = SigBolt.Shoot(this, full ? C("Orb") : C("Shot"), committedShotOrigin, aim, full ? 15 : 12, D(.8f + Mathf.Min(1, sig.A) * 1.3f), full ? .32f : .22f, full ? 2 : 1, 12);
                        bolt.Impact = hit;
                        if (full)
                        {
                            // A full charge bursts once where it first lands; the picture is drawn at the burst's own radius.
                            bool burst = false;
                            var area = D(.6f);
                            bolt.OnHit = (enemy, point) =>
                            {
                                if (burst)
                                    return;
                                burst = true;
                                SigFx.Play(C("Burst"), point, 0, -1, false, false, NoviceBurstRadius * 2 * 32 / 173f);
                                SigArea(point, NoviceBurstRadius, area, null, 3, .08f);
                            };
                        }
                        break;
                    }
                case "scout_carbine":
                    {
                        SigFx.Play(C("Muzzle"), committedShotOrigin, SigAngle);
                        if (shotObstructed)
                            break;
                        bool steady = sig.A >= .8f;
                        var bolt = SigBolt.Shoot(this, C("Shot"), committedShotOrigin, aim, steady ? 30 : 22, D(.9f + Mathf.Min(1, sig.A) * 1.2f), .2f, steady ? 2 : 1, 14);
                        bolt.Impact = steady ? C("B") : hit;
                        break;
                    }
            }
        }
    }
}
