using UnityEngine;

namespace RuinRunner
{
    public enum WorldInteractionArt
    {
        ChestClosed,
        ChestOpen,
        PortalLocked,
        PortalActive,
        AmmoFrame,
        DashReady,
        DashEmpty,
        LootHalo
    }

    public static class PixelWorldArt
    {
        public static bool Available => IndividualArt.Exists("World/ChestClosed");
        public static Texture2D GetTexture(WorldInteractionArt art) => IndividualArt.Texture("World/" + art);
        public static Sprite GetSprite(WorldInteractionArt art) => IndividualArt.Sprite("World/" + art);
    }
}
