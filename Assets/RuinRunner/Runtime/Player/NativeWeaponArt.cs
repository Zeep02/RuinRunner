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
            public Pose(float x, float y, float mx, float my, float angle)
            {
                Grip = new Vector2(x, y);
                Muzzle = new Vector2(mx, my);
                ForwardAngle = angle;
            }
        }
        private static readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>
        {
            { "hunter_bow", new Pose(13f,24f,10f,27f,-135f) },
            { "composite_bow", new Pose(15f,25f,12f,28f,-135f) },
            { "great_bow", new Pose(11f,26f,8f,29f,-135f) },
            { "celestial_bow", new Pose(10f,13f,7f,10f,135f) },
        };
        static NativeWeaponArt()
        {
            foreach (var row in SignatureWeaponCatalog.Poses)
                poses[row.Key] = row.Value;
        }
        public static bool Supports(string id) => id != null && poses.ContainsKey(id);
        public static bool TryGet(string id, out Pose pose) => poses.TryGetValue(id, out pose);
        public static string Path(string id) => "Art/Pixel/Separated/Equipment/Weapon/" + id;
    }
}
