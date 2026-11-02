using UnityEngine;
using System.Collections.Generic;

namespace RuinRunner
{
    /// <summary>Two physical diagonal arcs, on the body's two committed wrist strokes.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class MeleeCrossCutEffect : MonoBehaviour
    {
        static readonly HashSet<MeleeCrossCutEffect> active=new HashSet<MeleeCrossCutEffect>();
        static readonly float[][] Keys={new[]{.095f,.22f,.275f,.33f},new[]{.417f,.52f,.735f,.917f}};
        readonly SpriteRenderer[] views=new SpriteRenderer[2];
        readonly int[] keys=new int[2],peakFrame={-1,-1};
        Sprite[] frames;PlayerController player;HeldWeaponView held;SpriteRenderer caster;
        int sequence,initialContacts,contacts;
        public bool FollowsCrossAction=>player!=null;
        public int FrameIndex(int stroke)=>keys[stroke];
        public SpriteRenderer StrokeRenderer(int stroke)=>views[stroke];
        public static void CancelBound(PlayerController owner)
        {
            foreach(var fx in active)if(fx!=null&&fx.player==owner)
            {foreach(var view in fx.views)if(view!=null)view.enabled=false;fx.player=null;Destroy(fx.gameObject);}
        }
        public static bool CreateBound(PlayerController owner,float angle,Vector2 size,bool critical,SpriteRenderer caster)
        {
            var frames=PixelArtAtlas.GetMeleeSlashFrames(WeaponFamily.Katana,critical);if(frames==null||frames.Length!=4)return false;
            var root=new GameObject("CrossCutVFX");root.transform.SetParent(owner.transform,false);
            var fx=root.AddComponent<MeleeCrossCutEffect>();fx.frames=frames;fx.player=owner;fx.caster=caster;
            fx.held=owner.GetComponent<HeldWeaponView>();fx.sequence=owner.AttackAnimationSequence;
            fx.initialContacts=fx.contacts=owner.MeleeContactsResolved;
            for(int stroke=0;stroke<2;stroke++)
            {
                var child=new GameObject(stroke==0?"FirstDiagonal":"ReturnDiagonal");child.transform.SetParent(root.transform,false);
                child.transform.rotation=Quaternion.Euler(0,0,angle+MeleeMotionTiming.CrossStrikeOffset(stroke)+90f);
                child.transform.localScale=Vector3.one*Mathf.Max(size.x,size.y)*1.18f;
                var view=child.AddComponent<SpriteRenderer>();fx.views[stroke]=view;view.sprite=frames[0];view.enabled=false;
                view.flipX=stroke==1;view.sortingOrder=caster.sortingOrder-2;view.color=critical?new Color(1f,.88f,.62f):Color.white;
                MeleeEffectSorting.AboveWeapon(view,caster,fx.held);
            }
            active.Add(fx);return true;
        }
        void Update()
        {
            if(player==null)return;
            bool newContact=contacts<player.MeleeContactsResolved;
            if(player.IsDead||player.AttackAnimationSequence!=sequence||!player.IsMeleeSwingActive&&!newContact)
            {foreach(var view in views)view.enabled=false;Destroy(gameObject);return;}
            if(player.AnimationPaused)return;
            while(contacts<player.MeleeContactsResolved)
            {contacts++;var stroke=Mathf.Clamp(contacts-initialContacts-1,0,1);peakFrame[stroke]=Time.frameCount;}
            var progress=player.AttackAnimationProgress;
            // On a long rendered frame keep the most recent real contact visible.
            // Never stack both bright arcs into a detached star if two contacts
            // resolve in that frame; gameplay still resolves each stroke once.
            int contactStroke=newContact?Mathf.Clamp(contacts-initialContacts-1,0,1):-1;
            for(int stroke=0;stroke<2;stroke++)
            {
                keys[stroke]=0;for(int i=3;i>=0;i--)if(progress>=Keys[stroke][i]){keys[stroke]=i;break;}
                if(peakFrame[stroke]==Time.frameCount)keys[stroke]=1;
                views[stroke].sprite=frames[keys[stroke]];
                views[stroke].enabled=progress>=Keys[stroke][0]&&(stroke==1||progress<MeleeMotionTiming.CrossSwitch);
                if(contactStroke>=0)views[stroke].enabled=stroke==contactStroke;
            }
        }
        void LateUpdate()
        {
            if(player!=null)transform.position=held==null?(Vector2)player.transform.position:held.BladePoint(0);
            foreach(var view in views)MeleeEffectSorting.AboveWeapon(view,caster,held);
        }
        void OnDestroy(){active.Remove(this);}
    }
}
