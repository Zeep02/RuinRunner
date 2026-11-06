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
            {"GreatswordCut",88},{"PlayerHit",29},
            // Monster attack clips, Tools/Art/enemy-attack-fx.mjs --diameters
            {"EBurstSpore",61},{"EBurstHex",61},{"EBurstStone",100},
            {"ERingStone",208},{"ERingMid",144},{"ERingSmall",76},{"ERingSpore",122},{"ERingHex",80},{"ERushDust",12},
            {"EQuakeRift",252},{"EWhirlpool",108},{"EWaveCrash",211},{"EGeyserBlast",164},{"ESmite",180},{"ESealCircle",216},{"EManaBurst",137}
        };
        public static float NativeDiameter(string clip)=>diameters[clip];
        static readonly Dictionary<string,Sprite[]> clips=new Dictionary<string,Sprite[]>();
        SpriteRenderer view;Sprite[] frames;float age,duration,delay;bool loop;Color tint;
        SpriteRenderer foregroundActor;int actorOffset=-2;
        HeldWeaponView foregroundWeapon;bool aboveWeapon;
        float loopFrameRate;
        public string ClipId {get;private set;}
        public int FrameIndex {get;private set;}
        public static int ActiveCount=>active.Count;
        public void SetLoopFrameRate(float framesPerSecond)=>loopFrameRate=Mathf.Max(1,framesPerSecond);
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
            System.Array.Sort(frames,(a,b)=>string.CompareOrdinal(a.name,b.name));
            // Exports run for their whole clock, so many start or end empty. Those would blink a loop off each time round.
            if(BlankEnds().TryGetValue(clip,out var ends))
            {
                var visible=new Sprite[frames.Length-ends.x-ends.y];
                System.Array.Copy(frames,ends.x,visible,0,visible.Length);frames=visible;
            }
            clips[clip]=frames;return frames;
        }
        /// <summary>The rate a pixelfx clip was exported at and whether it repeats. False for older hand-drawn clips.</summary>
        public static bool TryTiming(string clip,out float framesPerSecond,out bool repeats)
        {
            BlankEnds();
            var known=timings.TryGetValue(clip,out var timing);
            framesPerSecond=known?timing.x:0;repeats=known&&timing.y>0;
            return known;
        }
        static readonly Dictionary<string,Vector2> timings=new Dictionary<string,Vector2>();
        public static void PreloadIfKnown(string clip){BlankEnds();if(timings.ContainsKey(clip))Frames(clip);}
        /// <summary>Clips any fight may need: monster attacks and shared hits. Loaded up front so a first attack doesn't hitch on the disk.
        /// Signature weapon clips load when the weapon is equipped.</summary>
        public static List<string> SharedPixelFxClips()
        {
            BlankEnds();
            var shared=new List<string>();
            foreach(var clip in timings.Keys)if(clip.StartsWith("E")||clip.StartsWith("Fx")||clip.StartsWith("Warden"))shared.Add(clip);
            return shared;
        }
        static Dictionary<string,Vector2Int> blankEnds;
        static Dictionary<string,Vector2Int> BlankEnds()
        {
            if(blankEnds!=null)return blankEnds;
            blankEnds=new Dictionary<string,Vector2Int>();
            // Written by Tools/Art/list_blank_fx_frames.py.
            var table=Resources.Load<TextAsset>(IndividualArt.Root+"CombatFX/BlankFrames");
            if(table==null)return blankEnds;
            foreach(var line in table.text.Split('\n'))
            {
                var parts=line.Trim().Split(' ');
                if(parts.Length!=5)continue;
                blankEnds[parts[0]]=new Vector2Int(int.Parse(parts[1]),int.Parse(parts[2]));
                timings[parts[0]]=new Vector2(int.Parse(parts[3]),int.Parse(parts[4]));
            }
            return blankEnds;
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
            fx.view=root.AddComponent<SpriteRenderer>();fx.view.sprite=fx.frames[0];fx.view.sortingOrder=order;fx.view.color=color;PixelFxMaterial.Apply(fx.view,clip);
            fx.view.enabled=delay<=0;
            active.Add(fx);return fx;
        }
        void Update()
        {
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
