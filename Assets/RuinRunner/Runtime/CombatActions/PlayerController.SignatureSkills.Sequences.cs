using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Multi-step pieces the skills start: chained cuts, volleys, rewinds and summons.</summary>
    public sealed partial class PlayerController
    {
        // One draw of the blade: a step to the cursor side and a line left hanging in the air. The last one sets them all off.
        void SigInstantCut(int step)
        {
            var from = (Vector2)transform.position;
            var dir = SigAim;
            var end = Ahead(3.7f);
            var line = C("B");
            var hit = C("Hit");
            SigSwing(MeleeAttackMotion.Sweep, .2f, step);
            SigDash(dir, Vector2.Distance(from, end), .07f, null, () =>
            {
                if (sig == null || sig.Id != "instant_king")
                    return;
                var to = (Vector2)transform.position;
                SigSeg(from, to, .75f, D(.7f), line, hit, 1);
                sig.Points.Add(from);
                sig.Points.Add(to);
                sig.Views.Add(SigFx.Line(C("E"), from, to, -1, false, true));
                ActionShake(.08f);
            });
        }
        void SigInstantShatter()
        {
            var points = new List<Vector2>(sig.Points);
            sig.Points.Clear();
            sig.Count = 0;
            var views = new List<SigFx>(sig.Views);
            sig.Views.Clear();
            var flash = C("B");
            var shatter = C("C");
            var hit = C("Hit");
            CameraFollow.MeleeHitStop?.Invoke(.08f, .05f);
            SigAfter(.1f, () =>
            {
                foreach (var view in views)
                    if (view != null)
                        view.Stop();
                for (int i = 0; i + 1 < points.Count; i += 2)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    SigSeg(a, b, 1.1f, D(1.5f), flash, hit, 4);
                    for (int k = 0; k < 3; k++)
                        SigFx.Play(shatter, Vector2.Lerp(a, b, (k + .5f) / 3), 0, -1);
                }
                ActionShake(.2f);
            });
        }
        void SigLightSpear(Vector2 from, Vector2 dir, float damage)
        {
            var end = CombatActionGeometry.Clip(from, dir, LightSpearUnits);
            SigFx.Reach(C("C"), from, end, LightSpearUnits);
            SigSeg(from, end, .75f, damage, null, C("Hit"), 2);
            ActionShake(.08f);
        }
        // Eight spears go out across the fan one after another and stick where they land; the chains go taut, then all of them reel in at once.
        IEnumerator SigChainFan(Vector2 aim, string hit)
        {
            const int Count = 8;
            const float Reach = 4.4f, Flight = .06f, Gap = .045f, Hold = .45f, Reel = .16f;
            var chainClip = C("A");
            var headClip = C("D");
            var bite = C("B");
            var yank = C("E");
            var seen = new HashSet<EnemyController>();
            var chains = new SigFx[Count];
            var heads = new SigFx[Count];
            var ends = new Vector2[Count];
            var dirs = new Vector2[Count];
            float clock = 0;
            bool Gone() => sig == null || sig.Id != "chain_horizon" || ItemEffectsSuspended;
            yield return new WaitForSeconds(.1f);
            for (int i = 0; i < Count; i++)
            {
                dirs[i] = (Vector2)(Quaternion.Euler(0, 0, 55 - i * 110f / (Count - 1)) * aim);
                ends[i] = CombatActionGeometry.Clip(transform.position, dirs[i], Reach);
            }
            var flown = new bool[Count];
            while (clock < Gap * Count + Flight && !Gone())
            {
                for (int i = 0; i < Count; i++)
                {
                    var start = Gap * i;
                    if (clock < start)
                        continue;
                    if (chains[i] == null && !flown[i])
                    {
                        chains[i] = SigFx.Line(chainClip, transform.position, transform.position, -1, false, true).Native(Reach);
                        heads[i] = SigFx.Loop(headClip, transform.position, Mathf.Atan2(dirs[i].y, dirs[i].x) * Mathf.Rad2Deg, false, 15);
                    }
                    var k = Mathf.Clamp01((clock - start) / Flight);
                    var from = (Vector2)transform.position;
                    var at = Vector2.Lerp(from, ends[i], k);
                    if (chains[i] != null)
                        chains[i].SetLine(from, at);
                    if (heads[i] != null)
                        heads[i].transform.position = at;
                    if (k >= 1 && !flown[i])
                    {
                        flown[i] = true;
                        SigFx.Play(bite, ends[i], Mathf.Atan2(dirs[i].y, dirs[i].x) * Mathf.Rad2Deg, -1);
                        ActionShake(.05f);
                        foreach (var enemy in CombatActionGeometry.Segment(from, ends[i], .75f))
                            if (seen.Add(enemy))
                            {
                                SigHit(enemy, D(.9f), Vector2.zero, hit);
                                EnemyStatus.On(enemy).Root(.6f);
                            }
                    }
                }
                clock += Time.deltaTime;
                yield return null;
            }
            for (float wait = 0; wait < Hold && !Gone(); wait += Time.deltaTime)
            {
                for (int i = 0; i < Count; i++)
                    if (chains[i] != null)
                        chains[i].SetLine(transform.position, ends[i]);
                yield return null;
            }
            // Reel in: whatever stands on a chain is dragged along with it.
            var dragged = new HashSet<EnemyController>();
            for (float t = 0; t < Reel && !Gone(); t += Time.deltaTime)
            {
                var from = (Vector2)transform.position;
                var k = t / Reel;
                k = k * k;
                for (int i = 0; i < Count; i++)
                {
                    var at = Vector2.Lerp(ends[i], from, k);
                    if (chains[i] != null)
                        chains[i].SetLine(from, at);
                    if (heads[i] != null)
                        heads[i].transform.position = at;
                    foreach (var enemy in CombatActionGeometry.Segment(from, at, .75f))
                        if (dragged.Add(enemy))
                        {
                            SigHit(enemy, D(.5f), Vector2.zero, hit);
                            SigPull(enemy, from, 12);
                        }
                }
                yield return null;
            }
            for (int i = 0; i < Count; i++)
            {
                if (chains[i] != null)
                    chains[i].Stop();
                if (heads[i] != null)
                    heads[i].Stop();
            }
            if (!Gone())
            {
                SigFx.Play(yank, SkillOrigin, 0, -1);
                ActionShake(.14f);
            }
        }
        // Runs each thread out one at a time: a step beside the enemy and a cut across it.
        IEnumerator SigSutureCuts(List<EnemyController> targets)
        {
            var line = C("B");
            var hit = C("Hit");
            foreach (var target in targets)
            {
                if (target == null || target.IsDead || sig == null || sig.Id != "shadow_suture")
                    continue;
                var c = (Vector2)target.transform.position;
                var from = (Vector2)transform.position;
                var to = (c - from).normalized;
                var side = new Vector2(-to.y, to.x);
                var at = c - to * 1f;
                SigSwing(MeleeAttackMotion.Sweep, .2f);
                SigDash((at - from).normalized, (at - from).magnitude, .09f, null, () =>
                {
                    if (target == null)
                        return;
                    var p = (Vector2)target.transform.position;
                    SigSeg(p + side * 1.6f, p - side * 1.6f, .8f, D(1.5f), line, hit, 3);
                    ActionShake(.1f);
                });
                yield return new WaitForSeconds(.2f);
            }
        }
        // Back along the way it came, with a cut on every stretch of the road. The clock stands where the hero was when the press came.
        IEnumerator SigRewind(List<Vector2> path, Vector2 last)
        {
            const float Seconds = .32f;
            var line = C("B");
            var hit = C("Hit");
            SigFx.Play(C("Cast"), last, 0, -1, true);
            invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(Seconds));
            float total = 0;
            for (int i = 1; i < path.Count; i++)
                total += Vector2.Distance(path[i - 1], path[i]);
            int leg = path.Count - 1;
            float walked = 0;
            for (float t = 0; t < Seconds && leg > 0 && !dead; t += Time.deltaTime)
            {
                var want = total * t / Seconds;
                while (leg > 0 && walked + Vector2.Distance(path[leg], path[leg - 1]) < want)
                {
                    walked += Vector2.Distance(path[leg], path[leg - 1]);
                    var a = path[leg];
                    var b = path[leg - 1];
                    SigSeg(a, b, .85f, D(.7f), line, hit, 1);
                    leg--;
                }
                if (leg > 0)
                {
                    var span = Vector2.Distance(path[leg], path[leg - 1]);
                    body.position = Vector2.Lerp(path[leg], path[leg - 1], span > 0 ? (want - walked) / span : 1);
                }
                GetComponent<DashAfterimageEffect>()?.Stamp();
                yield return null;
            }
            for (; leg > 0; leg--)
                SigSeg(path[leg], path[leg - 1], .85f, D(.7f), line, hit, 1);
            if (path.Count > 0)
                body.position = path[0];
        }
        // Mirrors circle the hero a third of a turn apart.
        Vector2 SigMirrorAt(int index)
        {
            var a = (combatClock * 150 + index * 120) * Mathf.Deg2Rad;
            return (Vector2)transform.position + Vector2.up * .15f + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.45f;
        }
        void SigMirrorVolley(float damage)
        {
            var target = AimPoint(30);
            var ray = C("B");
            var hit = C("Hit");
            for (int i = 0; i < sig.Views.Count; i++)
            {
                if (sig.Views[i] == null)
                    continue;
                var from = (Vector2)sig.Views[i].transform.position;
                SigFx.Play(C("F"), from, 0, -1);
                var end = CombatActionGeometry.Clip(from, target - from, LightSpearUnits);
                SigFx.Reach(ray, from, end, LightSpearUnits);
                SigSeg(from, end, .5f, damage, null, hit, 1);
            }
        }
        void SigMirrorBreak(bool throwShards)
        {
            var hit = C("Hit");
            var shard = C("D");
            for (int i = 0; i < sig.Views.Count; i++)
            {
                if (sig.Views[i] == null)
                    continue;
                var from = (Vector2)sig.Views[i].transform.position;
                SigFx.Play(C("C"), from, 0, -1);
                if (throwShards)
                    for (int k = 0; k < 3; k++)
                    {
                        var a = UnityEngine.Random.Range(0, 360f) * Mathf.Deg2Rad;
                        var piece = SigBolt.Shoot(this, shard, from, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 11, D(.5f), .26f, 1, 8);
                        piece.Impact = hit;
                        piece.HomingTurn = 540;
                        piece.HomingRange = 7;
                    }
            }
            sig.Stage = 0;
            sig.ClearViews();
        }
        // How far the eclipse reaches for how much of the hero's circling has gone into it, and how large it stands.
        float EclipseRadius(float turns) => 1.8f + turns;
        float EclipseScale(float turns) => .55f + turns * .4f;
        IEnumerator SigSouls(Vector2 target, string flameClip, string burstClip, string hit)
        {
            var start = SkillOrigin;
            var flames = new SigFx[3];
            var bends = new[] { 1.8f, -1.8f, 0f };
            var ends = new Vector2[3];
            var side = new Vector2(-SigAim.y, SigAim.x);
            for (int i = 0; i < 3; i++)
            {
                flames[i] = SigFx.Loop(flameClip, start, 0, false);
                ends[i] = target + side * (i - 1) * .45f;
            }
            for (float t = 0; t < .55f; t += Time.deltaTime)
            {
                var k = t / .55f;
                for (int i = 0; i < 3; i++)
                    if (flames[i] != null)
                    {
                        var control = (start + ends[i]) * .5f + side * bends[i];
                        flames[i].transform.position = (1 - k) * (1 - k) * start + 2 * (1 - k) * k * control + k * k * ends[i];
                    }
                yield return null;
            }
            yield return new WaitForSeconds(.28f);
            for (int i = 0; i < 3; i++)
            {
                if (flames[i] != null)
                    flames[i].Stop();
                if (!ItemEffectsSuspended)
                {
                    SigFx.Play(burstClip, ends[i], 0, .65f);
                    SigArea(ends[i], 1.15f, D(.85f), hit, 3, .08f);
                }
                yield return new WaitForSeconds(.24f);
            }
        }
    }
}
