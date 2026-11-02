using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using RuinRunner;
using Newtonsoft.Json.Linq;

public static class InspectNativeAtlas20261002
{
    public static string Main()
    {
        // Detect before interpreting pipeline-specific pixel camera settings.
        var pipeline = GraphicsSettings.currentRenderPipeline;
        string kind = pipeline == null ? "BuiltIn" : pipeline.GetType().FullName;
        var result = new JObject { { "pipeline", kind }, { "playing", EditorApplication.isPlaying },
            { "compiling", EditorApplication.isCompiling }, { "antiAliasing", QualitySettings.antiAliasing },
            { "anisotropicFiltering", QualitySettings.anisotropicFiltering.ToString() },
            { "spritePacker", EditorSettings.spritePackerMode.ToString() } };
        var catalogs = new JArray();
        for (int version = 0; version < NativeHeroFrameCatalog.CurrentVersions.Length; version++)
        {
            string name = NativeHeroFrameCatalog.CurrentVersions[version];
            var catalog = NativeHeroFrameCatalog.Load(version);
            int expected = version == 0 ? SscaryArmedFrames.UniqueSpriteCount : WeaponStandingFrames.UniqueSpriteCount;
            if (catalog == null || catalog.frames.Length != expected) throw new Exception("Missing native catalog " + name);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath("Assets/RuinRunner/ArtAtlases/NativeHero" + name + ".spriteatlasv2");
            var platform = importer.GetPlatformSettings("Standalone");
            if (!importer.includeInBuild || importer.textureSettings.filterMode != FilterMode.Point || importer.textureSettings.generateMipMaps ||
                importer.packingSettings.padding != 4 || importer.packingSettings.enableRotation || importer.packingSettings.enableTightPacking ||
                platform.format != TextureImporterFormat.RGBA32 || platform.textureCompression != TextureImporterCompression.Uncompressed)
                throw new Exception("Lossless atlas setting regression " + name);
            // Check actual dispatcher/Sprite identity, not just the new manifest.
            for (int i = 0; i < expected; i++)
                if (IndividualArt.Sprite(catalog.logicalRoot + "/" + i.ToString("D5")) != catalog.frames[i] ||
                    AssetDatabase.GetAssetPath(catalog.frames[i]) != NativeHeroFrameCatalog.SourceRoot + name + "/" + i.ToString("D5") + ".png")
                    throw new Exception("Native catalog dispatcher/reference mismatch " + name + "/" + i);
            catalogs.Add(new JObject { { "version", name }, { "sprites", expected }, { "format", platform.format.ToString() },
                { "sourceRoot", NativeHeroFrameCatalog.SourceRoot + name }, { "fullRect", true }, { "padding", 4 } });
        }
        foreach (string name in NativeHeroFrameCatalog.ArchivedVersions)
        {
            string first = Directory.GetFiles(NativeHeroFrameCatalog.ArchiveRoot + name,"*.png")[0];
            string id = NativeHeroFrameCatalog.LogicalRoot + name + "/" + Path.GetFileNameWithoutExtension(first);
            if (IndividualArt.Sprite(id) == null || !AssetDatabase.GetAssetPath(IndividualArt.Sprite(id)).StartsWith(NativeHeroFrameCatalog.ArchiveRoot))
                throw new Exception("Original editor authoring source unavailable " + name);
        }
        result["catalogs"] = catalogs;
        result["retiredEditorSourcesAvailable"] = true; result["result"] = "PASS";
        File.WriteAllText("ArtDirection/Review-20261002/native-frame-atlas/editor-catalog-settings.json",result.ToString(),new UTF8Encoding(false));
        return result.ToString();
    }
}
