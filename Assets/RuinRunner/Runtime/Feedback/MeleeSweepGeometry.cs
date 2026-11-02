using UnityEngine;

namespace RuinRunner
{
    /// <summary>Visible contact extent of the native, hand-bound cuts.</summary>
    public static class MeleeSweepGeometry
    {
        public static bool TryEnvelope(MeleeAttackStep step,WeaponFamily family,Vector2 hand,Vector2 axis,
            out Vector2 center,out Vector2 size,int crossStroke=0)
        {
            center=axis*step.Reach;size=step.HitboxSize;
            if(step.Motion==MeleeAttackMotion.CrossCut)
            {
                // Original 57x42 Katana contact cell, pivot(28.5,21),
                // alpha x[0,52], y[1,33] in bottom-up source pixel quads.
                var unit=Mathf.Max(size.x,size.y)*1.18f/57f;
                var side=new Vector2(-axis.y,axis.x);
                center=hand+axis*(4f*unit)+side*((crossStroke==0?-2.5f:2.5f)*unit);
                size=new Vector2(32f*unit,52f*unit);return true;
            }
            float radius;
            if(step.Motion==MeleeAttackMotion.Thrust)
            {
                radius=MeleeThrustEffect.VisibleReach(family);
                size.y=MeleeThrustEffect.VisibleBreadth(family);
            }
            else if(step.Motion==MeleeAttackMotion.Cleave&&family==WeaponFamily.Greatsword)
                radius=Mathf.Max(size.x,size.y)*1.08f*49f/88f;
            else if(step.Motion==MeleeAttackMotion.Sweep)
            {
                // Exact alpha-bearing pixel-quad edge of the original contact
                // cell relative to its common pivot and actual imported PPU.
                var native=family==WeaponFamily.Dagger?16f/36f:family==WeaponFamily.Mace?28f/68f:
                    family==WeaponFamily.Katana?20f/57f:family==WeaponFamily.Greatsword?25f/70f:17f/42f;
                radius=Mathf.Max(size.x,size.y)*1.18f*native;
            }
            else return false;
            var inner=step.Reach-size.x*.5f;
            var outer=Mathf.Max(inner+.1f,Vector2.Dot(hand,axis)+radius);
            size.x=outer-inner;center=axis*((inner+outer)*.5f);
            if(step.Motion==MeleeAttackMotion.Thrust)center+=hand-axis*Vector2.Dot(hand,axis);
            return true;
        }
    }
}
