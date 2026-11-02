using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools.LegacyArt
{
    // Source sheets are editor-only materials, never loaded by the game.
    public static class LegacySource
    {
        public static Texture2D Load(string resourcePath)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RuinRunner/ArtSource/Sheets/" + resourcePath + ".png")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RuinRunner/Resources/" + resourcePath + ".png");
        }
    }
}
