using UnityEngine;
namespace RuinRunner
{
    public static class EnvironmentArt
    {
        public static bool Available => IndividualArt.Exists("Meadow/Terrain/Grass");
        public static Sprite Ground => IndividualArt.Sprite("Meadow/Terrain/Grass");
        public static Sprite Cliff => IndividualArt.Sprite("Meadow/Terrain/Cliff");
        public static Sprite Dirt => IndividualArt.Sprite("Meadow/Terrain/Dirt");
        public static Sprite Prop(int cell) => IndividualArt.Sprite("Props/" + Mathf.Clamp(cell, 0, 11).ToString("D2"));
    }
}
