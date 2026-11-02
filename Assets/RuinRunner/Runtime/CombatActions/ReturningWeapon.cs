using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>A native weapon sprite on an outbound/return path, with one contact per target per leg.</summary>
    public sealed class ReturningWeapon : MonoBehaviour
    {
        PlayerController owner;Vector2 aim;float damage,age;int epoch;bool returning;
        readonly HashSet<EnemyController> struck=new HashSet<EnemyController>();
        public static ReturningWeapon Create(PlayerController owner,WeaponDefinition weapon,Vector2 aim,float damage,int epoch)
        {
            var root=new GameObject("Returning native blade");root.transform.position=owner.transform.position;
            var view=root.AddComponent<SpriteRenderer>();view.sprite=PixelArtAtlas.GetHeldWeaponSprite(weapon);
            MeleeEffectSorting.AboveWeapon(view,owner.BodyRenderer,owner.GetComponent<HeldWeaponView>());
            var action=root.AddComponent<ReturningWeapon>();action.owner=owner;action.aim=aim.normalized;action.damage=damage;action.epoch=epoch;return action;
        }
        void Update()
        {
            if(owner==null||!owner.IsCombatEpochValid(epoch)){Destroy(gameObject);return;}
            age+=Time.deltaTime;
            if(!returning&&age>=.3f){returning=true;struck.Clear();}
            Vector2 from=transform.position;
            if(returning){var offset=(Vector2)owner.transform.position-from;if(offset.magnitude<.35f){Destroy(gameObject);return;}aim=offset.normalized;}
            var to=CombatActionGeometry.Clip(from,aim,Time.deltaTime*10);
            foreach(var enemy in CombatActionGeometry.Segment(from,to,.6f))if(struck.Add(enemy))
            {owner.SecondaryHit(enemy,damage,aim*2);NativeCombatEffect.Create("CutStreak",enemy.transform.position,0,.65f,.18f,Color.white)?.AboveWeapon(owner.BodyRenderer);}
            transform.position=to;transform.Rotate(0,0,-720*Time.deltaTime);
            if(age>1.2f||Vector2.Distance(from,to)<Time.deltaTime*9.8f)Destroy(gameObject);
        }
    }
}
