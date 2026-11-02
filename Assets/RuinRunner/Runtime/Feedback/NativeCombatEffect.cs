using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Native authored frames. Presentation has no damage, input or completion callbacks.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class NativeCombatEffect : MonoBehaviour
    {
        static readonly HashSet<NativeCombatEffect> active=new HashSet<NativeCombatEffect>();
        static readonly Dictionary<string,float> diameters=new Dictionary<string,float>
        {
            {"ImpactRing",37},{"FlameArc",44},{"Arcane",45},{"Wind",67},{"BlueFire",52},
            {"Ember",59},{"Charge",37},{"Void",57},{"Energy",13},{"RifleEnergy",24},
            {"Fireball",14},{"Shell",14},{"Arrow",13},{"Bolt",35},{"Smoke",29},
            {"RewardSpark",30},{"RewardRing",28},{"RewardGlint",28},{"ItemThunder",28},{"ItemShield",26},
            {"RifleFlash",58},{"RifleWake",81},{"ParryCross",32},{"PiercingHit",32},{"RicochetHit",32},
            {"VoidBolt",59},{"PowderFlash",28},{"CannonBlast",37},{"GroundDust",24},{"TwinCrescent",29},
            {"CutStreak",26},{"MetalSpark",9},{"StoneChip",14},{"LeafChip",9},{"Slug",3},{"HeavyBlast",138},{"SlamDust",63},
            {"GreatswordCut",88},{"PlayerHit",29}
        };
        static readonly Dictionary<string,Sprite[]> clips=new Dictionary<string,Sprite[]>();
        SpriteRenderer view;Sprite[] frames;float age,duration,delay;bool loop;Color tint;
        SpriteRenderer foregroundActor;int actorOffset=-2;
        HeldWeaponView foregroundWeapon;bool aboveWeapon;
        float loopFrameRate;
        PlayerController meleeOwner;HeldWeaponView meleeWeapon;int meleeSequence,meleeContacts,meleePeakFrame=-1;
        Quaternion meleeBaseRotation;
        static readonly float[] CutKeys={.18f,.23f,.28f,.34f,.42f,.55f,.68f,.82f,.95f};
        // Authored leading pixels of the unchanged 128px intro cells, measured
        // from their top-left edge. The first three are a thin trail on the
        // outside of the canvas, not a full crescent centred at the hand yet.
        static readonly Vector2[] CutIntroLeading={new Vector2(93.5f,91.5f),new Vector2(100.5f,74.5f),
            new Vector2(102.5f,49.5f),new Vector2(62.5f,34.5f)};
        public string ClipId {get;private set;}
        public int FrameIndex {get;private set;}
        public bool FollowsMeleeAction=>meleeOwner!=null;
        public static int ActiveCount=>active.Count;
        public bool IsBelowActor=>foregroundActor!=null&&!aboveWeapon;
        public bool IsAboveWeapon=>foregroundActor!=null&&aboveWeapon;
        public void SetLoopFrameRate(float framesPerSecond)=>loopFrameRate=Mathf.Max(1,framesPerSecond);
        public static void PreloadCleave(){Frames("GreatswordCut");}
        public static void CancelCleave(PlayerController owner)
        {
            foreach(var fx in active)if(fx!=null&&fx.meleeOwner==owner)
            {fx.view.enabled=false;fx.meleeOwner=null;Destroy(fx.gameObject);}
        }
        public void BelowActor(SpriteRenderer actor,int offset=-2)
        {
            aboveWeapon=false;foregroundWeapon=null;
            foregroundActor=actor;actorOffset=offset;
            if(actor!=null){view.sortingLayerID=actor.sortingLayerID;view.sortingOrder=actor.sortingOrder+offset;}
        }
        public void AboveWeapon(SpriteRenderer actor)
        {
            aboveWeapon=true;foregroundActor=actor;
            foregroundWeapon=actor==null?null:actor.GetComponentInParent<HeldWeaponView>();
            MeleeEffectSorting.AboveWeapon(view,actor,foregroundWeapon);
        }
        public static Sprite[] Frames(string clip)
        {
            if(clips.TryGetValue(clip,out var frames))return frames;
            frames=Resources.LoadAll<Sprite>(IndividualArt.Root+"CombatFX/"+clip);
            if(frames.Length==0)throw new System.InvalidOperationException("Missing combat animation "+clip);
            System.Array.Sort(frames,(a,b)=>string.CompareOrdinal(a.name,b.name));clips[clip]=frames;return frames;
        }
        public static NativeCombatEffect Create(string clip,Vector2 point,float angle,float scale,float lifetime,
            Color color,int order=2002,Transform parent=null,bool repeating=false,float delay=0)
        {
            if(active.Count>=80&&!repeating)return null;
            var root=new GameObject("NativeFX_"+clip);
            root.transform.SetPositionAndRotation(point,Quaternion.Euler(0,0,angle));
            if(parent!=null)root.transform.SetParent(parent,true);
            var fx=root.AddComponent<NativeCombatEffect>();fx.ClipId=clip;fx.frames=Frames(clip);
            // Compare visible native pixels, never the padded 100px source canvas.
            var worldScale=scale*fx.frames[0].pixelsPerUnit/diameters[clip];
            var parentScale=parent==null?Vector3.one:parent.lossyScale;
            // Marks/pickup glints may follow scaled actors. Their native pixels keep the requested world size.
            root.transform.localScale=new Vector3(worldScale/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),
                worldScale/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),worldScale/Mathf.Max(.001f,Mathf.Abs(parentScale.z)));
            fx.duration=lifetime;fx.loop=repeating;fx.tint=color;fx.delay=delay;
            fx.view=root.AddComponent<SpriteRenderer>();fx.view.sprite=fx.frames[0];fx.view.sortingOrder=order;fx.view.color=color;
            fx.view.enabled=delay<=0;
            active.Add(fx);return fx;
        }
        public static NativeCombatEffect CreateCleave(PlayerController owner,Vector2 aim,Vector2 envelope,SpriteRenderer actor)
        {
            // The native sweep begins before contact and reaches its bright source
            // pose at the existing .42 damage clock. It never schedules a hit.
            float angle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
            // Both releases perform the same overhead blade motion. A charged
            // strike gains weight from its preparation, reach and contact aftermath;
            // an unrelated branching wedge must not replace the blade trajectory.
            var weapon=owner.GetComponent<HeldWeaponView>();
            var grip=weapon==null?(Vector2)owner.transform.position:weapon.BladePoint(0);
            var fx=Create("GreatswordCut",grip,
                angle+90,Mathf.Max(envelope.x,envelope.y)*1.08f,.42f,Color.white,actor.sortingOrder-2,owner.transform);
            if(fx==null)return null;
            fx.meleeOwner=owner;fx.meleeWeapon=weapon;fx.meleeSequence=owner.AttackAnimationSequence;fx.meleeContacts=owner.MeleeContactsResolved;
            fx.meleeBaseRotation=fx.transform.rotation;
            fx.view.flipY=true;fx.view.enabled=false;fx.AboveWeapon(actor);return fx;
        }
        void Update()
        {
            if(meleeOwner!=null)
            {
                bool newContact=meleeOwner.MeleeContactsResolved>meleeContacts;
                if((!meleeOwner.IsMeleeSwingActive&&!newContact)||meleeOwner.AttackAnimationSequence!=meleeSequence||meleeOwner.IsDead)
                {Destroy(gameObject);return;}
                if(meleeOwner.AnimationPaused)return;
                var progress=meleeOwner.AttackAnimationProgress;var keys=CutKeys;
                FrameIndex=0;for(int i=keys.Length-1;i>=0;i--)if(progress>=keys[i]){FrameIndex=i;break;}
                // A late rendered frame must still show the actual contact flash.
                // Read the resolved-contact counter; never delay or replay damage.
                if(newContact){meleeContacts=meleeOwner.MeleeContactsResolved;meleePeakFrame=Time.frameCount;}
                if(meleePeakFrame==Time.frameCount)FrameIndex=4;
                view.enabled=progress>=keys[0];view.sprite=frames[FrameIndex];
                return;
            }
            age+=Time.deltaTime;
            if(delay>0){delay-=Time.deltaTime;age=0;if(delay>0)return;view.enabled=true;}
            if(!loop&&age>=duration){Destroy(gameObject);return;}
            FrameIndex=loop?(Mathf.FloorToInt(age*(loopFrameRate>0?loopFrameRate:frames.Length<=6?14f:36f))%frames.Length):
                Mathf.Clamp(Mathf.FloorToInt(age/duration*frames.Length),0,frames.Length-1);
            view.sprite=frames[FrameIndex];
            if(!loop){var c=tint;c.a*=Mathf.Clamp01((duration-age)/Mathf.Min(.075f,duration*.3f));view.color=c;}
        }
        void LateUpdate()
        {
            if(aboveWeapon)MeleeEffectSorting.AboveWeapon(view,foregroundActor,foregroundWeapon);
            else if(foregroundActor!=null)view.sortingOrder=foregroundActor.sortingOrder+actorOffset;
            // Follow the current rendered grip after the native body/socket and
            // held blade update. The damage box center is not a swing pivot.
            if(meleeOwner!=null)PositionCleave();
        }
        void PositionCleave()
        {
            var grip=meleeWeapon==null?(Vector2)meleeOwner.transform.position:meleeWeapon.BladePoint(0);
            transform.SetPositionAndRotation(grip,meleeBaseRotation);
            // Only the pre-contact ribbon follows the original metal tip. Its
            // fixed canvas radius used to leave a detached line beyond the blade.
            // By the existing .42 contact, return to the original hand pivot,
            // orientation and full-sized envelope used by the damage geometry.
            if(FrameIndex>=CutIntroLeading.Length||meleeWeapon==null||meleeOwner.EquippedWeapon==null||
                !NativeWeaponArt.TryGet(meleeOwner.EquippedWeapon.Id,out var pose))return;
            var metal=meleeWeapon.VisualRenderer;if(metal==null||metal.sprite==null)return;
            var tip=(Vector2)metal.transform.TransformPoint(SourcePoint(metal,pose.Muzzle));
            var source=SourcePoint(view,CutIntroLeading[FrameIndex]);
            var current=(Vector2)transform.TransformVector(source);
            var weight=FrameIndex<3?1f:1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(CutKeys[3],MeleeMotionTiming.Contact,meleeOwner.AttackAnimationProgress));
            var turn=Vector2.SignedAngle(current,tip-grip);
            transform.rotation=Quaternion.AngleAxis(turn*weight,Vector3.forward)*meleeBaseRotation;
            transform.position=(Vector3)grip+((Vector3)(tip-grip)-transform.TransformVector(source))*weight;
        }
        static Vector3 SourcePoint(SpriteRenderer renderer,Vector2 topLeftPixel)
        {
            var sprite=renderer.sprite;
            var local=new Vector3((topLeftPixel.x-sprite.pivot.x)/sprite.pixelsPerUnit,
                (sprite.rect.height-topLeftPixel.y-sprite.pivot.y)/sprite.pixelsPerUnit,0);
            if(renderer.flipX)local.x=-local.x;if(renderer.flipY)local.y=-local.y;
            return local;
        }
        void OnDestroy(){active.Remove(this);}
    }

    // Explicit above-weapon policy for physical melee traces only. Ground dust,
    // projectiles and every existing BelowActor caller retain their own policy.
    internal static class MeleeEffectSorting
    {
        internal static void AboveWeapon(SpriteRenderer effect,SpriteRenderer body,HeldWeaponView held)
        {
            var weapon=held==null?null:held.VisualRenderer;
            if(weapon!=null)
            {
                effect.sortingLayerID=weapon.sortingLayerID;
                effect.sortingOrder=weapon.sortingOrder+1;
            }
            else if(body!=null)
            {
                effect.sortingLayerID=body.sortingLayerID;
                effect.sortingOrder=body.sortingOrder-1;
            }
        }
    }

}
