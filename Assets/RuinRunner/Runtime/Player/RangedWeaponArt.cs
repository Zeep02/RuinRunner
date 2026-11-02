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
            { "hand_crossbow", new Landmarks(33,53,75,11,45) },
            { "scrap_pistol", new Landmarks(27,62,103,8,21) },
            { "scrap_shotgun", new Landmarks(27,70,112,8,32) },
            { "composite_bow", new Landmarks(25,48,19,42,140) },
            { "repeater", new Landmarks(30,55,76,14,46) },
            { "flintlock", new Landmarks(28,68,110,8,25) },
            { "scattergun", new Landmarks(29,72,118,10,31) },
            { "great_bow", new Landmarks(28,50,21,44,140) },
            { "siege_crossbow", new Landmarks(30,55,73,15,46) },
            { "twin_pistols", new Landmarks(29,70,111,9,29) },
            { "storm_rifle", new Landmarks(30,73,115,5,35) },
            { "celestial_bow", new Landmarks(30,54,23,48,140) },
            { "void_revolver", new Landmarks(23,79,104,28,20) },
            { "dragon_cannon", new Landmarks(25,80,98,28,26) },
            { "starbreaker", new Landmarks(30,82,119,8,38) },
            { "copper_revolver", new Landmarks(7,10,20,6,0) },
            { "spark_pistol", new Landmarks(7,14,21,7,0) },
            { "signal_pistol", new Landmarks(12,12,26,8,0) },
            { "scout_rifle", new Landmarks(15,11,32,5,0) },
            { "coil_rifle", new Landmarks(5,14,27,9,0) },
            { "breach_shotgun", new Landmarks(7,11,29,11,0) },
            { "twin_bore", new Landmarks(6,10,21,8,0) },
            { "siege_mortar", new Landmarks(5,13,29,7,0) },
            { "sun_cannon", new Landmarks(5,15,26,10,0) }
        };

        public static bool TryGet(string id, out Landmarks value)
        {
            if(id==null){value=default;return false;}
            if((landmarks.ContainsKey(id)||id=="tether_carbine"||id=="bastion_shotgun") && NativeWeaponArt.TryGet(id,out var pose))
            {
                value=new Landmarks(pose.Grip.x,pose.Grip.y,pose.Muzzle.x,pose.Muzzle.y,pose.ForwardAngle);
                return true;
            }
            return landmarks.TryGetValue(id,out value);
        }
    }
}
