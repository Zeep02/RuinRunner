using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class ImportItemEffects20261001
{
    const string Source = "ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/FX/";
    const string Root = "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/";
    static readonly JArray rows = new JArray();
    static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static void Import(string clip, string source, int width, int height, int count)
    {
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false); sheet.LoadImage(File.ReadAllBytes(Source + source));
        var hashes = new HashSet<string>();
        var factories = new SpriteDataProviderFactories(); factories.Init();
        for (int i = 0; i < count; i++)
        {
            var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = sheet.GetPixels(i * width, 0, width, height); frame.SetPixels(pixels); frame.Apply();
            bool nonempty = false; foreach (var p in pixels) if (p.a > 0) { nonempty = true; break; }
            if (!nonempty || !hashes.Add(Hash(frame.GetRawTextureData<byte>().ToArray()))) throw new Exception("Empty/duplicate pose " + clip + i);
            string path = Root + clip + "/" + i.ToString("D2") + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) throw new Exception("Do not overwrite existing source " + path);
            File.WriteAllBytes(path, frame.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot)) throw new Exception("No pivot capability " + path);
            var rects = provider.GetSpriteRects(); if (rects.Length != 1) throw new Exception("One PNG/pose required");
            rects[0].alignment = SpriteAlignment.Custom; rects[0].pivot = new Vector2(.5f, .5f);
            provider.SetSpriteRects(rects); provider.Apply(); importer.SaveAndReimport();
            rows.Add(new JObject { { "path", path }, { "source", Source + source }, { "sourceCell", new JArray(i * width, 0, width, height) }, { "sourceSha256", Hash(File.ReadAllBytes(Source + source)) }, { "ppu", 32 }, { "canvas", new JArray(width, height) } });
            UnityEngine.Object.DestroyImmediate(frame);
        }
        UnityEngine.Object.DestroyImmediate(sheet);
    }
    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Stop Play Mode before importing native frames");
        rows.Clear(); Import("ItemThunder", "Elemental/Thunder/SpriteSheet.png", 20, 28, 8);
        Import("ItemShield", "Magic/Shield/SpriteSheetYellow.png", 24, 26, 6);
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20261001/item-effects-native-import.json", new JObject { { "frames", rows }, { "note", "14 unique native CC0 Ninja Adventure poses. Exact cells, no resampling/repaint or duplicated intermediates. Common canvas/pivot within each clip." } }.ToString(), new UTF8Encoding(false));
        return "14 native poses imported with Point/uncompressed settings and common pivots";
    }
}
