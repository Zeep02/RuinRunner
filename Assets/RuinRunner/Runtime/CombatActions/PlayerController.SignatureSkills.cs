using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The 70 signature skills. Each case is one weapon from Tools/Art/weapons70.json.</summary>
    public sealed partial class PlayerController
    {
        float sigLockUntil;
        bool sigHurt;
        internal bool SigMoveLocked => sig != null && combatClock < sigLockUntil;

        bool SigPassThrough()
        {
            switch (sig.Id)
            {
                case "merc_slab":
                case "ironback_blade":
                    return true;
            }
            return false;
        }
        // A second press is the second half of a skill already paid for, so it never waits for a swing to end.
        bool SigAllowsDuringAttack()
        {
            switch (sig.Id)
            {
                case "instant_king":
                case "siege_engineer":
                    return true;
                case "falling_blossom":
                    return sig.Stage > 0;
                case "shadow_suture":
                    return sig.Stage == 2;
                case "hour_minute":
                case "dragon_spine":
                case "bannerless":
                case "star_fold":
                case "mirror_eye":
                case "small_eclipse":
                    return sig.Stage == 1;
                case "world_pendulum":
                    return sig.Stage == 1 && sig.Count > 0;
                case "lightning_carbine":
                    return sig.Stage == 1 && sig.Marks.Count > 0;
            }
            return false;
        }
        bool SigCanCast()
        {
            switch (sig.Id)
            {
                case "thousand_returns":
                    return sig.Points.Count > 0;
                case "scrap_revolver":
                    return ActiveWeaponState != null && ActiveWeaponState.Ammo > 0;
                case "last_bell":
                    return SigNearestToAim(9) != null;
            }
            return true;
        }
        EnemyController SigNearestToAim(float range)
        {
            EnemyController best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in CombatActionGeometry.Circle(transform.position, range, 12))
            {
                var to = (Vector2)enemy.transform.position - (Vector2)transform.position;
                var off = Mathf.Abs(Vector2.SignedAngle(SigAim, to));
                if (off < best_angle && off + to.magnitude * 3 < bestScore)
                {
                    bestScore = off + to.magnitude * 3;
                    best = enemy;
                }
            }
            return best;
        }
        const float best_angle = 70;

        // ---- first press
        void SigCast()
        {
            var aim = SigAim;
            var from = (Vector2)transform.position;
            var angle = SigAngle;
            var A = C("A");
            var B = C("B");
            var CC = C("C");
            var hit = C("Hit");
            switch (sig.Id)
            {
                // ---- daggers
                case "alley_awl":
                    // Straight through whatever is in the way, and out the far side with a cut hanging behind.
                    sig.Until = combatClock + .4f;
                    SigSwing(MeleeAttackMotion.Thrust, 1.2f);
                    SigDash(aim, 3f, .13f, null, () =>
                    {
                        var to = (Vector2)transform.position;
                        SigFx.Play(B, (from + to) * .5f, angle, -1);
                        SigSeg(from, to, 1f, D(1.1f), CC, A, 3);
                        ActionShake(.1f);
                    });
                    break;
                case "saw_fang":
                    // Three rakes in a row in front of the hero, each one a real hit that opens a wound, then a hop back.
                    sig.Until = combatClock + .35f;
                    SigSwing(MeleeAttackMotion.Sweep, 1f);
                    {
                        var at = Ahead(1.5f);
                        var dir = angle;
                        var pop = CC;
                        var wound = A;
                        for (int i = 0; i < 3; i++)
                        {
                            int index = i;
                            SigAfter(.05f + i * .09f, () =>
                            {
                                SigFx.Play(B, at, dir + (index - 1) * 35, -1, false, index == 1);
                                foreach (var enemy in SigArea(at, 1.3f, D(.45f), hit, 0, .05f))
                                    EnemyStatus.On(enemy).AddDot(this, "bleed", D(.13f), 3, wound);
                                if (index == 2)
                                    SigFx.Play(pop, at, 0, -1);
                            });
                        }
                    }
                    SigAfter(.28f, () => SigDash(-aim, 1.4f, .16f));
                    break;
                case "acrobat_knife":
                    {
                        SigSwing(MeleeAttackMotion.Thrust, .4f);
                        for (int i = -1; i <= 1; i++)
                        {
                            var knife = SigBolt.Shoot(this, A, SkillOrigin, Quaternion.Euler(0, 0, i * 22) * aim, 13, D(.6f), .36f, 1, 4.8f);
                            knife.ReturnsToOwner = true;
                            knife.Impact = hit;
                            knife.OnCaught = () =>
                            {
                                if (sig == null || sig.Id != "acrobat_knife")
                                    return;
                                sig.Until = combatClock + .45f;
                                SigFx.Play(B, SkillOrigin, 0, -1);
                            };
                        }
                        break;
                    }
                case "frost_fang":
                    sig.Stage = 1;
                    sig.Until = combatClock + 2.2f;
                    SigSwing(MeleeAttackMotion.Sweep, .9f);
                    break;
                case "viper_tongue":
                    {
                        sig.Stage = 1;
                        SigSwing(MeleeAttackMotion.Thrust, 1f);
                        var at = Ahead(1.9f);
                        SigFx.Play(C("Cast"), at, 0, -1, true);
                        SigFx.Play(B, at, 0, -1, true);
                        SigAfter(.15f, () =>
                        {
                            foreach (var enemy in SigArea(at, 1.5f, D(.4f), hit, 0, .08f))
                                EnemyStatus.On(enemy).AddDot(this, "poison", D(.14f), 3, C("C"));
                        });
                        break;
                    }
                case "ember_claw":
                    {
                        SigSwing(MeleeAttackMotion.Sweep, .9f);
                        var dropped = new List<Vector2>();
                        var embers = new List<SigFx>();
                        var struck = new Dictionary<EnemyController, int>();
                        float next = .15f, moved = 0;
                        var last = (Vector2)transform.position;
                        float pop = D(.4f);
                        SigDash(aim, 3f, .2f, p =>
                        {
                            moved += Vector2.Distance(last, p);
                            last = p;
                            if (moved >= next)
                            {
                                next += .6f;
                                var point = p + Vector2.down * .2f;
                                dropped.Add(point);
                                embers.Add(SigFx.Loop(A, point, 0, true, 15));
                            }
                        }, () =>
                        {
                            for (int i = 0; i < dropped.Count; i++)
                            {
                                int index = i;
                                SigAfter(.12f + .075f * i, () =>
                                {
                                    embers[index].Stop();
                                    SigFx.Play(B, dropped[index], 0, .7f);
                                    foreach (var enemy in CombatActionGeometry.Circle(dropped[index], .85f))
                                    {
                                        struck.TryGetValue(enemy, out var count);
                                        if (count >= 2)
                                            continue;
                                        struck[enemy] = count + 1;
                                        SigHit(enemy, pop, ((Vector2)enemy.transform.position - dropped[index]).normalized * 3, hit);
                                    }
                                    ActionShake(.05f);
                                    CombatAudio.Native("heavy-impact", AudioChannel.Player, .16f);
                                });
                            }
                        });
                        break;
                    }
                case "shadow_suture":
                    {
                        // The tendrils are the hit: every enemy they catch gets a thread, and the cut comes on the next press.
                        SigSwing(MeleeAttackMotion.Sweep, .2f);
                        var center = Ahead(SutureReach);
                        sig.Stage = 0;
                        sig.Marks.Clear();
                        sig.ClearViews();
                        SigFx.Play(C("Cast"), center, 0, -1, true);
                        SigAfter(.2f, () =>
                        {
                            if (sig == null || sig.Id != "shadow_suture")
                                return;
                            foreach (var enemy in SigArea(center, SutureRadius, D(.5f), hit, 0, .06f))
                            {
                                sig.Marks.Add(enemy);
                                sig.Views.Add(SigFx.Line(A, SkillOrigin, enemy.transform.position, -1, false, true));
                            }
                            if (sig.Marks.Count > 0)
                            {
                                sig.Stage = 2;
                                sig.Until2 = combatClock + 5;
                            }
                        });
                        break;
                    }
                case "blood_bank":
                    {
                        // What was saved up decides how big it gets; an empty bank still bites.
                        var saved = sig.A;
                        var at = Ahead(1.9f);
                        var big = Mathf.Lerp(.7f, 1.25f, saved / 10);
                        var radius = 1.1f + .14f * saved;
                        SigSwing(MeleeAttackMotion.Thrust, 1 + .22f * saved);
                        Stats.Heal(saved * .5f);
                        SigFx.Play(C("Cast"), at, 0, -1, true, false, big);
                        SigAfter(.14f, () =>
                        {
                            SigFx.Play(B, at, 0, -1, true, false, big);
                            SigArea(at, radius, D(.7f + .16f * saved), hit, 4, .12f + saved * .012f);
                        });
                        sig.A = 0;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                        break;
                    }
                case "hour_minute":
                    sig.Stage = 1;
                    sig.Until = combatClock + 5;
                    sig.Point = from;
                    sig.Points.Clear();
                    sig.Points.Add(from);
                    sig.Timer = combatClock + .12f;
                    sig.ClearViews();
                    break;
                case "night_thread":
                    {
                        // Marked enemies first, then whoever else is near, up to four: stars fall on each and the thread between them cuts.
                        var marks = new List<EnemyController>(sig.Marks);
                        sig.Marks.Clear();
                        foreach (var view in sig.Views)
                            if (view != null)
                                view.Stop();
                        sig.Views.Clear();
                        foreach (var enemy in CombatActionGeometry.Circle(Ahead(2.5f), 4.5f, 8))
                            if (marks.Count < 4 && !marks.Contains(enemy))
                                marks.Add(enemy);
                        SigSwing(MeleeAttackMotion.Sweep, .4f);
                        var fall = C("Cast");
                        if (marks.Count == 0)
                            SigFx.Play(fall, Ahead(2.5f), 0, -1, true);
                        for (int i = 0; i < marks.Count; i++)
                            if (marks[i] != null)
                                SigFx.Play(fall, marks[i].transform.position, 0, -1, true);
                        for (int i = 0; i + 1 < marks.Count; i++)
                            if (marks[i] != null && marks[i + 1] != null)
                                SigFx.Line(B, marks[i].transform.position, marks[i + 1].transform.position, .3f);
                        SigAfter(.34f, () =>
                        {
                            for (int i = 0; i < marks.Count; i++)
                            {
                                if (marks[i] == null)
                                    continue;
                                if (i + 1 < marks.Count && marks[i + 1] != null)
                                    SigSeg(marks[i].transform.position, marks[i + 1].transform.position, .55f, D(1.2f), CC, hit);
                                SigHit(marks[i], D(.9f), Vector2.zero, hit);
                            }
                            ActionShake(.14f);
                        });
                        break;
                    }
                // ---- greatswords
                case "lumber_blade":
                    sig.Until = combatClock + .5f;
                    SigSwing(MeleeAttackMotion.Sweep, 1.25f);
                    break;
                case "warden_door":
                    sig.Stage = 1;
                    sig.Flag = false;
                    sig.Until = combatClock + .55f;
                    sigLockUntil = sig.Until;
                    IsGuarding = true;
                    weaponActionTimer = .55f;
                    break;
                case "cinder_cleaver":
                    {
                        SigSwing(MeleeAttackMotion.Cleave, 1.1f);
                        var far = Ahead(4);
                        SigCastAt((from + far) * .5f, .7f);
                        SigAfter(.13f, () =>
                        {
                            SigFx.Line(A, from, far, 1.1f, true);
                            for (int i = 0; i < 5; i++)
                            {
                                var at = Vector2.Lerp(from, far, (i + .5f) / 5);
                                int index = i;
                                SigAfter(index * .06f, () =>
                                {
                                    SigFx.Play(B, at, 0, .7f);
                                    foreach (var enemy in SigArea(at, .8f, D(.3f), null, 1, 0))
                                        EnemyStatus.On(enemy).AddDot(this, "burn", D(.08f), 2);
                                });
                            }
                        });
                        break;
                    }
                case "glacier_tomb":
                    {
                        // Each pillar is bigger than the last, so the ridge climbs away from the hero and hits harder the further it goes.
                        SigSwing(MeleeAttackMotion.Cleave, 1f);
                        var clips = new[] { A, B, CC };
                        for (int i = 0; i < 3; i++)
                        {
                            var at = CombatActionGeometry.Clip(from, aim, 1.6f + i * 1.6f);
                            int index = i;
                            SigAfter(.14f + .13f * i, () =>
                            {
                                SigFx.Play(clips[index], at, 0, -1, true);
                                foreach (var enemy in SigArea(at, 1.1f + .4f * index, D(.55f + .2f * index), hit, 2, .1f + index * .05f))
                                    EnemyStatus.On(enemy).Slow(.55f, 2);
                            });
                        }
                        break;
                    }
                case "thunder_slug":
                    {
                        SigSwing(MeleeAttackMotion.Cleave, 1.05f);
                        sig.Point = Ahead(1.6f);
                        SigCastAt(sig.Point);
                        sig.Target = null;
                        sig.Stage = 1;
                        sig.Until = combatClock + .45f;
                        SigAfter(.15f, () =>
                        {
                            if (sig == null)
                                return;
                            var target = sig.Target;
                            var fixedPoint = sig.Point;
                            var nail = SigFx.Loop(A, target != null ? (Vector2)target.transform.position : fixedPoint, 0, false, 12, target != null ? target.transform : null, 1, .8f);
                            SigAfter(.7f, () =>
                            {
                                var p = target != null ? (Vector2)target.transform.position : fixedPoint;
                                SigFx.Play(B, p, 0, .42f);
                                SigFx.Play(CC, p, 0, .45f, true);
                                SigArea(p, 1.5f, D(1.4f), hit, 3, .12f);
                            });
                        });
                        break;
                    }
                case "gravity_stele":
                    {
                        var center = Ahead(2.3f);
                        sigLockUntil = combatClock + .2f;
                        SigCastAt(center, 1, true);
                        for (int i = 0; i < 7; i++)
                            SigAfter(i * .1f, () =>
                            {
                                foreach (var enemy in CombatActionGeometry.Circle(center, 3.2f))
                                    SigPull(enemy, center, 6);
                            });
                        SigAfter(.72f, () =>
                        {
                            SigSwing(MeleeAttackMotion.Cleave, .6f);
                            SigFx.Play(CC, center, 0, .3f, true);
                            SigFx.Play(B, center, 0, .6f);
                            SigArea(center, 1.9f, D(1.6f), hit, 5, .16f);
                        });
                        break;
                    }
                case "executioner_scale":
                    sig.Stage = 1;
                    sig.Until = combatClock + .55f;
                    sigLockUntil = sig.Until + .1f;
                    sig.View = SigFx.Loop(A, (Vector2)transform.position + Vector2.up * .9f, 0, false, 12, transform, 1, .6f);
                    break;
                case "dawn_gate":
                    {
                        var gate = Ahead(1.6f);
                        sig.Point = gate;
                        SigCastAt(gate);
                        SigFx.Play(A, gate, 0, .62f);
                        var dir = aim;
                        SigAfter(.28f, () =>
                        {
                            SigSwing(MeleeAttackMotion.Cleave, .6f);
                            SigSeg(gate, gate + dir * 5.6f, 1.15f, D(2f), B, hit, 4);
                            ActionShake(.15f);
                            if (sig != null)
                                sig.Until = combatClock + 3;
                        });
                        break;
                    }
                case "continent_scar":
                    {
                        SigSwing(MeleeAttackMotion.Cleave, .8f);
                        var a = from + aim * .6f;
                        var b = CombatActionGeometry.Clip(from, aim, 6.6f);
                        var mid = (a + b) * .5f;
                        SigCastAt(mid, .75f);
                        SigFx.Line(A, a, b, 1.25f, true);
                        for (int i = 0; i < 5; i++)
                        {
                            var p1 = Vector2.Lerp(a, mid, i / 5f);
                            var p2 = Vector2.Lerp(b, mid, i / 5f);
                            SigAfter(.5f + i * .1f, () =>
                            {
                                SigFx.Play(B, p1, 0, .5f);
                                SigFx.Play(B, p2, 0, .5f);
                                SigArea(p1, .85f, D(.35f), hit, 2, 0);
                                SigArea(p2, .85f, D(.35f), hit, 2, .05f);
                            });
                        }
                        SigAfter(1.05f, () =>
                        {
                            SigFx.Play(CC, mid, 0, .7f);
                            SigArea(mid, 2.2f, D(2f), hit, 6, .2f);
                        });
                        break;
                    }
                // ---- katanas
                case "trainee_blade":
                    sig.Stage = 1;
                    sig.Until = combatClock + .16f;
                    sigLockUntil = sig.Until;
                    weaponActionTimer = .16f;
                    attackTimer = Mathf.Max(attackTimer, .3f);
                    break;
                case "wanderer_blade":
                    {
                        BeginActionMelee(MeleeAttackMotion.Sweep, .4f);
                        SigDash(aim, 2.5f, .16f, null, () =>
                        {
                            var to = (Vector2)transform.position;
                            SigSeg(from, to, 1f, D(1.35f), A, hit);
                            SigFx.Play(B, to, angle, .45f);
                        });
                        break;
                    }
                case "moon_white":
                    {
                        SigSwing(MeleeAttackMotion.Sweep, .3f);
                        for (int i = -1; i <= 1; i += 2)
                        {
                            var dir = (Vector2)(Quaternion.Euler(0, 0, i * 13) * aim);
                            var wave = SigBolt.Shoot(this, A, from + aim * .4f, dir, 12.5f, D(1.05f), .42f, 4, 7);
                            wave.Impact = B;
                        }
                        break;
                    }
                case "red_lotus":
                    {
                        SigSwing(MeleeAttackMotion.Sweep, .8f);
                        var far = Ahead(3.5f);
                        SigFx.Line(A, from, far, .55f, true);
                        SigAfter(.45f, () =>
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                var at = Vector2.Lerp(from, far, (i + .5f) / 5);
                                int index = i;
                                SigAfter(index * .05f, () =>
                                {
                                    SigFx.Play(B, at, 0, -1);
                                    SigArea(at, .72f, D(.5f), hit, 1, .03f);
                                });
                            }
                            SigAfter(.28f, () =>
                            {
                                SigFx.Play(C("E"), far, 0, -1);
                                SigArea(far, 1.95f, D(1.1f), hit, 3, .14f);
                            });
                        });
                        break;
                    }
                case "wind_cutter":
                    sig.Stage = 1;
                    sig.Until = combatClock + 3.2f;
                    sig.View = SigFx.Loop(A, SkillOrigin, 0, false, 12, transform);
                    break;
                case "falling_blossom":
                    sig.Stage = 1;
                    sig.Flag = true;
                    sig.Timer = combatClock;
                    sigReady = combatClock + .1f;
                    SigSwing(MeleeAttackMotion.Sweep, .7f, 0);
                    SigFx.Play(A, Ahead(1.3f), angle);
                    break;
                case "mu_eum":
                    {
                        SigSwing(MeleeAttackMotion.Sweep, .2f);
                        sig.Marks.Clear();
                        sig.Stage = 1;
                        sig.Until = combatClock + .6f;
                        sigHurt = false;
                        foreach (var enemy in CombatActionGeometry.Segment(from, Ahead(2.9f), 2.3f))
                        {
                            sig.Marks.Add(enemy);
                            sig.Views.Add(SigFx.Loop(A, enemy.transform.position, angle, false, 12, enemy.transform));
                        }
                        break;
                    }
                case "instant_king":
                    sig.Count = 2;
                    sig.Until = combatClock + InstantKingWindow;
                    sig.Points.Clear();
                    sig.ClearViews();
                    SigInstantCut(0);
                    break;
                case "thousand_returns":
                    {
                        // A ghost of the hero stands in each spot and swings for real: a spinning cut, then a cross on top of it.
                        var points = new List<Vector2>(sig.Points);
                        var angles = new List<float>(sig.Angles);
                        var marks = new List<SigFx>(sig.Views);
                        var weapon = EquippedWeapon;
                        var cross = C("Cross");
                        sig.Points.Clear();
                        sig.Angles.Clear();
                        sig.Views.Clear();
                        for (int i = 0; i < points.Count; i++)
                        {
                            int index = i;
                            SigAfter(.2f * i, () =>
                            {
                                if (marks[index] != null)
                                    marks[index].Stop();
                                var dir = new Vector2(Mathf.Cos(angles[index] * Mathf.Deg2Rad), Mathf.Sin(angles[index] * Mathf.Deg2Rad));
                                var feet = points[index];
                                SigPhantom.Swing(feet, dir, 2, weapon, new Color(.7f, .9f, 1f, .9f));
                                SigFx.Play(B, feet, angles[index], .56f, false, dir.x < 0);
                                SigArea(feet, PhantomReach, D(.7f), hit, 3, .08f);
                                SigAfter(.17f, () =>
                                {
                                    SigFx.Play(cross, feet + dir * .5f, angles[index], .56f);
                                    SigArea(feet, PhantomReach, D(.5f), null, 2, .05f);
                                });
                            });
                        }
                        break;
                    }
                // ---- spears
                case "sentry_spear":
                    sig.Until = combatClock + .35f;
                    SigSwing(MeleeAttackMotion.Thrust, 1.35f);
                    SigFx.Play(B, Feet - aim * 1.1f, 0, .5f, true, false, .55f);
                    {
                        var tip = SkillOrigin;
                        var dir = aim;
                        SigAfter(.08f, () =>
                        {
                            var end = CombatActionGeometry.Clip(tip, dir, 4);
                            SigFx.Reach(CC, tip, end, 4);
                            foreach (var enemy in SigSeg(tip, end, .7f, D(.7f), null, hit, 6))
                                enemy.Stagger(.7f);
                            ActionShake(.08f);
                        });
                    }
                    break;
                case "hunt_harpoon":
                    {
                        SigSwing(MeleeAttackMotion.Thrust, .2f);
                        var rope = SigFx.Line(B, from, from + aim * .1f, -1, false, true).Native(4.4f);
                        var harpoon = SigBolt.Shoot(this, A, from + aim * .5f, aim, 16, D(1f), .3f, 1, 6.5f);
                        harpoon.Impact = CC;
                        harpoon.OnMove = b =>
                        {
                            if (rope != null)
                                rope.SetLine(transform.position, b.transform.position);
                        };
                        harpoon.OnHit = (enemy, point) =>
                        {
                            if (rope != null)
                                rope.Stop();
                            SigPull(enemy, transform.position, 16);
                            SigFx.Play(CC, point, 0, -1);
                            ActionShake(.1f);
                        };
                        harpoon.OnEnd = (point, wall) =>
                        {
                            if (rope != null)
                                rope.Stop();
                        };
                        break;
                    }
                case "side_blade_pike":
                    SigSwing(MeleeAttackMotion.Sweep, .05f);
                    SigFx.Play(A, transform.position, angle, .56f, true);
                    SigFx.Play(B, Feet, 0, .5f, true);
                    SigArea(transform.position, 2.5f, D(1.2f), hit, 8, .12f);
                    break;
                case "thunder_needle":
                    sig.Stage = 1;
                    sig.Until = combatClock + 7;
                    sig.Point = Ahead(2.6f);
                    if (sig.View != null)
                        sig.View.Stop();
                    sig.View = SigFx.Loop(A, sig.Point, 0, false);
                    SigSwing(MeleeAttackMotion.Thrust, .2f);
                    {
                        var at = sig.Point;
                        SigFx.Play(CC, at, 0, -1, true);
                        SigAfter(.08f, () =>
                        {
                            SigFx.Play(C("D"), at, 0, -1);
                            SigArea(at, 2f, D(.8f), hit, 3, .12f);
                        });
                    }
                    break;
                case "tide_fang":
                    {
                        // The current goes out in three jets as wide as the hit, whirls at the far end, then comes back and drags everything home.
                        SigSwing(MeleeAttackMotion.Thrust, .8f);
                        var far = Ahead(4.2f);
                        var tip = SkillOrigin;
                        var side = new Vector2(-aim.y, aim.x) * .2f;
                        foreach (var off in new[] { -1, 0, 1 })
                            SigFx.Play(A, tip + side * off, angle + off * 8, -1, false, off < 0);
                        SigAfter(.1f, () =>
                        {
                            SigSeg(from, far, 1.25f, D(.75f), null, hit, 8);
                            SigFx.Play(CC, far, 0, -1);
                            ActionShake(.08f);
                        });
                        SigAfter(.28f, () => SigFx.Play(C("D"), far, 0, -1, true));
                        SigAfter(.62f, () =>
                        {
                            var home = (Vector2)transform.position;
                            var back = Mathf.Atan2(home.y - far.y, home.x - far.x) * Mathf.Rad2Deg;
                            var cross = new Vector2(-(home - far).normalized.y, (home - far).normalized.x) * .2f;
                            foreach (var off in new[] { -1, 0, 1 })
                                SigFx.Play(B, far + cross * off, back + off * 8, -1, false, off < 0);
                            foreach (var enemy in CombatActionGeometry.Segment(far, home, 1.25f))
                            {
                                SigHit(enemy, D(.45f), Vector2.zero, hit);
                                SigPull(enemy, home, 8);
                            }
                            SigFx.Play(CC, home + (far - home).normalized * .9f, 0, -1);
                            ActionShake(.1f);
                        });
                        break;
                    }
                case "thorn_spear":
                    {
                        SigSwing(MeleeAttackMotion.Thrust, .9f);
                        var at = Ahead(2.5f);
                        SigFx.Line(A, from, at, .4f, true);
                        SigAfter(.18f, () =>
                        {
                            SigFx.Play(CC, at, 0, -1, true);
                            for (int i = 0; i < 5; i++)
                            {
                                var ring = at + new Vector2(Mathf.Cos(i * 1.2566f), Mathf.Sin(i * 1.2566f)) * 1f;
                                int index = i;
                                SigAfter(.04f * index, () => SigFx.Play(CC, ring, 0, -1, true));
                            }
                            foreach (var enemy in SigArea(at, 1.55f, D(.5f), hit, 0, .08f))
                            {
                                EnemyStatus.On(enemy).Root(1.6f);
                                SigFx.Loop(B, enemy.transform.position, 0, false, 12, enemy.transform, 1, 1.6f);
                            }
                        });
                        break;
                    }
                case "chain_horizon":
                    {
                        SigSwing(MeleeAttackMotion.Thrust, .3f);
                        StartCoroutine(SigChainFan(aim, hit));
                        break;
                    }
                case "bannerless":
                    {
                        // Driven in where the cursor is: the ring is the area, and the next press sends the charge out of it.
                        var at = AimPoint(BannerReach);
                        sig.Stage = 1;
                        sig.Until = combatClock + BannerSeconds;
                        sig.Point = at;
                        sig.Timer = combatClock + .5f;
                        sig.ClearViews();
                        SigSwing(MeleeAttackMotion.Thrust, .3f);
                        SigFx.Play(C("Cast"), at, 0, -1, true);
                        ActionShake(.12f);
                        sig.View = SigFx.Loop(A, at, 0, true);
                        sig.View2 = SigFx.Loop(CC, at, 0, false, 8);
                        SigArea(at, BannerRadius, D(.8f), hit, 4, 0);
                        break;
                    }
                case "dragon_spine":
                    {
                        SigSwing(MeleeAttackMotion.Thrust, .9f);
                        sig.ClearViews();
                        sig.Points.Clear();
                        sig.Stage = 1;
                        sig.Until = combatClock + 5;
                        sig.Dir = aim;
                        var end = Ahead(4.7f);
                        SigSeg(from, end, .7f, D(.6f), B, hit, 2);
                        var dropAt = new List<Vector2>();
                        for (int i = 0; i < 5; i++)
                            dropAt.Add(Vector2.Lerp(from, end, (i + 1) / 5f));
                        foreach (var p in dropAt)
                            sig.Points.Add(p);
                        for (int i = 0; i < dropAt.Count; i++)
                        {
                            var p = dropAt[i];
                            SigAfter(i * .06f, () =>
                            {
                                if (sig != null && sig.Id == "dragon_spine" && sig.Stage == 1)
                                    sig.Views.Add(SigFx.Loop(A, p, angle - 90, true));
                            });
                        }
                        break;
                    }
                case "polaris":
                    // The thrust itself throws the lance; the star stays for further thrusts through it.
                    sig.Stage = 1;
                    sig.Until = combatClock + 8;
                    sig.Point = Ahead(2.1f);
                    sig.ClearViews();
                    sig.View = SigFx.Loop(A, sig.Point, 0, false);
                    SigSwing(MeleeAttackMotion.Thrust, .4f);
                    {
                        var tip = SkillOrigin;
                        var dir = aim;
                        SigAfter(.08f, () => SigLightSpear(tip, dir, D(1.2f)));
                    }
                    break;
                // ---- staves
                case "novice_branch":
                    sig.Holding = true;
                    sig.A = 0;
                    sigLockUntil = 0;
                    sig.View = SigFx.Loop(C("Cast"), SkillOrigin, 0, false, 30).Tracking(() => SkillOrigin);
                    break;
                case "charcoal_staff":
                    {
                        SigShotPose(aim);
                        SigFx.Play(C("Muzzle"), committedShotOrigin, angle);
                        for (int i = 0; i < 9; i++)
                        {
                            var dir = (Vector2)(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-32f, 32f)) * aim);
                            var ember = SigBolt.Shoot(this, A, committedShotOrigin, dir, UnityEngine.Random.Range(6.5f, 10.5f), D(.26f), .2f, 1, UnityEngine.Random.Range(2.1f, 3.5f));
                            ember.Impact = B;
                            ember.OnHit = (enemy, point) => EnemyStatus.On(enemy).AddDot(this, "burn", D(.09f), 2.5f);
                        }
                        break;
                    }
                case "pebble_staff":
                    {
                        SigShotPose(aim);
                        var at = AimPoint(4.6f);
                        SigFx.Play(A, at, 0, .6f);
                        SigCastAt(at, .5f);
                        SigAfter(.08f, () =>
                        {
                            SigFx.Play(B, at, 0, .5f);
                            SigArea(at, 1.15f, D(1.5f), hit, 5, .1f);
                        });
                        break;
                    }
                case "frost_branch":
                    {
                        SigShotPose(aim);
                        SigFx.Play(C("Muzzle"), committedShotOrigin, angle);
                        var ice = SigBolt.Shoot(this, A, committedShotOrigin, aim, 12, D(1f), .26f, 1, 9);
                        ice.Impact = hit;
                        ice.OnHit = (enemy, point) => EnemyStatus.On(enemy).Slow(.55f, 2.5f);
                        ice.OnEnd = (point, wall) =>
                        {
                            SigFx.Play(CC, point, 0, .5f);
                            var dir = ice.Direction;
                            foreach (var turn in new[] { 80f, -80f, 110f, -110f })
                            {
                                var shard = SigBolt.Shoot(this, B, point, Quaternion.Euler(0, 0, turn) * dir, 10, D(.4f), .2f, 1, 2.6f);
                                shard.Impact = hit;
                                shard.OnHit = (e, p) => EnemyStatus.On(e).Slow(.6f, 1.6f);
                            }
                        };
                        break;
                    }
                case "storm_cloud_staff":
                    {
                        SigShotPose(aim);
                        var start = Ahead(1.2f);
                        var cloud = SigFx.Loop(A, start, 0, false, 12, null, 1, 4.1f);
                        for (int i = 1; i <= 12; i++)
                        {
                            var at = CombatActionGeometry.Clip(start, aim, i * .38f);
                            SigAfter(i * .33f, () =>
                            {
                                if (cloud != null)
                                    cloud.transform.position = at + Vector2.up * 1.6f;
                                var under = CombatActionGeometry.Circle(at, 1.7f, 1);
                                var p = under.Count > 0 ? (Vector2)under[0].transform.position : at + UnityEngine.Random.insideUnitCircle * 1.2f;
                                SigFx.Play(B, p, 0, -1, true);
                                if (under.Count > 0)
                                    SigHit(under[0], D(.42f), Vector2.zero, hit);
                                ActionShake(.05f);
                            });
                        }
                        break;
                    }
                case "swamp_lantern":
                    {
                        SigShotPose(aim);
                        var at = AimPoint(4.6f);
                        SigCastAt(at, .5f);
                        var glob = SigBolt.Shoot(this, A, committedShotOrigin, at - committedShotOrigin, 8.5f, D(.4f), .2f, 1, Vector2.Distance(committedShotOrigin, at));
                        glob.Impact = hit;
                        glob.OnEnd = (point, wall) =>
                        {
                            SigFx.Play(CC, point, 0, .5f);
                            SigFx.Loop(B, point, 0, true, 12, null, 1, 4);
                            for (int i = 1; i <= 8; i++)
                                SigAfter(i * .5f, () =>
                                {
                                    foreach (var enemy in CombatActionGeometry.Circle(point, 1.55f))
                                        EnemyStatus.On(enemy).AddDot(this, "poison", D(.14f), 1.2f);
                                });
                        };
                        break;
                    }
                case "refractor_staff":
                    sig.Stage = 1;
                    sig.Until = combatClock + 7;
                    sig.Point = Ahead(2f);
                    sig.ClearViews();
                    sig.View = SigFx.Loop(A, sig.Point, 0, false);
                    SigShotPose(aim);
                    {
                        var prism = sig.Point;
                        var rays = new[] { C("E"), C("G"), C("F") };
                        SigFx.Play(B, prism, 0, -1);
                        ActionShake(.1f);
                        for (int i = -1; i <= 1; i++)
                        {
                            var end = CombatActionGeometry.Clip(prism, Quaternion.Euler(0, 0, i * 22) * aim, LightSpearUnits);
                            SigFx.Reach(rays[i + 1], prism, end, LightSpearUnits);
                            SigSeg(prism, end, .6f, D(.8f), null, hit, 2);
                        }
                    }
                    break;
                case "undertaker_candle":
                    SigShotPose(aim);
                    SigCastAt(AimPoint(4.4f));
                    Runner.StartCoroutine(SigSouls(AimPoint(4.4f), A, B, hit));
                    break;
                case "star_fold":
                    {
                        // Steps through a door to the cursor and leaves the door open behind.
                        var to = AimPoint(StarFoldReach);
                        sig.Stage = 1;
                        sig.Until = combatClock + StarFoldSeconds;
                        sig.Point = from;
                        sig.ClearViews();
                        SigShotPose(aim);
                        SigFx.Play(C("D"), from, 0, -1, true);
                        sig.View = SigFx.Loop(A, from, 0, true, 12);
                        invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(.25f));
                        GetComponent<DashAfterimageEffect>()?.Stamp();
                        body.position = to;
                        transform.position = to;
                        SigFx.Play(C("E"), to, 0, -1, true);
                        SigFx.Play(C("Cast"), to, 0, -1, true);
                        SigArea(to, StarFoldRadius, D(1.2f), hit, 5, .14f);
                        break;
                    }
                case "season_end":
                    {
                        SigShotPose(aim);
                        var at = AimPoint(4.2f);
                        int season = sig.Count % 4;
                        sig.Count++;
                        var clips = new[] { A, B, CC, C("D") };
                        SigFx.Play(clips[season], at, 0, -1, true);
                        if (season == 0)
                            for (int i = 0; i < 5; i++)
                            {
                                var ring = at + new Vector2(Mathf.Cos(i * 1.2566f), Mathf.Sin(i * 1.2566f)) * 1.15f;
                                SigAfter(.05f * i, () => SigFx.Play(A, ring, 0, -1, true));
                            }
                        if (season == 1)
                            SigFx.Play(C("E"), at, 0, -1);
                        if (season == 2)
                            SigFx.Play(C("G"), at, 0, -1, true);
                        if (season == 2)
                            for (int i = 0; i < 3; i++)
                            {
                                var ring = at + new Vector2(Mathf.Cos(i * 2.0944f + .5f), Mathf.Sin(i * 2.0944f + .5f)) * 1.1f;
                                SigFx.Play(CC, ring, 0, -1, true);
                            }
                        if (season == 3)
                            for (int i = 0; i < 6; i++)
                            {
                                var ring = at + new Vector2(Mathf.Cos(i * 1.0472f), Mathf.Sin(i * 1.0472f)) * 1.3f;
                                SigAfter(.08f + .03f * i, () => SigFx.Play(C("F"), ring, 0, -1, true));
                            }
                        ActionShake(.1f);
                        foreach (var enemy in SigArea(at, 1.9f, D(1f), hit, season == 2 ? 9 : 1))
                        {
                            if (season == 0)
                                EnemyStatus.On(enemy).Root(1.5f);
                            else if (season == 1)
                                EnemyStatus.On(enemy).AddDot(this, "burn", D(.14f), 3);
                            else if (season == 3)
                                EnemyStatus.On(enemy).Slow(.45f, 3);
                        }
                        break;
                    }
                // ---- orbs
                case "whetstone_orb":
                    sig.Stage = 1;
                    sig.Until = combatClock + 3.2f;
                    orb.Radius = 2.6f;
                    orb.Spin = 520;
                    orb.ContactUntil = Time.time + 3.2f;
                    orb.ContactDamage = D(.45f);
                    orb.ContactHit = hit;
                    SigFx.Play(A, orb.Position, 0, -1);
                    sig.Timer = combatClock;
                    break;
                case "lantern_orb":
                    sig.Stage = 1;
                    sig.Until = combatClock + 6.5f;
                    orb.Pin(AimPoint(3.2f));
                    SigCastAt(orb.PinPoint);
                    sig.ClearViews();
                    sig.View = SigFx.Loop(A, orb.PinPoint, 0, true);
                    break;
                case "iron_core":
                    {
                        SigShotPose(aim);
                        var origin = orb.Position;
                        SigFx.Play(CC, origin, 0, -1);
                        ActionShake(.08f);
                        for (int i = 0; i < 10; i++)
                        {
                            var dir = (Vector2)(Quaternion.Euler(0, 0, -55 + i * 110f / 9) * aim);
                            var shard = SigBolt.Shoot(this, A, origin, dir, 12, D(.36f), .3f, 1, 3.6f);
                            shard.ReturnsToOwner = true;
                            shard.Impact = hit;
                            shard.OnCaught = () => SigFx.Line(B, orb.Position, orb.Position + dir * .8f, .14f);
                        }
                        break;
                    }
                case "frost_satellite":
                    sig.Stage = 1;
                    sig.Until = combatClock + 3.6f;
                    orb.Radius = 1.8f;
                    orb.Spin = 260;
                    sig.Points.Clear();
                    sig.Timer = combatClock;
                    break;
                case "ember_heart":
                    {
                        var at = orb.Position;
                        float heat = sig.A;
                        sig.A = 0;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                        SigFx.Play(B, at, 0, -1, true, false, Mathf.Lerp(.7f, 1.4f, heat / 10));
                        SigFx.Play(CC, at, 0, -1, true, false, Mathf.Lerp(.6f, 1.2f, heat / 10));
                        SigArea(at, 1.3f + .14f * heat, D(.6f + .42f * heat), hit, 4 + heat * .3f, .12f + heat * .014f);
                        break;
                    }
                case "storm_marble":
                    {
                        SigShotPose(aim);
                        sig.Marks.Clear();
                        SigFx.Play(C("E"), orb.Position, 0, -1);
                        var marble = SigBolt.Shoot(this, A, orb.Position, aim, 11, D(.7f), .5f, 1, 5.5f);
                        marble.ReturnsToOwner = true;
                        marble.ReturnClip = CC;
                        marble.Impact = hit;
                        marble.OnHit = (enemy, point) =>
                        {
                            if (sig == null)
                                return;
                            if (!marble.Returning)
                            {
                                sig.Marks.Add(enemy);
                                sig.Views.Add(SigFx.Loop(C("D"), enemy.transform.position, 0, false, 15, enemy.transform, 1, 3));
                            }
                            else if (sig.Marks.Contains(enemy))
                            {
                                SigFx.Play(B, enemy.transform.position, 0, -1, true);
                                SigFx.Play(C("E"), (Vector2)enemy.transform.position + Vector2.up * .3f, 0, -1);
                                SigArea(point, 1.6f, D(1.3f), hit, 4, .16f);
                            }
                        };
                        break;
                    }
                case "twin_star":
                    sig.Stage = 1;
                    sig.Until = combatClock + 3;
                    orb.ContactUntil = Time.time + 3;
                    orb.ContactDamage = D(.35f);
                    orb.ContactHit = hit;
                    orb.Radius = 1.3f;
                    orbTwo.ContactUntil = Time.time + 3;
                    orbTwo.ContactDamage = D(.35f);
                    orbTwo.ContactHit = hit;
                    orbTwo.Radius = 2.2f;
                    sig.Timer = combatClock;
                    sig.ClearViews();
                    sig.View = SigFx.Line(C("D"), orb.Position, orbTwo.Position, -1, false, true);
                    SigFx.Play(C("Cast"), orb.Position, 0, -1);
                    SigFx.Play(C("Cast"), orbTwo.Position, 0, -1);
                    break;
                case "mirror_eye":
                    sig.Stage = 1;
                    sig.Until = combatClock + MirrorSeconds;
                    sig.ClearViews();
                    SigFx.Play(C("Cast"), orb.Position, 0, -1);
                    for (int i = 0; i < 3; i++)
                        sig.Views.Add(SigFx.Loop(A, SigMirrorAt(i), 0, false));
                    SigMirrorVolley(D(.8f));
                    break;
                case "small_eclipse":
                    sig.Stage = 1;
                    sig.Until = combatClock + 8;
                    orb.Pin(orb.Position);
                    sig.Point = orb.PinPoint;
                    sig.A = 0;
                    sig.Count = 0;
                    sig.B = Mathf.Atan2(transform.position.y - sig.Point.y, transform.position.x - sig.Point.x) * Mathf.Rad2Deg;
                    sig.ClearViews();
                    sig.View = SigFx.Loop(C("D"), sig.Point, 0, false, 30);
                    sig.View.SetScale(EclipseScale(0));
                    // The ring's rim is where the release will hit; it widens as the hero circles.
                    sig.View2 = SigFx.Loop(C("E"), sig.Point, 0, true);
                    sig.View2.SetScale(EclipseRadius(0) / EclipseRingUnits);
                    SigFx.Play(C("Cast"), sig.Point, 0, -1);
                    orb.GetComponent<SpriteRenderer>().enabled = false;
                    break;
                case "world_pendulum":
                    // Everything inside the ring gets caught.
                    sig.Stage = 1;
                    sig.Until = combatClock + 4;
                    sig.Count = 0;
                    sig.ClearViews();
                    sig.View = SigFx.Loop(CC, Feet, 0, true, 12, transform, 1, 4);
                    break;
                // ---- guns
                case "scrap_revolver":
                    {
                        int shots = 0;
                        while (inventory.TryConsumeAmmo() && shots < 6)
                            shots++;
                        var clip = C("Shot");
                        var flash = C("Muzzle");
                        for (int i = 0; i < shots; i++)
                        {
                            int index = i;
                            SigAfter(i * .07f, () =>
                            {
                                PrepareRangedShot(SigAim, .09f);
                                var spread = index * 4.5f * (index % 2 == 0 ? 1 : -1);
                                var dir = (Vector2)(Quaternion.Euler(0, 0, spread) * SigAim);
                                SigFx.Play(flash, committedShotOrigin, SigAngle);
                                if (!shotObstructed)
                                {
                                    var round = SigBolt.Shoot(this, clip, committedShotOrigin, dir, 18, D(.62f), .18f, 1, 11);
                                    round.Impact = hit;
                                }
                                ActionShake(.04f);
                                if (index == shots - 1)
                                    SigFx.Play(A, committedShotOrigin, 0, .5f);
                            });
                        }
                        attackTimer = Mathf.Max(attackTimer, shots * .07f);
                        break;
                    }
                case "gate_shotgun":
                    {
                        SigShotPose(aim);
                        SigFx.Play(A, committedShotOrigin, angle);
                        if (shotObstructed)
                            break;
                        for (int i = 0; i < 10; i++)
                        {
                            var dir = (Vector2)(Quaternion.Euler(0, 0, -30 + i * 60f / 9) * aim);
                            var pellet = SigBolt.Shoot(this, C("Shot"), committedShotOrigin, dir, 13, D(.32f), .2f, 1, 4.4f);
                            pellet.Impact = hit;
                            pellet.OnHit = (enemy, point) =>
                            {
                                SigPull(enemy, (Vector2)enemy.transform.position + dir * 3, 11);
                                SigFx.Play(B, (Vector2)enemy.transform.position + Vector2.down * .25f, 0, .5f, true);
                            };
                        }
                        SigDash(-aim, .55f, .1f, null, null, false);
                        ActionShake(.14f);
                        break;
                    }
                case "scout_carbine":
                    // The sight sits on the cursor and the sight line runs back to the muzzle for as long as the shot is held.
                    sig.Holding = true;
                    sig.A = 0;
                    sig.View = SigFx.Loop(A, AimPoint(ScoutRange), 0, false, 12).Tracking(() => AimPoint(ScoutRange));
                    sig.View2 = SigFx.Line(CC, SkillOrigin, AimPoint(ScoutRange), -1, false, true);
                    break;
                case "furnace_sprayer":
                    {
                        SigShotPose(aim);
                        var clip = A;
                        var smoke = B;
                        var torch = SigFx.Loop(C("Cast"), SkillOrigin, angle, false, 30, null, 1, 1.3f).Tracking(() => SkillOrigin, () => SigAngle);
                        for (int i = 0; i < 15; i++)
                        {
                            int index = i;
                            SigAfter(i * .08f, () =>
                            {
                                if (EquippedWeapon == null || EquippedWeapon.Id != "furnace_sprayer")
                                    return;
                                PrepareRangedShot(SigAim, .09f);
                                var dir = (Vector2)(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-17f, 17f)) * SigAim);
                                if (!shotObstructed)
                                {
                                    var tongue = SigBolt.Shoot(this, clip, committedShotOrigin, dir, 10, D(.2f), .5f, 3, 3.6f);
                                    tongue.Impact = hit;
                                    tongue.OnHit = (e, p) => EnemyStatus.On(e).AddDot(this, "burn", D(.08f), 2);
                                }
                                if (index % 5 == 0)
                                    SigFx.Play(smoke, committedShotOrigin + Vector2.up * .15f, 0, .55f);
                            });
                        }
                        attackTimer = Mathf.Max(attackTimer, 1.2f);
                        break;
                    }
                case "frost_bolt_rifle":
                    sig.Count = 3;
                    sig.Stacks.Clear();
                    SigShotPose(aim);
                    SigFx.Play(A, committedShotOrigin, angle, .5f);
                    {
                        var end = CombatActionGeometry.Clip(committedShotOrigin, aim, LightSpearUnits);
                        SigFx.Reach(C("Cast"), committedShotOrigin, end, LightSpearUnits);
                        foreach (var enemy in SigSeg(committedShotOrigin, end, .7f, D(1f), null, hit, 2))
                            EnemyStatus.On(enemy).Slow(.5f, 2.5f);
                        ActionShake(.1f);
                    }
                    break;
                case "ricochet_pistol":
                    {
                        SigShotPose(aim);
                        SigFx.Play(C("Muzzle"), committedShotOrigin, angle);
                        if (shotObstructed)
                            break;
                        var round = SigBolt.Shoot(this, A, committedShotOrigin, aim, 17, D(1.05f), .3f, 2, 26);
                        round.Bounces = 2;
                        round.Wall = C("E");
                        round.Impact = B;
                        round.OnHit = (enemy, point) =>
                        {
                            if (round.BouncesDone > 0)
                                SigHit(enemy, D(.8f * round.BouncesDone), Vector2.zero, B);
                        };
                        break;
                    }
                case "lightning_carbine":
                    {
                        sig.Marks.Clear();
                        sig.ClearViews();
                        sig.Stage = 1;
                        sig.Until = combatClock + 5;
                        for (int i = 0; i < 3; i++)
                        {
                            int index = i;
                            SigAfter(i * .12f, () =>
                            {
                                PrepareRangedShot(SigAim, .1f);
                                SigFx.Play(C("Muzzle"), committedShotOrigin, SigAngle);
                                if (shotObstructed)
                                    return;
                                var shot = SigBolt.Shoot(this, A, committedShotOrigin, Quaternion.Euler(0, 0, (index - 1) * 5) * SigAim, 20, D(.5f), .2f, 1, 11);
                                shot.Impact = hit;
                                shot.OnHit = (enemy, point) =>
                                {
                                    if (sig == null || sig.Id != "lightning_carbine" || sig.Marks.Contains(enemy) || sig.Marks.Count >= 3)
                                        return;
                                    sig.Marks.Add(enemy);
                                    sig.Views.Add(SigFx.Loop(B, enemy.transform.position, 0, false, 12, enemy.transform));
                                };
                            });
                        }
                        break;
                    }
                case "siege_engineer":
                    // Braced: a slam, dust and plates on the floor that stay as long as the gun is planted.
                    sig.Stage = 1;
                    sig.A = 0;
                    sigLockUntil = float.MaxValue;
                    sigReady = combatClock + .1f;
                    sig.ClearViews();
                    SigFx.Play(C("Cast"), Feet, 0, -1, true);
                    SigFx.Play(C("D"), Feet, 0, .5f, true);
                    sig.View2 = SigFx.Loop(CC, Feet, 0, true, 12);
                    sig.Views.Add(SigFx.Line(C("E"), SkillOrigin, AimPoint(ScoutRange), -1, false, true));
                    ActionShake(.12f);
                    GameAudio.Play(GameSound.Block, AudioChannel.Player, .5f);
                    break;
                case "last_bell":
                    sig.Target = SigNearestToAim(9);
                    sig.Stage = 1;
                    sig.Until = combatClock + 1.2f;
                    sig.Until2 = combatClock + 6;
                    sig.ClearViews();
                    sig.View = SigFx.Loop(A, sig.Target.transform.position, 0, false, 12 / 1.2f * 1f, sig.Target.transform);
                    break;
                case "meteor_waybill":
                    {
                        var points = new List<Vector2>(sig.Points);
                        sig.Points.Clear();
                        foreach (var view in sig.Views)
                            if (view != null)
                                view.Stop();
                        sig.Views.Clear();
                        SigShotPose(aim);
                        points.Add(AimPoint(6.5f));
                        var rock = C("Cast");
                        for (int i = 0; i < points.Count; i++)
                        {
                            var at = points[i];
                            SigAfter(.2f * i, () =>
                            {
                                SigFx.Play(rock, at, 0, -1);
                                SigAfter(MeteorFall, () =>
                                {
                                    SigFx.Play(CC, at, 0, -1, true);
                                    SigArea(at, 1.7f, D(1.4f), hit, 4, .18f);
                                });
                            });
                        }
                        break;
                    }
            }
        }
        const float SutureReach = 2.3f, SutureRadius = 1.7f, PhantomReach = 2.25f, InstantKingWindow = 1.8f, BannerReach = 4.6f, BannerRadius = 2.6f, BannerSeconds = 7, StarFoldReach = 7, StarFoldSeconds = 6, StarFoldRadius = 1.8f,
            MirrorSeconds = 6, MeteorFall = .62f, ScoutRange = 9, LightSpearUnits = 8, EclipseRingUnits = 2.6f;
    }
}
