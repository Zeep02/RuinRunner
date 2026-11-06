using UnityEngine;

namespace RuinRunner
{
    /// <summary>How a weapon's skill changes the player's own attacks, shots and blocks.</summary>
    public sealed partial class PlayerController
    {
        // ---- reactions to the player's own attacks
        float SigMeleeMult()
        {
            if (sig == null)
                return 1;
            if (sig.Id == "acrobat_knife" && combatClock < sig.Until)
            {
                sig.Until = 0;
                return 1.6f;
            }
            return 1;
        }
        float SigDelayMult()
        {
            if (sig == null)
                return 1;
            if (sig.Id == "acrobat_knife" && combatClock < sig.Until)
                return .5f;
            if (sig.Id == "wind_cutter" && sig.Stage == 1)
                return .55f;
            return 1;
        }
        void SigOnContact(Vector2 center, float angle, MeleeAttackStep step, bool heavy)
        {
            if (sig == null)
                return;
            var aim = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var from = (Vector2)transform.position;
            var hit = C("Hit");
            switch (sig.Id)
            {
                case "merc_slab":
                    if (heavy)
                    {
                        SigFx.Play(C("A"), center, 0, .5f);
                        ActionShake(.1f);
                    }
                    break;
                case "lumber_blade":
                    if (combatClock < sig.Until)
                        SigFx.Play(C("A"), center, angle, .5f);
                    break;
                case "dawn_gate":
                    if (combatClock < sig.Until)
                    {
                        sig.Until = 0;
                        var dir = (center - sig.Point).normalized;
                        SigSeg(sig.Point, sig.Point + dir * 4.4f, .8f, D(.9f), C("C"), hit, 3);
                    }
                    break;
                case "thousand_returns":
                    sig.Points.Add(center);
                    sig.Angles.Add(angle);
                    sig.Views.Add(SigFx.Loop(C("A"), center, angle, false, 12, null, 1, 6));
                    if (sig.Points.Count > 3)
                    {
                        sig.Points.RemoveAt(0);
                        sig.Angles.RemoveAt(0);
                        if (sig.Views[0] != null)
                            sig.Views[0].Stop();
                        sig.Views.RemoveAt(0);
                    }
                    break;
                case "thunder_needle":
                    if (sig.Stage == 1 && step.Motion == MeleeAttackMotion.Thrust && Vector2.Distance(from + aim * 2.2f, sig.Point) < 1.3f)
                    {
                        sig.Stage = 0;
                        var at = sig.Point;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                        SigFx.Line(C("B"), from + aim * 1.6f, at, .3f);
                        SigFx.Play(C("C"), at, 0, -1, true);
                        SigFx.Play(C("D"), at, 0, -1);
                        SigArea(at, 2.5f, D(1.35f), hit, 3, .16f);
                    }
                    break;
                case "polaris":
                    if (sig.Stage == 1 && step.Motion == MeleeAttackMotion.Thrust && Vector2.Distance(from + aim * 2.1f, sig.Point) < 1.5f)
                    {
                        SigFx.Play(C("B"), sig.Point, 0, .3f);
                        SigLightSpear(sig.Point, aim, D(.85f));
                    }
                    break;
            }
        }
        void SigOnMeleeHit(EnemyController enemy, float damage, bool heavy, MeleeAttackMotion motion)
        {
            if (sig == null || enemy == null)
                return;
            var hit = C("Hit");
            switch (sig.Id)
            {
                case "alley_awl":
                    if (combatClock < sig.Until)
                    {
                        sig.Until = 0;
                        bool behind = enemy.FacingDirection * (transform.position.x - enemy.transform.position.x) < 0;
                        if (behind)
                            SigHit(enemy, D(1f), Vector2.zero, C("A"));
                    }
                    break;
                case "saw_fang":
                    if (combatClock < sig.Until)
                        EnemyStatus.On(enemy).AddDot(this, "bleed", D(.13f), 3, C("A"));
                    break;
                case "frost_fang":
                    if (sig.Stage == 1 && sig.Target == null)
                    {
                        sig.Target = enemy;
                        EnemyStatus.On(enemy).Slow(.5f, 2.2f);
                        if (sig.View != null)
                            sig.View.Stop();
                        sig.View = SigFx.Loop(C("A"), (Vector2)enemy.transform.position + Vector2.up * .2f, 0, false, 12, enemy.transform);
                    }
                    else if (sig.Stage == 2 && enemy == sig.Target)
                    {
                        sig.Stage = 0;
                        sig.Target = null;
                        if (sig.View != null)
                        {
                            sig.View.Stop();
                            sig.View = null;
                        }
                        SigFx.Play(C("B"), (Vector2)enemy.transform.position + Vector2.up * .15f, 0, .55f);
                        SigHit(enemy, D(1.2f), Vector2.zero);
                        ActionShake(.1f);
                    }
                    break;
                case "viper_tongue":
                    if (sig.Stage == 1)
                    {
                        sig.Stage = 0;
                        sig.Target = enemy;
                        sig.Count = 0;
                        if (sig.View != null)
                            sig.View.Stop();
                        sig.View = SigFx.Loop(C("A"), (Vector2)enemy.transform.position + Vector2.up * .55f, 0, false, 12, enemy.transform);
                    }
                    else if (sig.Target == enemy)
                    {
                        sig.Count++;
                        sig.View?.SetScale(1 + sig.Count * .45f);
                        if (sig.Count >= 3)
                        {
                            var at = (Vector2)enemy.transform.position;
                            sig.Target = null;
                            if (sig.View != null)
                            {
                                sig.View.Stop();
                                sig.View = null;
                            }
                            SigFx.Play(C("B"), at, 0, .7f);
                            foreach (var near in SigArea(at, 1.95f, D(1f), hit, 3, .12f))
                                EnemyStatus.On(near).AddDot(this, "poison", D(.16f), 3, C("C"));
                        }
                    }
                    break;
                case "blood_bank":
                    {
                        sig.A = Mathf.Min(10, sig.A + 1);
                        var clip = C("A" + Mathf.Clamp(Mathf.CeilToInt(sig.A / 2), 1, 5));
                        if (sig.View == null)
                            sig.View = SigFx.Loop(clip, (Vector2)transform.position + new Vector2(.5f, .1f), 0, false, 12, transform);
                        else
                            sig.View.SetClip(clip);
                        break;
                    }
                case "night_thread":
                    if (!sig.Marks.Contains(enemy) && sig.Marks.Count < 4)
                    {
                        sig.Marks.Add(enemy);
                        sig.Views.Add(SigFx.Loop(C("A"), (Vector2)enemy.transform.position + Vector2.up * .5f, 0, false, 12, enemy.transform));
                    }
                    break;
                case "lumber_blade":
                    if (combatClock < sig.Until)
                    {
                        var side = new Vector2(-SigAim.y, SigAim.x);
                        SigPull(enemy, (Vector2)enemy.transform.position + side, 10);
                    }
                    break;
                case "merc_slab":
                    if (heavy)
                        enemy.Stagger(.6f);
                    break;
                case "thunder_slug":
                    if (sig.Stage == 1 && sig.Target == null && combatClock < sig.Until)
                        sig.Target = enemy;
                    break;
                case "executioner_scale":
                    if (sig.Stage == 2)
                    {
                        var ratio = 1 - enemy.Health / Mathf.Max(1, enemy.MaxHealth);
                        SigFx.Line(C("B"), (Vector2)enemy.transform.position + new Vector2(-1, .8f), (Vector2)enemy.transform.position + new Vector2(1, -.8f), .34f);
                        SigHit(enemy, D(2.3f * ratio + .3f), Vector2.zero);
                        if (enemy.IsDead)
                        {
                            sigReady = Mathf.Min(sigReady, combatClock + .6f);
                            SigFx.Play(C("C"), enemy.transform.position, 0, .6f);
                        }
                    }
                    break;
                case "sentry_spear":
                    if (combatClock < sig.Until && Vector2.Distance(transform.position, enemy.transform.position) > 1.35f)
                    {
                        enemy.Stagger(.7f);
                        SigFx.Play(C("A"), (Vector2)enemy.transform.position + Vector2.up * .2f);
                    }
                    break;
            }
        }
        // Every family's ranged LMB passes here so a weapon can change its bolt.
        void SigDressShot(SigBolt bolt)
        {
            if (CurrentMutation == EquipmentMutation.Ricochet)
                bolt.Bounces = Mathf.Max(bolt.Bounces, 1);
            bolt.Impact = C("Hit");
            bolt.OnHit = (enemy, point) => SigShotLanded(enemy, point, false);
            switch (sig.Id)
            {
                case "frost_bolt_rifle":
                    if (sig.Count > 0)
                    {
                        sig.Count--;
                        bolt.SetClip(C("B"));
                        bolt.OnHit = (enemy, point) =>
                        {
                            if (sig == null || sig.Id != "frost_bolt_rifle")
                                return;
                            sig.Stacks.TryGetValue(enemy, out var count);
                            count++;
                            sig.Stacks[enemy] = count;
                            EnemyStatus.On(enemy).Slow(.75f - .15f * count, 2.5f);
                            if (count >= 3)
                            {
                                sig.Stacks.Remove(enemy);
                                SigFx.Play(C("C"), (Vector2)enemy.transform.position + Vector2.up * .15f, 0, .55f);
                                SigHit(enemy, D(1.5f), Vector2.zero);
                                ActionShake(.1f);
                            }
                            else
                                SigFx.Loop(C("D" + count), (Vector2)enemy.transform.position + Vector2.up * .5f, 0, false, 12, enemy.transform, 1, 1.2f);
                        };
                    }
                    break;
                case "refractor_staff":
                    if (sig.Stage == 1)
                    {
                        var prism = sig.Point;
                        bolt.OnMove = b =>
                        {
                            if (b.Teleported || Vector2.Distance(b.transform.position, prism) > .55f)
                                return;
                            b.Teleported = true;
                            SigFx.Play(C("B"), prism, 0, .2f);
                            var dir = b.Direction;
                            var clips = new[] { C("Shot"), C("C"), C("D") };
                            for (int i = -1; i <= 1; i++)
                            {
                                var split = SigBolt.Shoot(this, clips[i + 1], prism, Quaternion.Euler(0, 0, i * 20) * dir, b.Speed, b.Damage * .8f, .2f, 1, 8, true, false, attackAnimationSequence);
                                split.Impact = C("Hit");
                            }
                            b.Damage = 0;
                            b.MoveTo(prism, dir);
                            b.Finish();
                        };
                    }
                    break;
            }
            if (!shotPlantsRound)
                return;
            var landed = bolt.OnHit;
            var ended = bolt.OnEnd;
            var plantAction = attackAnimationSequence;
            var plantWeapon = CurrentWeaponIdentity;
            var plantDamage = Damage * .6f;
            bolt.OnHit = (enemy, point) =>
            {
                landed?.Invoke(enemy, point);
                PlantAwakenedRound(point, plantDamage, plantAction, plantWeapon);
            };
            bolt.OnEnd = (point, wall) =>
            {
                ended?.Invoke(point, wall);
                PlantAwakenedRound(point, plantDamage, plantAction, plantWeapon);
            };
        }
        void SigShotLanded(EnemyController enemy, Vector2 point, bool orbStrike)
        {
            if (sig == null || enemy == null)
                return;
            switch (sig.Id)
            {
                case "ember_heart":
                    {
                        sig.A = Mathf.Min(10, sig.A + 1);
                        var clip = C("A" + Mathf.Clamp(Mathf.CeilToInt(sig.A / 2), 1, 5));
                        if (sig.View == null)
                            sig.View = SigFx.Loop(clip, orb.Position, 0, false, 12, orb.transform);
                        else
                            sig.View.SetClip(clip);
                        break;
                    }
            }
        }
        void SigOrbSetup()
        {
            orb.ContactHit = C("Hit");
            if (sig.Id == "twin_star")
            {
                orbTwo = SigOrb.Create(this, NativeCombatEffect.Frames(C("Shot"))[0], 180);
                orbTwo.UsePixelFxMaterial();
                orbTwo.Radius = 1.5f;
                orbTwo.Spin = -190;
                orbTwo.Impact = C("Hit");
                orbTwo.ContactHit = C("Hit");
                orb.OnStrikeHit = (enemy, point) => SigTwinMark(enemy, 0);
                orbTwo.OnStrikeHit = (enemy, point) => SigTwinMark(enemy, 1);
            }
        }
        void SigTwinMark(EnemyController enemy, int side)
        {
            if (sig == null || enemy == null)
                return;
            sig.Stacks.TryGetValue(enemy, out var bits);
            bits |= 1 << side;
            sig.Stacks[enemy] = bits;
            if (bits == 3)
            {
                sig.Stacks.Remove(enemy);
                SigFx.Play(C("C"), (Vector2)enemy.transform.position + Vector2.up * .15f, 0, .42f);
                SigHit(enemy, D(1f), Vector2.zero);
            }
            else
                SigFx.Loop(C(side == 0 ? "A" : "B"), (Vector2)enemy.transform.position + Vector2.up * .55f, 0, false, 12, enemy.transform, 1, 1.4f);
        }
        void SigOrbStrikeStarted(Vector2 direction, float damage, bool critical)
        {
            switch (sig.Id)
            {
                case "twin_star":
                    orbTwo.Damage = damage * .8f;
                    orbTwo.Action = attackAnimationSequence;
                    orbTwo.Strike(direction, 2.9f);
                    break;
                case "mirror_eye":
                    if (sig.Stage == 1)
                        SigMirrorVolley(damage * .6f);
                    break;
            }
        }
        void SigLob(Vector2 direction, float damage)
        {
            var target = AimPoint(6.5f);
            var impact = C("Hit");
            var mark = sig;
            var origin = committedShotOrigin;
            var shell = SigBolt.Shoot(this, C("Shot"), origin, target - origin, 11, damage * .4f, .2f, 1, Vector2.Distance(origin, target));
            shell.Impact = null;
            shell.OnEnd = (point, wall) =>
            {
                SigFx.Play(impact, point, 0, .6f);
                SigArea(point, 1.25f, damage, null, 3, .1f);
                if (sig == null || sig != mark)
                    return;
                sig.Points.Add(point);
                if (sig.Points.Count > 3)
                {
                    sig.Points.RemoveAt(0);
                    if (sig.Views.Count > 0)
                    {
                        if (sig.Views[0] != null)
                            sig.Views[0].Stop();
                        sig.Views.RemoveAt(0);
                    }
                }
                foreach (var view in sig.Views)
                    if (view != null)
                        view.Stop();
                sig.Views.Clear();
                for (int i = 0; i < sig.Points.Count; i++)
                    sig.Views.Add(SigFx.Loop(C("A" + (i + 1)), sig.Points[i], 0, true));
            };
        }
        bool SigTryBlock(Vector2 direction)
        {
            sigHurt = true;
            if (sig == null || sig.Id != "warden_door" || sig.Stage != 1)
                return false;
            if (direction.sqrMagnitude < .001f || Vector2.Dot(SigAim, -direction.normalized) < .25f)
                return false;
            sig.Flag = true;
            invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(.2f));
            SigFx.Play(C("A"), SkillOrigin, SigAngle);
            OnActiveDefense();
            return true;
        }
        void SigParried()
        {
            if (sig == null)
                return;
            SigFx.Play(C("A"), SkillOrigin, SigAngle, .42f);
        }
        bool SigCatchProjectile(Vector2 point)
        {
            if (sig == null || sig.Id != "world_pendulum" || sig.Stage != 1 || combatClock >= sig.Until || sig.Count >= 5)
                return false;
            if (Vector2.Distance(point, transform.position) > 2.3f)
                return false;
            sig.Count++;
            sig.Views.Add(SigFx.Loop(C("A"), point, 0, false));
            SigFx.Play(C("D"), point, 0, .3f);
            return true;
        }
        // Gun-side rules a shot must respect. False holds the shot.
        bool SigShotAllowed()
        {
            if (sig == null)
                return true;
            if (sig.Holding)
                return false;
            if (sig.Id == "siege_engineer" && sig.Flag)
                return false;
            return true;
        }
        float SigShotDelayMult()
        {
            if (sig == null)
                return 1;
            if (sig.Id == "siege_engineer" && sig.Stage == 1)
                return .34f;
            return 1;
        }
        void SigShotFired(Vector2 direction)
        {
            if (sig == null || sig.Id != "siege_engineer" || sig.Stage != 1)
                return;
            sig.A += .055f;
            SigFx.Play(C("A"), (Vector2)transform.position + new Vector2(-direction.x * .2f, .15f), UnityEngine.Random.Range(0, 360f), .3f);
            if (sig.View == null && sig.A > .6f)
                sig.View = SigFx.Loop(C("B"), committedShotOrigin, 0, false, 12, transform);
            if (sig.A >= 1)
            {
                sig.Flag = true;
                sig.Until2 = combatClock + 1.6f;
            }
        }
    }
}
