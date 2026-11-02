using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum PixelHudBarType { Health, Mana, Experience, Boss }

    public static class PixelHudSkin
    {
        public static bool Available => IndividualArt.Exists("HUD/Health/Empty");
        public static Texture2D GetEmpty(PixelHudBarType type) => IndividualArt.Texture("HUD/" + type + "/Empty");
        public static Texture2D GetFilled(PixelHudBarType type) => IndividualArt.Texture("HUD/" + type + "/Filled");
    }
}
