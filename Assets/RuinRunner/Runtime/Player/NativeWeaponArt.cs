using System.Collections.Generic;
using UnityEngine;
namespace RuinRunner
{
    // Authored native pixels. Legacy art remains only for the unreviewed equipment.
    public static class NativeWeaponArt
    {
        public readonly struct Pose
        {
            public readonly Vector2 Grip, Muzzle;
            public readonly float ForwardAngle;
            public Pose(float x,float y,float mx,float my,float angle)
            {Grip=new Vector2(x,y);Muzzle=new Vector2(mx,my);ForwardAngle=angle;}
        }
        private static readonly Dictionary<string,Pose> poses=new Dictionary<string,Pose>
        {
            { "worn_dagger", new Pose(6.5f,16f,6.5f,2f,90f) },
            { "wind_dagger", new Pose(6.5f,16f,6.5f,2f,90f) },
            { "shadow_fang", new Pose(8.5f,25f,8.5f,2f,90f) },
            { "rust_sword", new Pose(6.5f,26f,6.5f,2f,90f) },
            { "knight_sword", new Pose(10.5f,32f,10.5f,2f,90f) },
            { "sun_oath", new Pose(9.5f,33f,9.5f,2f,90f) },
            { "iron_mace", new Pose(7f,24f,7f,2f,90f) },
            { "iron_hammer", new Pose(11f,25f,11f,2f,90f) },
            { "quake_mace", new Pose(8f,28f,8f,2f,90f) },
            { "training_katana", new Pose(8f,28.5f,7f,0.5f,90f) },
            { "moon_blade", new Pose(8f,28.5f,7f,0.5f,90f) },
            { "blood_moon_katana", new Pose(8f,28.5f,7f,0.5f,90f) },
            { "iron_greatsword", new Pose(10.5f,42f,10.5f,2f,90f) },
            { "dragon_slayer", new Pose(9f,30f,9f,2f,90f) },
            { "blood_reaver", new Pose(7f,39f,7f,2f,90f) },
            { "hunter_bow", new Pose(13f,24f,10f,27f,-135f) },
            { "composite_bow", new Pose(15f,25f,12f,28f,-135f) },
            { "great_bow", new Pose(11f,26f,8f,29f,-135f) },
            { "celestial_bow", new Pose(10f,13f,7f,10f,135f) },
            { "scrap_pistol", new Pose(7f,14f,20f,9f,0f) },
            { "flintlock", new Pose(7f,11f,17f,7f,0f) },
            { "void_revolver", new Pose(9f,16f,30f,11f,0f) },
            { "scrap_shotgun", new Pose(8f,16f,24f,10f,0f) },
            { "scattergun", new Pose(7f,10.5f,28f,6f,0f) },
            { "storm_rifle", new Pose(7f,11f,28f,9f,0f) },
            { "starbreaker", new Pose(7f,15f,36f,9f,0f) },
            { "dragon_cannon", new Pose(9f,17f,34f,12f,0f) },
            { "twin_pistols", new Pose(8f,15f,22f,9f,0f) },
            { "hand_crossbow", new Pose(13f,22f,23f,12f,45f) },
            { "repeater", new Pose(15f,22f,25f,12f,45f) },
            { "siege_crossbow", new Pose(14f,23f,25f,12f,45f) },
            { "cinder_tanto", new Pose(5.5f,25f,5.5f,2f,90f) },
            { "azure_dirk", new Pose(6.5f,20f,6.5f,2f,90f) },
            { "tide_sword", new Pose(8f,29f,8f,2f,90f) },
            { "ember_sword", new Pose(7f,27f,7f,2f,90f) },
            { "sentinel_cleaver", new Pose(10f,31f,10f,2f,90f) },
            { "glacier_greatsword", new Pose(8f,33f,8f,2f,90f) },
            { "copper_revolver", new Pose(7f,10f,20f,6f,0f) },
            { "spark_pistol", new Pose(7f,14f,21f,7f,0f) },
            { "signal_pistol", new Pose(12f,12f,26f,8f,0f) },
            { "scout_rifle", new Pose(15f,11f,32f,5f,0f) },
            { "coil_rifle", new Pose(5f,14f,27f,9f,0f) },
            { "breach_shotgun", new Pose(7f,11f,29f,11f,0f) },
            { "twin_bore", new Pose(6f,10f,21f,8f,0f) },
            { "siege_mortar", new Pose(5f,13f,29f,7f,0f) },
            { "sun_cannon", new Pose(5f,15f,26f,10f,0f) },
            { "returning_edge", new Pose(5f,22f,5f,2f,90f) },
            { "gravity_maul", new Pose(10.5f,34f,10.5f,3f,90f) },
            { "tether_carbine", new Pose(12f,13f,32f,7f,0f) },
            { "bastion_shotgun", new Pose(8f,14f,29f,10f,0f) },
        };
        public static bool Supports(string id)=>id!=null&&poses.ContainsKey(id);
        public static bool TryGet(string id,out Pose pose)=>poses.TryGetValue(id,out pose);
        public static string Path(string id)=>"Art/Pixel/Separated/Equipment/Weapon/"+id;
    }
}
