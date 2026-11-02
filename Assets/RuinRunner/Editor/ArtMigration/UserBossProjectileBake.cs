using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public static class UserBossProjectileBake
    {
        [MenuItem("Prototype/Art/Bake User Boss Projectiles")]
        public static void Bake()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes("ArtDirection/UserProvided/BossProjectiles-20260910/original.png")) ||
                source.width != 2048 || source.height != 768) throw new InvalidOperationException("Unexpected projectile sheet dimensions");
            for (var row = 0; row < 2; row++) for (var frame = 0; frame < 8; frame++)
            {
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                texture.SetPixels(source.GetPixels(frame * 256, source.height - (row + 1) * 256, 256, 256));
                texture.Apply();
                var path = "Assets/RuinRunner/Resources/" + IndividualArt.Root + "UserBossProjectiles/" +
                    (row == 0 ? "Radial" : "Aimed") + "/" + frame.ToString("D2") + ".png";
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 320; importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false; importer.isReadable = true; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                // Aim projectiles are anchored on the damaging bronze/blue head, not on the exhaust trail.
                settings.spritePivot = row == 0 ? new Vector2(.5f, 100f/256) : new Vector2(174f/256, .5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            UnityEngine.Object.DestroyImmediate(source); AssetDatabase.SaveAssets();
            Debug.Log("[User Boss Projectiles] PASS original RGBA preserved; first two rows, 8 frames each; third row not imported.");
        }
    }
}
