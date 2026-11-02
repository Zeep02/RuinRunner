#if UNITY_EDITOR
using UnityEditor;

namespace RuinRunner.EditorTools
{
    public sealed class PixelArtImportProcessor : AssetPostprocessor
    {
        // Changing a postprocessor does not migrate already imported textures.
        // Run before every clean build, and expose the same repair in the Editor.
        [MenuItem("Prototype/Art/Validate and Repair Pixel Imports")]
        public static void EnsurePixelImports()
        {
            var repaired = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/RuinRunner/Resources/Art/Pixel" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.npotScale == TextureImporterNPOTScale.None && importer.isReadable &&
                    !importer.mipmapEnabled && importer.filterMode == UnityEngine.FilterMode.Point &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed && importer.maxTextureSize == 4096) continue;
                ApplyPolicy(importer);
                importer.SaveAndReimport();
                repaired++;
            }
            UnityEngine.Debug.Log("[Pixel Imports] Repaired " + repaired + " legacy imports; NPOT preserved, point filter, no compression/mipmaps.");
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Art/Pixel/")) return;

            var importer = (TextureImporter)assetImporter;
            ApplyPolicy(importer);
        }

        private static void ApplyPolicy(TextureImporter importer)
        {
            // Individual PNGs retain their authored single-sprite pivots, PPU and borders.
            if (!importer.assetPath.Contains("/Separated/")) importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            // Preserve atlas cell coordinates for generated non-power-of-two sources.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.wrapMode = importer.assetPath.EndsWith("/Meadow/Terrain/Dirt.png")
                ? UnityEngine.TextureWrapMode.Repeat : UnityEngine.TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.alphaIsTransparency = true;
        }
    }
}
#endif
