using UnityEngine;

namespace RuinRunner
{
    /// <summary>Pixel-Boy/AAA authored attack poses; owns no damage or gameplay state.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class ExternalCombatEffect : MonoBehaviour
    {
        SpriteRenderer view;Sprite[] frames;float age,duration;
        SpriteRenderer caster;
        HeldWeaponView held;bool meleeForeground;
        public static void Create(string clip,Vector2 point,float angle,float size,int order,SpriteRenderer owner=null)
        {
            var root=new GameObject(clip+"VFX");root.transform.SetPositionAndRotation(point,Quaternion.Euler(0,0,angle));
            root.transform.localScale=Vector3.one*size;
            var effect=root.AddComponent<ExternalCombatEffect>();effect.frames=IndividualArt.Frames("External/NinjaAdventure/Combat/"+clip,4);
            effect.caster=owner;
            effect.held=owner==null?null:owner.GetComponentInParent<HeldWeaponView>();
            effect.meleeForeground=clip=="Impact"||clip=="Cleave";
            effect.duration=clip=="Impact"?.32f:.28f;effect.view=root.AddComponent<SpriteRenderer>();
            effect.view.sprite=effect.frames[0];effect.view.sortingOrder=order;
            if(effect.meleeForeground)MeleeEffectSorting.AboveWeapon(effect.view,owner,effect.held);
        }
        void LateUpdate()
        {
            if(meleeForeground)MeleeEffectSorting.AboveWeapon(view,caster,held);
            else if(caster!=null)view.sortingOrder=caster.sortingOrder-2;
        }
        void Update()
        {
            age+=Time.deltaTime;if(age>=duration){Destroy(gameObject);return;}
            view.sprite=frames[WeaponEffectTiming.Frame(age/duration,frames.Length)];
        }
    }
}
