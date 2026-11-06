// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD] Built-in sprite catalogs. Source PNGs/GUIDs remain in Assets.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public sealed class NativeHeroAtlasPrebuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -90;
        public void OnPreprocessBuild(BuildReport report) => Generate();

        public static string Generate()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before catalog authoring.");
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                throw new Exception("SpriteAtlas V2 did not enable.");
            EnsureFolder("Assets/RuinRunner/Resources/Art/NativeHero");
            EnsureFolder("Assets/RuinRunner/ArtAtlases");
            int total = 0;
            for (int version = 0; version < NativeHeroFrameCatalog.CurrentVersions.Length; version++)
            {
                string name = NativeHeroFrameCatalog.CurrentVersions[version];
                string sourceRoot = NativeHeroFrameCatalog.SourceRoot + name;
                if (!AssetDatabase.IsValidFolder(sourceRoot))
                    throw new Exception("Missing current native sources: " + sourceRoot);
                string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceRoot })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                int expected = NativeHeroFrameCatalog.ExpectedFrames(version);
                if (paths.Length != expected)
                    throw new Exception("Incomplete native frame set: " + name);
                string catalogPath = "Assets/RuinRunner/Resources/" + NativeHeroFrameCatalog.CatalogRoot + name + ".asset";
                var catalog = AssetDatabase.LoadAssetAtPath<NativeHeroFrameCatalog>(catalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<NativeHeroFrameCatalog>();
                    AssetDatabase.CreateAsset(catalog, catalogPath);
                }
                catalog.logicalRoot = NativeHeroFrameCatalog.LogicalRoot + name;
                catalog.frames = new Sprite[expected];
                catalog.pngSha256 = new string[expected];
                catalog.visiblePixelSha256 = new string[expected];
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    for (int i = 0; i < paths.Length; i++)
                    {
                        string path = paths[i];
                        if (Path.GetFileName(path) != i.ToString("D5") + ".png")
                            throw new Exception("Non-contiguous native frame: " + path);
                        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                        var settings = new TextureImporterSettings();
                        importer.ReadTextureSettings(settings);
                        if (sprites.Length != 1 || sprites[0].rect != new Rect(0, 0, 48, 64) ||
                            sprites[0].pivot != new Vector2(24, 22) || sprites[0].pixelsPerUnit != 32 ||
                            importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                            importer.textureCompression != TextureImporterCompression.Uncompressed ||
                            settings.spriteMeshType != SpriteMeshType.FullRect)
                            throw new Exception("Invalid native sprite geometry/import: " + path);
                        byte[] png = File.ReadAllBytes(path);
                        if (!decoded.LoadImage(png) || decoded.width != 48 || decoded.height != 64)
                            throw new Exception("Invalid PNG: " + path);
                        catalog.frames[i] = sprites[0];
                        using (var hash = SHA256.Create())
                            catalog.pngSha256[i] = BitConverter.ToString(hash.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
                        catalog.visiblePixelSha256[i] = NativeHeroFrameCatalog.VisiblePixelHash(decoded.GetPixels32(), 48, new RectInt(0, 0, 48, 64));
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }
                EditorUtility.SetDirty(catalog);

                string atlasPath = "Assets/RuinRunner/ArtAtlases/NativeHero" + name + ".spriteatlasv2";
                var atlas = new SpriteAtlasAsset();
                atlas.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(sourceRoot) });
                SpriteAtlasAsset.Save(atlas, atlasPath);
                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
                var atlasImporter = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
                atlasImporter.includeInBuild = true;
                var packing = atlasImporter.packingSettings;
                packing.padding = 4;
                packing.enableRotation = false;
                packing.enableTightPacking = false;
                packing.enableAlphaDilation = true;
                atlasImporter.packingSettings = packing;
                var texture = atlasImporter.textureSettings;
                texture.filterMode = FilterMode.Point;
                texture.generateMipMaps = false;
                // Full packed-pixel verification runs in the actual Windows build.
                texture.readable = true;
                atlasImporter.textureSettings = texture;
                atlasImporter.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = "DefaultTexturePlatform",
                    maxTextureSize = 2048,
                    format = TextureImporterFormat.RGBA32,
                    textureCompression = TextureImporterCompression.Uncompressed
                });
                atlasImporter.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = "Standalone",
                    overridden = true,
                    maxTextureSize = 2048,
                    format = TextureImporterFormat.RGBA32,
                    textureCompression = TextureImporterCompression.Uncompressed
                });
                atlasImporter.SaveAndReimport();
                total += expected;
            }
            AssetDatabase.SaveAssets();
            return total + " native individual PNG references; " + NativeHeroFrameCatalog.CurrentVersions.Length + " lossless built-in atlases, original pixels/pivots preserved.";
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            if (string.IsNullOrEmpty(guid))
                throw new Exception("Could not create asset folder: " + folder);
        }
    }
}
