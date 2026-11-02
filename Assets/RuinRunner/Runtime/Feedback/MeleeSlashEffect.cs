using UnityEngine;
using System.Collections.Generic;

namespace RuinRunner
{
    [DefaultExecutionOrder(300)]
    public sealed class MeleeSlashEffect : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float elapsed;
        private float duration;
        private SpriteRenderer caster;
        private PlayerController meleeOwner;
        private HeldWeaponView heldWeapon;
        private Vector3 unboundOrigin;
        private bool alignArcIntro;
        private int sequence,contacts,peakFrame=-1;
        private static readonly HashSet<MeleeSlashEffect> active=new HashSet<MeleeSlashEffect>();
        private static readonly float[] SixKeys={.18f,.30f,.42f,.55f,.72f,.90f};
        private static readonly float[] FiveKeys={.18f,.30f,.42f,.68f,.88f};
        private static readonly float[] FourKeys={.18f,.42f,.68f,.90f};
        public bool FollowsMeleeAction=>meleeOwner!=null;
        public int FrameIndex {get;private set;}

        public static void CancelBoundSlashes(PlayerController owner)
        {
            foreach(var effect in active)if(effect!=null&&effect.meleeOwner==owner)
            {effect.spriteRenderer.enabled=false;effect.meleeOwner=null;Destroy(effect.gameObject);}
        }

        public static bool CreateBound(PlayerController player,float angle,Vector2 hitboxSize,bool heavy,bool critical,bool reverse,SpriteRenderer actor)
        {
            var held=player.GetComponent<HeldWeaponView>();
            if(!Create(held==null?(Vector2)player.transform.position:held.BladePoint(0),angle,hitboxSize,player.EquippedWeapon.Family,heavy,critical,actor.sortingOrder-2,reverse,actor,out var effect))return false;
            effect.transform.SetParent(player.transform,true);
            effect.meleeOwner=player;effect.heldWeapon=held;effect.sequence=player.AttackAnimationSequence;effect.contacts=player.MeleeContactsResolved;
            effect.spriteRenderer.enabled=false;return true;
        }

        public static bool Create(Vector2 position, float angle, Vector2 hitboxSize, WeaponFamily family, bool heavy, bool critical,
            int sortingOrder = 1002, bool reverse = false, SpriteRenderer owner = null)
            =>Create(position,angle,hitboxSize,family,heavy,critical,sortingOrder,reverse,owner,out _);

        private static bool Create(Vector2 position,float angle,Vector2 hitboxSize,WeaponFamily family,bool heavy,bool critical,
            int sortingOrder,bool reverse,SpriteRenderer owner,out MeleeSlashEffect effect)
        {
            effect=null;
            var slashFrames = PixelArtAtlas.GetMeleeSlashFrames(family, critical);
            if (slashFrames == null || slashFrames.Length == 0) return false;

            var root = new GameObject(heavy ? "HeavySlashVFX" : "SlashVFX");
            root.transform.position = position;
            // The Circular contact pose faces up; the blade arcs face down.
            // Preserve each original silhouette and put its bright contact on
            // the committed strike side instead of behind the caster.
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle+(family==WeaponFamily.Mace?-90f:90f));
            // Preserve the authored arc proportions. Transparent padding accounts
            // for about one sixth of the canvas; the bright arc fills the envelope.
            var diameter = Mathf.Max(hitboxSize.x, hitboxSize.y);
            root.transform.localScale = Vector3.one * (diameter * 1.18f);
            effect = root.AddComponent<MeleeSlashEffect>();
            effect.frames = slashFrames;
            effect.caster = owner;
            effect.heldWeapon = owner == null ? null : owner.GetComponentInParent<HeldWeaponView>();
            effect.unboundOrigin = position;
            effect.alignArcIntro = family == WeaponFamily.Longsword;
            effect.duration = heavy ? .30f : family == WeaponFamily.Dagger ? .21f : .26f;
            effect.spriteRenderer = root.AddComponent<SpriteRenderer>();
            effect.spriteRenderer.sprite = slashFrames[0];
            effect.spriteRenderer.flipX = reverse;
            effect.spriteRenderer.sortingOrder = sortingOrder;
            effect.spriteRenderer.color = critical ? new Color(1f, .88f, .62f, 1f) : Color.white;
            active.Add(effect);
            return true;
        }

        private void LateUpdate()
        {
            // Reserve an order above the blade while the authored face/hand
            // remains in front of both. Never rely on a body-order tie.
            MeleeEffectSorting.AboveWeapon(spriteRenderer, caster, heldWeapon);

            var anchor = meleeOwner == null ? unboundOrigin : (Vector3)(heldWeapon == null
                ? (Vector2)meleeOwner.transform.position : heldWeapon.BladePoint(0));
            // Arc/00 is authored with the same downward-facing concavity as
            // Arc/01, but its circle centre sits 13 source pixels higher in the
            // common 42px canvas. Translate that first cell, never flip/rotate
            // its silhouette or shrink the crescent. Later cells and other
            // weapon families keep their original anchors and timing.
            var offset = alignArcIntro && FrameIndex == 0
                ? new Vector3(0f, -13f / frames[0].pixelsPerUnit, 0f) : Vector3.zero;
            transform.position = anchor + transform.TransformVector(offset);
        }

        private void Update()
        {
            if(meleeOwner!=null)
            {
                bool contact=meleeOwner.MeleeContactsResolved>contacts;
                if(meleeOwner.IsDead||meleeOwner.AttackAnimationSequence!=sequence||!meleeOwner.IsMeleeSwingActive&&!contact)
                {spriteRenderer.enabled=false;Destroy(gameObject);return;}
                if(meleeOwner.AnimationPaused)return;
                var progress=meleeOwner.AttackAnimationProgress;var keys=frames.Length==4?FourKeys:frames.Length==5?FiveKeys:SixKeys;
                FrameIndex=0;for(int i=keys.Length-1;i>=0;i--)if(progress>=keys[i]){FrameIndex=i;break;}
                // Read the original resolved contact; presentation never delays or
                // replays gameplay damage, including a late rendered frame.
                if(contact){contacts=meleeOwner.MeleeContactsResolved;peakFrame=Time.frameCount;}
                if(peakFrame==Time.frameCount)FrameIndex=frames.Length==4?1:2;
                spriteRenderer.enabled=progress>=keys[0];spriteRenderer.sprite=frames[FrameIndex];return;
            }
            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }
            FrameIndex=WeaponEffectTiming.Frame(elapsed / duration, frames.Length);spriteRenderer.sprite = frames[FrameIndex];
        }
        private void OnDestroy(){active.Remove(this);}
    }
}
