using UnityEngine;
using System.Collections.Generic;

namespace RuinRunner
{
    /// <summary>Native steel tip/streak attached to the committed stab; never owns damage.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class MeleeThrustEffect : MonoBehaviour
    {
        static Sprite[] longsword,dagger;
        static readonly HashSet<MeleeThrustEffect> active=new HashSet<MeleeThrustEffect>();
        static readonly float[] LongKeys={0f,.16f,.34f,.46f,.60f,.72f,.84f,.96f};
        static readonly float[] DaggerKeys={.16f,.34f,.48f,.65f,.86f};
        SpriteRenderer caster,view;
        Sprite[] frames;
        PlayerController player;
        HeldWeaponView held;
        WeaponFamily family;
        int sequence,contacts,peakFrame=-1;
        float age;
        public int FrameIndex {get;private set;}
        public bool FollowsThrustAction=>player!=null;
        public const float Duration=.24f;
        public static float OriginDistance(WeaponFamily family)=>family==WeaponFamily.Dagger?.25f:.40f;
        public static float DisplayScale(WeaponFamily family)=>family==WeaponFamily.Dagger?1.2f:1.35f;
        // Exact contact cell's alpha-bearing pixel quads, relative to its fixed pivot.
        public static float VisibleReach(WeaponFamily family)=>OriginDistance(family)+25f/32f*DisplayScale(family);
        public static float VisibleBreadth(WeaponFamily family)=>(family==WeaponFamily.Dagger?8f:10f)/32f*DisplayScale(family);
        public static void Preload()
        {
            if(longsword==null)longsword=IndividualArt.Frames("External/CombatFX/LongswordStab",8);
            if(dagger==null)dagger=IndividualArt.Frames("External/CombatFX/DaggerStab",5);
        }
        public static void CancelBound(PlayerController owner)
        {
            foreach(var fx in active)if(fx!=null&&fx.player==owner)
            {fx.view.enabled=false;fx.player=null;Destroy(fx.gameObject);}
        }
        public static void CreateBound(PlayerController owner,float angle,bool critical,SpriteRenderer body)
        {
            var held=owner.GetComponent<HeldWeaponView>();var family=owner.EquippedWeapon.Family;
            var fx=Make(held==null?(Vector2)owner.transform.position:held.BladePoint(OriginDistance(family)),angle,body.sortingOrder-2,critical,family,body);
            if(fx==null)return;
            fx.transform.SetParent(owner.transform,true);fx.player=owner;fx.held=held;
            fx.sequence=owner.AttackAnimationSequence;fx.contacts=owner.MeleeContactsResolved;fx.view.enabled=false;
        }
        // Compatibility for isolated callers. Physical cells retain uniform native proportions.
        public static void Create(Vector2 center,float angle,int order,bool critical,float length=3.2f,WeaponFamily family=WeaponFamily.Longsword,SpriteRenderer owner=null)
        {Make(center,angle,order,critical,family,owner);}
        static MeleeThrustEffect Make(Vector2 origin,float angle,int order,bool critical,WeaponFamily family,SpriteRenderer caster)
        {
            Preload();var cells=family==WeaponFamily.Dagger?dagger:longsword;
            if(cells==null||cells.Length==0)return null;
            var root=new GameObject("ThrustVFX");root.transform.position=origin;
            root.transform.rotation=Quaternion.Euler(0,0,angle);root.transform.localScale=Vector3.one*DisplayScale(family);
            var fx=root.AddComponent<MeleeThrustEffect>();fx.frames=cells;fx.family=family;fx.caster=caster;
            fx.held=caster==null?null:caster.GetComponentInParent<HeldWeaponView>();
            fx.view=root.AddComponent<SpriteRenderer>();fx.view.sprite=cells[0];fx.view.sortingOrder=order;
            MeleeEffectSorting.AboveWeapon(fx.view,caster,fx.held);
            fx.view.color=critical?new Color(1f,.90f,.75f):Color.white;active.Add(fx);return fx;
        }
        void Update()
        {
            if(player!=null)
            {
                bool contact=player.MeleeContactsResolved>contacts;
                if(player.IsDead||player.AttackAnimationSequence!=sequence||!player.IsThrustActive&&!contact)
                {view.enabled=false;Destroy(gameObject);return;}
                if(player.AnimationPaused)return;
                var progress=player.AttackAnimationProgress;var keys=family==WeaponFamily.Dagger?DaggerKeys:LongKeys;
                FrameIndex=0;for(int i=keys.Length-1;i>=0;i--)if(progress>=keys[i]){FrameIndex=i;break;}
                if(contact){contacts=player.MeleeContactsResolved;peakFrame=Time.frameCount;}
                if(peakFrame==Time.frameCount)FrameIndex=family==WeaponFamily.Dagger?1:2;
                view.enabled=progress>=keys[0];view.sprite=frames[FrameIndex];return;
            }
            age+=Time.deltaTime;if(age>=Duration){view.enabled=false;Destroy(gameObject);return;}
            FrameIndex=WeaponEffectTiming.Frame(age/Duration,frames.Length);view.sprite=frames[FrameIndex];
        }
        void LateUpdate()
        {
            MeleeEffectSorting.AboveWeapon(view,caster,held);
            if(player!=null)transform.position=held==null?(Vector2)player.transform.position:held.BladePoint(OriginDistance(family));
        }
        void OnDestroy(){active.Remove(this);}
    }
}
