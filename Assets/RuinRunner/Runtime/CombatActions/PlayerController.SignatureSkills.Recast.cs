using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Second press of a skill that is already running.</summary>
    public sealed partial class PlayerController
    {
        // ---- second press. True when the press was spent here.
        bool SigRecast()
        {
            var aim = SigAim;
            var from = (Vector2)transform.position;
            var hit = C("Hit");
            switch (sig.Id)
            {
                case "frost_fang":
                    if (sig.Stage == 1 && combatClock < sig.Until && sig.Target != null)
                    {
                        sig.Stage = 2;
                        sig.Until = combatClock + .4f;
                        SigSwing(MeleeAttackMotion.Thrust, 1f);
                        return true;
                    }
                    break;
                case "shadow_suture":
                    if (sig.Stage == 2 && sig.Marks.Count > 0)
                    {
                        var targets = new List<EnemyController>(sig.Marks);
                        sig.Marks.Clear();
                        sig.ClearViews();
                        sig.Stage = 0;
                        targets.Sort((a, b) => ((Vector2)a.transform.position - from).sqrMagnitude.CompareTo(((Vector2)b.transform.position - from).sqrMagnitude));
                        StartCoroutine(SigSutureCuts(targets));
                        return true;
                    }
                    break;
                case "hour_minute":
                    if (sig.Stage == 1)
                    {
                        var path = new List<Vector2>(sig.Points);
                        path.Add(from);
                        sig.Stage = 0;
                        sig.ClearViews();
                        StartCoroutine(SigRewind(path, from));
                        return true;
                    }
                    break;
                case "falling_blossom":
                    if (sig.Stage >= 1 && sig.Stage < 3 && combatClock < sig.Timer + 1.1f)
                    {
                        var gap = combatClock - sig.Timer;
                        sig.Flag &= gap >= .18f && gap <= .75f;
                        sig.Timer = combatClock;
                        sig.Stage++;
                        // Three different blows of the combo, not the first one three times.
                        SigSwing(MeleeAttackMotion.Sweep, .8f, sig.Stage - 1);
                        SigFx.Play(C(sig.Stage == 2 ? "C" : "D"), Ahead(1.3f), SigAngle);
                        if (sig.Stage == 3)
                        {
                            var perfect = sig.Flag;
                            var at = Ahead(1.3f);
                            var flower = C("B");
                            var opening = C("E");
                            sig.Stage = 0;
                            if (perfect)
                            {
                                CameraFollow.MeleeHitStop?.Invoke(.07f, .05f);
                                SigAfter(.16f, () =>
                                {
                                    SigFx.Play(flower, at, 0, -1);
                                    SigArea(at, 2.25f, D(2.1f), hit, 5, .2f);
                                    SigAfter(.12f, () => SigFx.Play(opening, at, 0, -1));
                                    SigAfter(.24f, () =>
                                    {
                                        SigFx.Play(opening, at + new Vector2(.9f, .5f), 0, -1);
                                        SigFx.Play(opening, at + new Vector2(-.9f, -.5f), 0, -1);
                                    });
                                });
                            }
                        }
                        return true;
                    }
                    break;
                case "instant_king":
                    if (sig.Count > 0)
                    {
                        sig.Count--;
                        sig.Until = combatClock + InstantKingWindow;
                        bool last = sig.Count == 0;
                        SigInstantCut(2 - sig.Count);
                        if (last)
                            SigAfter(.12f, () =>
                            {
                                if (sig != null && sig.Id == "instant_king")
                                    SigInstantShatter();
                            });
                        return true;
                    }
                    break;
                case "dragon_spine":
                    if (sig.Stage == 1)
                    {
                        sig.Stage = 0;
                        var nodes = new List<Vector2>(sig.Points);
                        sig.ClearViews();
                        var spike = C("C");
                        var side = new Vector2(-sig.Dir.y, sig.Dir.x);
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            var p = nodes[i];
                            // A pair of ribs at every bone of the spine, each pointing away from it.
                            SigAfter(i * .06f, () =>
                            {
                                var outward = Mathf.Atan2(side.y, side.x) * Mathf.Rad2Deg - 90;
                                SigFx.Play(spike, p + side * .25f, outward, .7f, true);
                                SigFx.Play(spike, p - side * .25f, outward + 180, .7f, true, true);
                                SigFx.Play(C("D"), p, 0, .5f);
                                SigSeg(p - side * 1.5f, p + side * 1.5f, .6f, D(.75f), null, hit, 2);
                                ActionShake(.04f);
                            });
                        }
                        return true;
                    }
                    break;
                case "bannerless":
                    if (sig.Stage == 1)
                    {
                        // Shades of the spear charge out of the ring, shoulder to shoulder, towards the cursor.
                        var at = sig.Point;
                        var dir = (AimPoint(30) - at).sqrMagnitude > .04f ? (AimPoint(30) - at).normalized : aim;
                        var side = new Vector2(-dir.y, dir.x);
                        var spear = C("B");
                        var burst = C("Cast");
                        sig.Stage = 0;
                        sig.ClearViews();
                        SigSwing(MeleeAttackMotion.Thrust, .3f);
                        SigFx.Play(burst, at, 0, -1, true);
                        ActionShake(.14f);
                        for (int i = -2; i <= 2; i++)
                        {
                            var start = at + side * i * .7f - dir * (Mathf.Abs(i) * .5f);
                            float delay = Mathf.Abs(i) * .06f;
                            SigAfter(delay, () =>
                            {
                                var end = CombatActionGeometry.Clip(start, dir, LightSpearUnits);
                                SigFx.Reach(spear, start, end, LightSpearUnits);
                                SigSeg(start, end, .7f, D(.8f), null, hit, 3);
                                ActionShake(.06f);
                            });
                        }
                        return true;
                    }
                    break;
                case "star_fold":
                    if (sig.Stage == 1)
                    {
                        // Back through the door: the fold closes behind the hero and everything between the two doors is cut by it.
                        var door = sig.Point;
                        sig.Stage = 0;
                        sig.ClearViews();
                        SigShotPose(aim);
                        SigFx.Play(C("D"), from, 0, -1, true);
                        SigFx.Reach(C("F"), from, door, 8);
                        SigSeg(from, door, 1.2f, D(1.7f), null, hit, 3);
                        invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(.25f));
                        body.position = door;
                        transform.position = door;
                        SigFx.Play(C("E"), door, 0, -1, true);
                        SigFx.Play(C("Cast"), door, 0, -1, true);
                        SigArea(door, StarFoldRadius, D(.8f), hit, 4, .14f);
                        return true;
                    }
                    break;
                case "mirror_eye":
                    if (sig.Stage == 1)
                    {
                        SigMirrorBreak(true);
                        ActionShake(.1f);
                        return true;
                    }
                    break;
                case "wind_cutter":
                    if (sig.Stage == 1)
                    {
                        SigWindRelease();
                        return true;
                    }
                    break;
                case "small_eclipse":
                    if (sig.Stage == 1)
                    {
                        // The corona folds inward for a beat, then the whole ring goes off at once.
                        var turns = sig.A / 360f;
                        var at = sig.Point;
                        var radius = EclipseRadius(turns);
                        sig.Stage = 0;
                        sig.ClearViews();
                        orb.Release();
                        orb.GetComponent<SpriteRenderer>().enabled = true;
                        var bigger = Mathf.Clamp(radius / 2.6f, .8f, 1.5f);
                        SigFx.Play(C("C"), at, 0, .45f, true, false, bigger);
                        foreach (var enemy in CombatActionGeometry.Circle(at, radius))
                            SigPull(enemy, at, 8);
                        CameraFollow.MeleeHitStop?.Invoke(.08f, .05f);
                        SigAfter(.28f, () =>
                        {
                            SigFx.Play(C("B"), at, 0, -1, true, false, bigger);
                            SigFx.Play(C("F"), at, 0, -1, true, false, bigger);
                            SigArea(at, radius, D(.8f + turns * 1.7f), hit, -6, .24f);
                        });
                        return true;
                    }
                    break;
                case "world_pendulum":
                    if (sig.Stage == 1 && sig.Count > 0)
                    {
                        int count = sig.Count;
                        var clip = C("B");
                        sig.Stage = 0;
                        sig.Count = 0;
                        var views = new List<SigFx>(sig.Views);
                        sig.Views.Clear();
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                        for (int i = 0; i < count; i++)
                        {
                            int index = i;
                            SigAfter(i * .09f, () =>
                            {
                                if (index < views.Count && views[index] != null)
                                {
                                    var start = views[index].transform.position;
                                    views[index].Stop();
                                    var shot = SigBolt.Shoot(this, clip, start, SigAim, 15, D(.85f), .26f, 1, 14);
                                    shot.Impact = hit;
                                    shot.HomingTurn = 420;
                                    shot.HomingRange = 9;
                                }
                            });
                        }
                        return true;
                    }
                    break;
                case "lightning_carbine":
                    if (sig.Stage == 1 && sig.Marks.Count > 0)
                    {
                        var marks = new List<EnemyController>(sig.Marks);
                        sig.Marks.Clear();
                        sig.Stage = 0;
                        sig.ClearViews();
                        var arc = C("C");
                        var pop = C("D");
                        for (int i = 0; i < marks.Count; i++)
                        {
                            int index = i;
                            var a = marks[i];
                            var b = i + 1 < marks.Count ? marks[i + 1] : null;
                            SigAfter(i * .1f, () =>
                            {
                                if (a == null)
                                    return;
                                SigFx.Play(C("E"), a.transform.position, 0, -1, true);
                                SigFx.Play(pop, a.transform.position, 0, -1);
                                SigHit(a, D(1f), Vector2.zero);
                                ActionShake(.08f);
                                if (b != null)
                                    SigSeg(a.transform.position, b.transform.position, .5f, D(.8f), arc, null, 0);
                            });
                        }
                        return true;
                    }
                    break;
                case "siege_engineer":
                    if (sig.Stage == 1)
                    {
                        sig.Stage = 0;
                        sigLockUntil = 0;
                        sig.ClearViews();
                        SigFx.Play(C("D"), Feet, 0, .5f, true);
                        return true;
                    }
                    break;
                case "last_bell":
                    if (sig.Stage == 1 && combatClock >= sig.Until && sig.Target != null)
                    {
                        SigShotPose(aim);
                        var target = sig.Target;
                        var shock = C("B");
                        sig.Stage = 0;
                        sig.ClearViews();
                        SigFx.Play(C("Muzzle"), committedShotOrigin, SigAngle);
                        var dir = ((Vector2)target.transform.position - committedShotOrigin).normalized;
                        var shot = SigBolt.Shoot(this, C("Shot"), committedShotOrigin, dir, 32, D(1.7f), .2f, 1, 14);
                        shot.Impact = hit;
                        shot.OnHit = (enemy, point) =>
                        {
                            if (enemy != target)
                                return;
                            SigAfter(.2f, () =>
                            {
                                var end = CombatActionGeometry.Clip(point, dir, LightSpearUnits);
                                SigFx.Reach(shock, point, end, LightSpearUnits);
                                SigSeg(point, end, 1f, D(1.9f), null, hit, 5);
                                ActionShake(.2f);
                                CameraFollow.MeleeHitStop?.Invoke(.07f, .05f);
                            });
                        };
                        return true;
                    }
                    if (sig.Stage == 1)
                        return true;
                    break;
            }
            return false;
        }

        void SigWindRelease()
        {
            sig.Stage = 0;
            if (sig.View != null)
            {
                sig.View.Stop();
                sig.View = null;
            }
            var tip = SkillOrigin;
            foreach (var turn in new[] { -18f, 0f, 18f })
            {
                var wave = SigBolt.Shoot(this, C("B"), tip, Quaternion.Euler(0, 0, turn) * SigAim, 15, D(turn == 0 ? 1.5f : .9f), .6f, 9, 7f);
                wave.Impact = C("Hit");
            }
            ActionShake(.1f);
            BeginActionMelee(MeleeAttackMotion.Sweep, .5f);
        }
    }
}
