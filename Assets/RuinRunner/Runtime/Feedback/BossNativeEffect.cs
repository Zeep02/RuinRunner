using UnityEngine;

namespace RuinRunner
{
    /// <summary>Authored combat cells at one source pixel = one world pixel.</summary>
    public sealed class BossNativeEffect : MonoBehaviour
    {
        EnemyController owner; SpriteRenderer view; Sprite[] frames; float elapsed,duration;
        public static BossNativeEffect Create(EnemyController owner,string clip,Vector2 point,float duration,float angle=0)
        {
            var root=new GameObject("BossNative_"+clip); root.transform.SetPositionAndRotation(point,Quaternion.Euler(0,0,angle));
            var fx=root.AddComponent<BossNativeEffect>(); fx.owner=owner;fx.duration=duration;
            fx.frames=NativeCombatEffect.Frames(clip);fx.view=root.AddComponent<SpriteRenderer>();
            fx.view.sprite=fx.frames[0];fx.view.sortingLayerName="WorldFloor";fx.view.sortingOrder=11;
            return fx;
        }
        void Update()
        {
            if(owner==null){Destroy(gameObject);return;} if(owner.AnimationPaused)return;
            elapsed+=Time.deltaTime;
            if(elapsed>=duration){Destroy(gameObject);return;}
            view.sprite=frames[Mathf.Min(frames.Length-1,(int)(elapsed/duration*frames.Length))];
        }
    }
}
