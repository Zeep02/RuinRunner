using UnityEngine;

namespace RuinRunner
{
    /// <summary>Clip names of a signature weapon. Every clip is its own pixelfx export: W + PascalId + Role.</summary>
    public sealed class SignatureMeleeFx
    {
        public readonly string Slash,Thrust,Cleave,Cross,Hit;
        public const float HitLife=.3f;
        SignatureMeleeFx(string id)
        {
            Slash=Clip(id,"Slash");Thrust=Clip(id,"Thrust");Cleave=Clip(id,"Cleave");Cross=Clip(id,"Cross");Hit=Clip(id,"Hit");
        }
        static readonly System.Collections.Generic.Dictionary<string,SignatureMeleeFx> kits=new System.Collections.Generic.Dictionary<string,SignatureMeleeFx>();
        public static SignatureMeleeFx For(WeaponDefinition weapon)
        {
            if(weapon==null||!SignatureWeaponCatalog.Has(weapon.Id))return null;
            if(!kits.TryGetValue(weapon.Id,out var kit))kits[weapon.Id]=kit=new SignatureMeleeFx(weapon.Id);
            return kit;
        }
        /// <summary>The trail of a swing: the weapon's own clip, or its family's.</summary>
        public static string Trail(WeaponDefinition weapon,MeleeAttackMotion motion,bool heavy)
        {
            var kit=For(weapon);
            if(kit!=null)return motion==MeleeAttackMotion.Thrust?kit.Thrust:motion==MeleeAttackMotion.Cleave?kit.Cleave:motion==MeleeAttackMotion.CrossCut?kit.Cross:kit.Slash;
            var family=weapon.Family==WeaponFamily.Dagger||weapon.Family==WeaponFamily.Mace||weapon.Family==WeaponFamily.Katana||weapon.Family==WeaponFamily.Greatsword||weapon.Family==WeaponFamily.Spear?weapon.Family:WeaponFamily.Longsword;
            var role=motion==MeleeAttackMotion.Thrust?"Thrust":motion==MeleeAttackMotion.CrossCut?"Cross":motion==MeleeAttackMotion.Cleave||motion==MeleeAttackMotion.Impact?"Cleave":heavy&&family==WeaponFamily.Longsword?"Heavy":"Slash";
            // A spear only has its own thrust; a swing of its shaft borrows the sword's arc.
            if(family==WeaponFamily.Spear&&role!="Thrust")return "FxTrailLongswordSlash";
            return "FxTrail"+family+role;
        }
        const float MinTrailScale=.85f,MaxTrailScale=1.25f;
        // The blade length FxTrail clips are drawn for, Tools/Art/hero-trail-fx.mjs.
        static float DrawnBladeLength(WeaponFamily family)
        {
            switch(family)
            {
                case WeaponFamily.Dagger:return 18;
                case WeaponFamily.Mace:return 24;
                case WeaponFamily.Katana:return 28;
                case WeaponFamily.Greatsword:return 38;
                default:return 26;
            }
        }
        /// <summary>Scale that fits a family's slash to this weapon. Weapons in a family differ in length.</summary>
        public static float TrailScale(WeaponDefinition weapon,MeleeAttackMotion motion)
        {
            if(For(weapon)!=null||motion==MeleeAttackMotion.Thrust||!NativeWeaponArt.TryGet(weapon.Id,out var pose))return 1;
            return Mathf.Clamp(Vector2.Distance(pose.Grip,pose.Muzzle)/DrawnBladeLength(weapon.Family),MinTrailScale,MaxTrailScale);
        }
        /// <summary>Loads a melee weapon's trails when it is picked up, so its first swing doesn't hitch on the disk.</summary>
        public static void Preload(WeaponDefinition weapon)
        {
            if(weapon==null||weapon.AttackType!=WeaponAttackType.Melee)return;
            MeleeComboFrames.Preload(weapon.Family);
            foreach(var role in new[]{"Slash","Heavy","Thrust","Cleave","Cross"})
                NativeCombatEffect.PreloadIfKnown(For(weapon)!=null?Clip(weapon.Id,role):"FxTrail"+weapon.Family+role);
        }
        public static string Clip(string id,string role)
        {
            var name="W";
            foreach(var part in id.Split('_'))name+=char.ToUpperInvariant(part[0])+part.Substring(1);
            return name+role;
        }
    }

    /// <summary>A blade trail held at the hand and stepped by the swing's own progress, so it stays on the moving tip. When the swing
    /// ends or another begins, it stays where it is and burns out on its own clock instead of vanishing. Never affects damage.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class SignatureSlashEffect : MonoBehaviour
    {
        // A trail clip's seconds from the start of the blade's sweep to the end of the swing at normal speed.
        const float SwingSeconds=.246f;
        PlayerController owner;HeldWeaponView held;SpriteRenderer view,body;Sprite[] frames;int sequence;float start,framesPerSecond,releasedAt=-1,age;
        public int FrameIndex{get;private set;}

        public static void Create(PlayerController owner,string clip,float angle,bool mirrored,float startProgress,float scale,SpriteRenderer actor)
        {
            var root=new GameObject("SignatureSlash_"+clip);
            root.transform.rotation=Quaternion.Euler(0,0,angle);root.transform.localScale=Vector3.one*scale;
            var fx=root.AddComponent<SignatureSlashEffect>();
            fx.owner=owner;fx.held=owner.GetComponent<HeldWeaponView>();fx.body=actor;fx.frames=NativeCombatEffect.Frames(clip);
            fx.sequence=owner.AttackAnimationSequence;fx.start=startProgress;
            if(!NativeCombatEffect.TryTiming(clip,out fx.framesPerSecond,out _))fx.framesPerSecond=45;
            fx.view=root.AddComponent<SpriteRenderer>();fx.view.flipY=mirrored;fx.view.enabled=false;PixelFxMaterial.Apply(fx.view,clip);
            fx.Follow();
        }
        void Update()
        {
            if(owner==null||owner.IsDead){Destroy(gameObject);return;}
            if(releasedAt<0&&(owner.AttackAnimationSequence!=sequence||!owner.IsMeleeSwingActive))
            {
                // A swing cut before its trail appeared leaves nothing to finish.
                if(!view.enabled){Destroy(gameObject);return;}
                releasedAt=FrameIndex;
            }
            if(releasedAt>=0)
            {
                age+=Time.deltaTime;
                FrameIndex=(int)(releasedAt+age*framesPerSecond);
                if(FrameIndex>=frames.Length){Destroy(gameObject);return;}
                view.sprite=frames[FrameIndex];
                return;
            }
            if(owner.AnimationPaused)return;
            var progress=owner.AttackAnimationProgress;
            view.enabled=progress>=start;
            // The clip runs on past the swing's end; those last frames play on the effect's own clock once released.
            FrameIndex=Mathf.Min(frames.Length-1,(int)(Mathf.InverseLerp(start,1,progress)*SwingSeconds*framesPerSecond));
            view.sprite=frames[FrameIndex];
        }
        void LateUpdate(){if(owner!=null&&releasedAt<0)Follow();}
        void Follow()
        {
            transform.position=held==null?owner.transform.position:(Vector3)held.BladePoint(0);
            MeleeEffectSorting.AboveWeapon(view,body,held);
        }
    }
}
