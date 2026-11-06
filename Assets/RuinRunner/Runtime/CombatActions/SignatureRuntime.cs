using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Plays a signature weapon clip at native pixels. Presentation only.</summary>
    public sealed class SigFx : MonoBehaviour
    {
        const float DefaultFrameRate=30;
        SpriteRenderer view;Sprite[] frames;float age,life,rate,lineUnits=2;bool loop,followed;Transform follow;Vector2 offset;Func<Vector2> track;Func<float> trackTurn;
        public static SigFx Play(string clip,Vector2 point,float angle=0,float lifetime=-1,bool ground=false,bool mirrored=false,float scale=1)
        {
            var fx=Make(clip,point,angle,ground,mirrored,scale);
            Created?.Invoke(clip,fx.transform.position,ground);
            fx.life=lifetime>0?lifetime:fx.frames.Length/DefaultFrameRate;return fx;
        }
        public static SigFx Loop(string clip,Vector2 point,float angle=0,bool ground=false,float framesPerSecond=12,Transform follow=null,float scale=1,float expires=-1)
        {
            var fx=Make(clip,point,angle,ground,false,scale);
            Created?.Invoke(clip,fx.transform.position,ground);
            fx.loop=true;fx.rate=framesPerSecond;fx.follow=follow;fx.followed=follow!=null;fx.offset=follow==null?Vector2.zero:(Vector2)fx.transform.position-(Vector2)follow.position;fx.life=expires;return fx;
        }
        /// <summary>A strip clip stretched between two points (its native strip is two units long).</summary>
        public static SigFx Line(string clip,Vector2 from,Vector2 to,float lifetime=-1,bool ground=false,bool looping=false)
        {
            from=OffBody(from);
            Created?.Invoke(clip,from,ground);
            var fx=Make(clip,from,0,ground,false,1);
            fx.loop=looping;fx.rate=12;fx.life=looping?-1:lifetime>0?lifetime:fx.frames.Length/DefaultFrameRate;
            fx.SetLine(from,to);return fx;
        }
        /// <summary>A one-shot drawn at its full length, cut short by a wall in front of it rather than stretched past its native pixels.</summary>
        public static SigFx Reach(string clip,Vector2 from,Vector2 to,float nativeUnits)
        {
            var fx=Line(clip,from,to);fx.lineUnits=nativeUnits;fx.SetLine(from,to);return fx;
        }
        /// <summary>For a line clip drawn longer than the usual two-unit strip.</summary>
        public SigFx Native(float units){lineUnits=units;return this;}
        /// <summary>Moves with something that isn't a transform, like the tip of the weapon or the cursor.</summary>
        public SigFx Tracking(Func<Vector2> at,Func<float> turn=null){track=at;trackTurn=turn;transform.position=at();return this;}
        // Effects that stand up in the air never start inside the hero's body; they leave from the weapon. Floor marks stay where they were put.
        const float BodyRadius=.55f;
        internal static Vector2 OffBody(Vector2 point)
        {
            foreach(var hero in PlayerController.ActiveCombatants)
                if(hero!=null&&((Vector2)hero.transform.position-point).sqrMagnitude<BodyRadius*BodyRadius)return hero.SkillOrigin;
            return point;
        }
        // A big blast set off beside the hero shrinks until its edge stops short of them; one centred on the hero on purpose keeps its size.
        const float CoveringHalfWidth=1.2f,SmallestClearScale=.45f;
        static float ClearOfHero(Sprite frame,Vector2 point,float scale)
        {
            float halfWidth=frame.bounds.extents.x*scale;
            if(halfWidth<=CoveringHalfWidth)return 1;
            foreach(var hero in PlayerController.ActiveCombatants)
            {
                if(hero==null)continue;
                float distance=Vector2.Distance(hero.transform.position,point);
                if(distance>.1f&&distance<halfWidth)return Mathf.Max(SmallestClearScale,(distance-.2f)/halfWidth);
            }
            return 1;
        }
        internal static System.Action<string,Vector2,bool> Created;
        static SigFx Make(string clip,Vector2 point,float angle,bool ground,bool mirrored,float scale)
        {
            if(!ground)point=OffBody(point);
            var root=new GameObject("SigFx_"+clip);
            root.transform.SetPositionAndRotation(point,Quaternion.Euler(0,0,angle));
            var fx=root.AddComponent<SigFx>();fx.frames=NativeCombatEffect.Frames(clip);
            if(!ground)scale*=ClearOfHero(fx.frames[0],point,scale);
            root.transform.localScale=Vector3.one*scale;
            fx.view=root.AddComponent<SpriteRenderer>();fx.view.sprite=fx.frames[0];fx.view.flipY=mirrored;PixelFxMaterial.Apply(fx.view,clip);
            if(ground){fx.view.sortingLayerName="WorldFloor";fx.view.sortingOrder=12;}else fx.view.sortingOrder=2003;
            return fx;
        }
        public void SetLine(Vector2 from,Vector2 to)
        {
            from=OffBody(from);to=OffBody(to);
            var delta=to-from;
            transform.SetPositionAndRotation(from,Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg));
            transform.localScale=new Vector3(Mathf.Max(.05f,delta.magnitude/lineUnits),1,1);
        }
        public void SetClip(string clip){frames=NativeCombatEffect.Frames(clip);age=0;}
        public void SetScale(float scale)=>transform.localScale=Vector3.one*scale;
        public void Stop(){if(this!=null&&gameObject!=null)Destroy(gameObject);}
        void Update()
        {
            age+=Time.deltaTime;
            if(life>0&&age>=life){Destroy(gameObject);return;}
            // A mark on something that no longer exists has nothing left to mark.
            if(followed&&follow==null){Destroy(gameObject);return;}
            if(follow!=null)transform.position=(Vector2)follow.position+offset;
            if(track!=null)transform.position=track();
            if(trackTurn!=null)transform.rotation=Quaternion.Euler(0,0,trackTurn());
            view.sprite=loop?frames[Mathf.FloorToInt(age*rate)%frames.Length]:frames[Mathf.Min(frames.Length-1,(int)(age/life*frames.Length))];
        }
    }

    /// <summary>A ghost of the hero that stands where the hero once cut and plays one blow of the katana combo with the weapon in hand.</summary>
    public sealed class SigPhantom : MonoBehaviour
    {
        SpriteRenderer body,blade;Transform pivot;int step,facing;float age,seconds,aimAngle,forward;Color tint;
        public static void Swing(Vector2 feet,Vector2 aim,int step,WeaponDefinition weapon,Color tint,float seconds=.5f)
        {
            var root=new GameObject("SigPhantom");root.transform.position=feet;
            var ghost=root.AddComponent<SigPhantom>();ghost.step=step;ghost.seconds=seconds;ghost.tint=tint;
            ghost.facing=aim.x>=0?(aim.y>=0?0:2):(aim.y<0?4:6);ghost.aimAngle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            var figure=new GameObject("Body");figure.transform.SetParent(root.transform,false);figure.transform.localPosition=new Vector3(0,-.28f,0);
            ghost.body=figure.AddComponent<SpriteRenderer>();ghost.body.sortingOrder=2001;
            var hand=new GameObject("Hand");hand.transform.SetParent(root.transform,false);ghost.pivot=hand.transform;
            ghost.blade=hand.AddComponent<SpriteRenderer>();ghost.blade.sprite=PixelArtAtlas.GetHeldWeaponSprite(weapon);ghost.blade.sortingOrder=2000;ghost.blade.flipY=aim.x<0;
            ghost.forward=(NativeWeaponArt.TryGet(weapon.Id,out var pose)?pose.ForwardAngle:45f)*(aim.x<0?-1:1);
            ghost.Pose();
        }
        void Update(){age+=Time.deltaTime;if(age>=seconds){Destroy(gameObject);return;}Pose();}
        void Pose()
        {
            var k=age/seconds;
            body.sprite=KatanaComboFrames.Sample(step,facing,k,out var hand,out var bladeAngle,out _);
            pivot.localPosition=new Vector3((hand.x-24)/32f,(42-hand.y)/32f-.28f,0);
            pivot.localRotation=Quaternion.Euler(0,0,aimAngle-forward+bladeAngle);
            var alpha=tint.a*(1-k*k*k);var color=new Color(tint.r,tint.g,tint.b,alpha);body.color=color;blade.color=color;
        }
    }

    /// <summary>Runs delayed skill effects independently of the player's own action, so lit fuses and queued strikes survive a dash.</summary>
    public sealed class SigRunner : MonoBehaviour
    {
        public void After(float delay,Action action)=>StartCoroutine(Wait(delay,action));
        IEnumerator Wait(float delay,Action action){yield return new WaitForSeconds(delay);action();}
    }

    /// <summary>A signature projectile: hits each target once per leg, can pierce, bounce off walls or fly back.</summary>
    public sealed class SigBolt : MonoBehaviour
    {
        PlayerController owner;Vector2 velocity;float damage,radius,travel,range;int pierce,action;bool direct,critical,returning,done;
        readonly HashSet<EnemyController> struck=new HashSet<EnemyController>();
        public string Impact,Wall;
        public int Bounces,BouncesDone;
        public bool ReturnsToOwner;
        public string ReturnClip;
        public Action<EnemyController,Vector2> OnHit;
        public Action<Vector2,bool> OnEnd;       // point, ended on a wall
        public Action<SigBolt> OnMove;
        public Action OnCaught;
        public Vector2 Direction=>velocity.normalized;
        public float Speed=>velocity.magnitude;
        public bool Returning=>returning;
        public float Damage{get=>damage;set=>damage=value;}
        public bool Teleported;
        public bool ItemProc; // Thrown by a passive: its hit counts as neither an attack nor a new passive.
        public float HomingTurn,HomingRange=6; // Degrees per second towards the nearest target it hasn't hit yet.
        SigFx view;

        public static SigBolt Shoot(PlayerController owner,string clip,Vector2 from,Vector2 direction,float speed,float damage,float radius=.22f,int pierce=1,float range=11,bool direct=false,bool critical=false,int action=0)
        {
            from=SigFx.OffBody(from);
            var root=new GameObject("SigBolt");root.transform.position=from;
            var bolt=root.AddComponent<SigBolt>();
            bolt.owner=owner;bolt.velocity=direction.normalized*speed;bolt.damage=damage;bolt.radius=radius;bolt.pierce=pierce;bolt.range=range;bolt.direct=direct;bolt.critical=critical;bolt.action=action;
            bolt.view=SigFx.Loop(clip,from,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,false,15);
            return bolt;
        }
        public void SetClip(string clip){if(view!=null)view.SetClip(clip);}
        public void Finish()=>End(transform.position,false,true);
        public void MoveTo(Vector2 point,Vector2 direction)
        {transform.position=point;velocity=direction.normalized*velocity.magnitude;Sync();}
        void Sync()
        {
            if(view==null)return;
            view.transform.SetPositionAndRotation(transform.position,Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg));
        }
        void Update()
        {
            if(done)return;
            if(owner==null||owner.ItemEffectsSuspended){End((Vector2)transform.position,false,true);return;}
            if(HomingTurn>0&&!returning)Steer();
            var step=velocity*Time.deltaTime;var from=(Vector2)transform.position;
            var wall=Physics2D.CircleCast(from,radius*.6f,step.normalized,step.magnitude,1<<GameLayers.Ground);
            if(wall.collider!=null&&!returning)
            {
                EnvironmentProp.HitBody(wall.collider);
                if(BouncesDone<Bounces)
                {
                    BouncesDone++;velocity=Vector2.Reflect(velocity,wall.normal);transform.position=wall.point+wall.normal*(radius*.7f);struck.Clear();
                    if(Wall!=null)SigFx.Play(Wall,wall.point,Mathf.Atan2(wall.normal.y,wall.normal.x)*Mathf.Rad2Deg);
                    Sync();return;
                }
                if(ReturnsToOwner){BeginReturn();return;}
                End(wall.point+wall.normal*.05f,true,false);return;
            }
            transform.position=from+step;travel+=step.magnitude;Sync();
            OnMove?.Invoke(this);if(done)return;
            foreach(var col in Physics2D.OverlapCircleAll(transform.position,radius,1<<GameLayers.Enemy))
            {
                var enemy=EnemyController.DamageTarget(col);
                if(enemy==null||enemy.IsDead||!struck.Add(enemy))continue;
                var point=col.ClosestPoint(transform.position);
                if(ItemProc){owner.ItemEffects.ApplySecondaryDamage(enemy,damage,Direction*3.5f);if(Impact!=null)SigFx.Play(Impact,point);}
                else owner.SigBoltHit(enemy,damage,Direction*3.5f,direct,critical,action,Impact,point);
                OnHit?.Invoke(enemy,point);
                if(--pierce<=0&&!ReturnsToOwner){End(point,false,false);return;}
            }
            if(returning)
            {
                var home=(Vector2)owner.transform.position-(Vector2)transform.position;
                velocity=home.normalized*velocity.magnitude;
                if(home.magnitude<.45f){OnCaught?.Invoke();End(transform.position,false,true);}
                return;
            }
            if(travel>=range){if(ReturnsToOwner)BeginReturn();else End(transform.position,false,false);}
        }
        void Steer()
        {
            var here=(Vector2)transform.position;
            foreach(var enemy in CombatActionGeometry.Circle(here,HomingRange))
            {
                if(enemy==null||enemy.IsDead||struck.Contains(enemy))continue;
                var heading=Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg;var to=(Vector2)enemy.transform.position-here;
                var turned=Mathf.MoveTowardsAngle(heading,Mathf.Atan2(to.y,to.x)*Mathf.Rad2Deg,HomingTurn*Time.deltaTime)*Mathf.Deg2Rad;
                velocity=new Vector2(Mathf.Cos(turned),Mathf.Sin(turned))*velocity.magnitude;
                return;
            }
        }
        void BeginReturn()
        {
            returning=true;struck.Clear();
            if(ReturnClip!=null&&view!=null)view.SetClip(ReturnClip);
        }
        void End(Vector2 point,bool onWall,bool silent)
        {
            done=true;if(view!=null)view.Stop();
            if(!silent)OnEnd?.Invoke(point,onWall);
            Destroy(gameObject);
        }
    }

    /// <summary>Slow, root and damage over time on one enemy. Applied after the enemy has chosen its own velocity.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class EnemyStatus : MonoBehaviour
    {
        EnemyController enemy;Rigidbody2D body;float slowUntil,rootUntil,slowFactor=1,clock;
        readonly List<Dot> dots=new List<Dot>();
        sealed class Dot{public PlayerController Owner;public float Damage,Next,Until;public string Tag;public SigFx View;}
        public static EnemyStatus On(EnemyController enemy)
        {
            var status=enemy.GetComponent<EnemyStatus>();if(status!=null)return status;
            status=enemy.gameObject.AddComponent<EnemyStatus>();status.enemy=enemy;status.body=enemy.GetComponent<Rigidbody2D>();return status;
        }
        public void Slow(float factor,float seconds){slowFactor=clock<slowUntil?Mathf.Min(slowFactor,factor):factor;slowUntil=Mathf.Max(slowUntil,clock+seconds);}
        public void Root(float seconds){if(enemy.IsBoss){Slow(.5f,seconds);return;}rootUntil=Mathf.Max(rootUntil,clock+seconds);}
        public void AddDot(PlayerController owner,string tag,float damagePerTick,float seconds,string clip=null)
        {
            foreach(var dot in dots)if(dot.Tag==tag){dot.Until=clock+seconds;dot.Damage=Mathf.Max(dot.Damage,damagePerTick);return;}
            dots.Add(new Dot{Owner=owner,Tag=tag,Damage=damagePerTick,Next=clock+.5f,Until=clock+seconds,
                View=clip==null?null:SigFx.Loop(clip,(Vector2)transform.position+Vector2.up*.15f,0,false,12,transform,1,seconds)});
        }
        void LateUpdate()
        {
            if(enemy==null||enemy.IsDead||enemy.AnimationPaused)return;
            clock+=Time.deltaTime;
#if UNITY_6000_0_OR_NEWER
            if(clock<rootUntil)body.linearVelocity=Vector2.zero;else if(clock<slowUntil)body.linearVelocity*=slowFactor;
#else
            if(clock<rootUntil)body.velocity=Vector2.zero;else if(clock<slowUntil)body.velocity*=slowFactor;
#endif
            for(int i=dots.Count-1;i>=0;i--)
            {
                var dot=dots[i];
                if(clock>=dot.Until){if(dot.View!=null)dot.View.Stop();dots.RemoveAt(i);continue;}
                if(clock<dot.Next||dot.Owner==null)continue;
                dot.Next=clock+.5f;enemy.MuteNextHitFx();dot.Owner.SecondaryHit(enemy,dot.Damage,Vector2.zero);if(enemy==null||enemy.IsDead)return;
            }
        }
        void OnDestroy(){foreach(var dot in dots)if(dot.View!=null)dot.View.Stop();}
    }

    /// <summary>The floating orb of the Orb family: circles the player, strikes on command, can be pinned in the world.</summary>
    public sealed class SigOrb : MonoBehaviour
    {
        const float StrikeSeconds=.3f;
        PlayerController owner;SpriteRenderer view;float angle,strikeAge=-1,strikeReach,contactTimer;Vector2 strikeDir;
        readonly HashSet<EnemyController> struck=new HashSet<EnemyController>();
        readonly Dictionary<EnemyController,float> touched=new Dictionary<EnemyController,float>();
        public float Radius=1.05f,Spin=150;
        public float ContactUntil,ContactDamage;public string ContactHit;
        public bool Pinned;public Vector2 PinPoint;
        public float Damage;public Action<EnemyController,Vector2> OnStrikeHit;public int Action;public bool Critical;
        public string Impact;public Action<Vector2> OnMoved;
        public Vector2 Position=>transform.position;

        public static SigOrb Create(PlayerController owner,Sprite sprite,float startAngle=0)
        {
            var root=new GameObject("SigOrb");root.transform.position=owner.transform.position;
            var orb=root.AddComponent<SigOrb>();orb.owner=owner;orb.angle=startAngle;
            orb.view=root.AddComponent<SpriteRenderer>();orb.view.sprite=sprite;
            return orb;
        }
        public void UsePixelFxMaterial()=>view.sharedMaterial=PixelFxMaterial.Shared;
        public void Strike(Vector2 direction,float reach){strikeDir=direction.normalized;strikeReach=reach;strikeAge=0;struck.Clear();}
        public void Pin(Vector2 point){Pinned=true;PinPoint=point;}
        public void Release(){Pinned=false;}
        void Update()
        {
            if(owner==null||owner.IsDead){Destroy(gameObject);return;}
            if(owner.AnimationPaused)return;
            var dt=Time.deltaTime;angle+=Spin*dt;
            var center=Pinned?PinPoint:(Vector2)owner.transform.position+Vector2.up*.05f;
            var rest=Pinned?center:center+new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*Radius;
            var next=rest;
            if(strikeAge>=0)
            {
                strikeAge+=dt;var k=strikeAge/StrikeSeconds;
                if(k>=1)strikeAge=-1;
                else
                {
                    var far=center+strikeDir*strikeReach;
                    var wall=Physics2D.Linecast(center,far,1<<GameLayers.Ground);if(wall.collider!=null)far=wall.point+wall.normal*.15f;
                    next=k<.42f?Vector2.Lerp(rest,far,Mathf.SmoothStep(0,1,k/.42f)):Vector2.Lerp(far,rest,Mathf.SmoothStep(0,1,(k-.42f)/.58f));
                    foreach(var col in Physics2D.OverlapCircleAll(next,.42f,1<<GameLayers.Enemy))
                    {
                        var enemy=EnemyController.DamageTarget(col);
                        if(enemy==null||enemy.IsDead||!struck.Add(enemy))continue;
                        var point=col.ClosestPoint(next);
                        owner.SigBoltHit(enemy,Damage,strikeDir*3,true,Critical,Action,Impact,point);
                        OnStrikeHit?.Invoke(enemy,point);
                    }
                }
            }
            if(ContactUntil>Time.time)
            {
                foreach(var col in Physics2D.OverlapCircleAll(next,.46f,1<<GameLayers.Enemy))
                {
                    var enemy=EnemyController.DamageTarget(col);
                    if(enemy==null||enemy.IsDead)continue;
                    if(touched.TryGetValue(enemy,out var ready)&&Time.time<ready)continue;
                    touched[enemy]=Time.time+.4f;
                    owner.SigBoltHit(enemy,ContactDamage,(next-center).normalized*2,false,false,0,ContactHit,col.ClosestPoint(next));
                    OnStrikeHit?.Invoke(enemy,next);
                }
            }
            transform.position=next;
            OnMoved?.Invoke(next);
            view.sortingOrder=next.y>owner.transform.position.y?owner.BodyRenderer.sortingOrder-1:owner.BodyRenderer.sortingOrder+1;
        }
    }
}
