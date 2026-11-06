using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Authored landmarks on the individual weapon PNGs, in top-left pixel coordinates.</summary>
    public static class RangedWeaponArt
    {
        public readonly struct Landmarks
        {
            public readonly Vector2 Grip, Muzzle;
            public readonly float ForwardAngle;
            public Landmarks(float gx, float gy, float mx, float my, float angle)
            { Grip = new Vector2(gx, gy); Muzzle = new Vector2(mx, my); ForwardAngle = angle; }
        }

        private static readonly Dictionary<string, Landmarks> landmarks = new Dictionary<string, Landmarks>
        {
            { "hunter_bow", new Landmarks(8,14,10,14,0) },
            { "composite_bow", new Landmarks(25,48,19,42,140) },
            { "great_bow", new Landmarks(28,50,21,44,140) },
            { "celestial_bow", new Landmarks(30,54,23,48,140) },
        };

        public static bool TryGet(string id, out Landmarks value)
        {
            if(id==null){value=default;return false;}
            if((landmarks.ContainsKey(id)||id=="tether_carbine"||id=="bastion_shotgun"||SignatureWeaponCatalog.Has(id)) && NativeWeaponArt.TryGet(id,out var pose))
            {
                value=new Landmarks(pose.Grip.x,pose.Grip.y,pose.Muzzle.x,pose.Muzzle.y,pose.ForwardAngle);
                return true;
            }
            return landmarks.TryGetValue(id,out value);
        }
    }
}
