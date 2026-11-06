using UnityEngine;

namespace RuinRunner
{
    /// <summary>Plays one combat clip on the owner's pause clock. A pixelfx one-shot runs at the rate it was drawn, whatever the
    /// duration asked: squeezing it loses its anticipation and falloff, which is what makes it read. A pixelfx loop repeats for the
    /// duration and fades out. Older hand-drawn clips are spread over the duration.</summary>
    public sealed class BossNativeEffect : MonoBehaviour
    {
        const float LoopFadeSeconds=.12f;
        EnemyController owner; SpriteRenderer view; Sprite[] frames; float elapsed,duration; bool outlivesOwner,ownerless;
        string clipId; float framesPerSecond; bool repeats;
        public static BossNativeEffect Create(EnemyController owner,string clip,Vector2 point,float duration,float angle=0)
        {
            var root=new GameObject("BossNative_"+clip); root.transform.SetPositionAndRotation(point,Quaternion.Euler(0,0,angle));
            var fx=root.AddComponent<BossNativeEffect>(); fx.owner=owner;fx.ownerless=owner==null;fx.duration=duration;fx.clipId=clip;
            fx.frames=NativeCombatEffect.Frames(clip);fx.view=root.AddComponent<SpriteRenderer>();
            if(NativeCombatEffect.TryTiming(clip,out fx.framesPerSecond,out fx.repeats)&&!fx.repeats)fx.duration=fx.frames.Length/fx.framesPerSecond;
            fx.view.sprite=fx.frames[0];fx.view.sortingLayerName="WorldFloor";fx.view.sortingOrder=11;PixelFxMaterial.Apply(fx.view,clip);
            return fx;
        }
        // Drawn over actors. A hit or death flash must finish even when its source is gone.
        public static BossNativeEffect CreateOverActors(EnemyController owner,string clip,Vector2 point,float duration,float angle=0)
        {
            var fx=Create(owner,clip,point,duration,angle);
            fx.view.sortingLayerName="Default";fx.view.sortingOrder=2001;fx.outlivesOwner=true;
            return fx;
        }
        // A shock ring or dust on the floor, under everyone's feet. Finishes even when its source is gone.
        public static BossNativeEffect CreateOnFloor(string clip,Vector2 point,float duration,float angle=0)
        {
            var fx=Create(null,clip,point,duration,angle);fx.outlivesOwner=true;
            return fx;
        }
        /// <summary>Scales the clip so its visible body is this wide in world units.</summary>
        public BossNativeEffect Fit(float diameter)
        {
            transform.localScale=Vector3.one*(diameter*32f/NativeCombatEffect.NativeDiameter(clipId));
            return this;
        }
        void Update()
        {
            // A charge-up loop has no reason to outlast the one charging.
            if(owner==null&&(!outlivesOwner||repeats)&&!ownerless){Destroy(gameObject);return;}
            if(owner!=null&&owner.AnimationPaused)return;
            elapsed+=Time.deltaTime;
            if(elapsed>=duration){Destroy(gameObject);return;}
            var index=framesPerSecond>0?(int)(elapsed*framesPerSecond):(int)(elapsed/duration*frames.Length);
            view.sprite=frames[repeats?index%frames.Length:Mathf.Min(frames.Length-1,index)];
            if(repeats){var tint=view.color;tint.a=Mathf.Clamp01((duration-elapsed)/LoopFadeSeconds);view.color=tint;}
        }
    }
}
