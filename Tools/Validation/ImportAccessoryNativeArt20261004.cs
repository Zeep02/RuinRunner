using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Run explicitly through the connected Editor's script runner. No AssetPostprocessor/MenuItem.
// This source file alone changes nothing. Main preflights every selected item before any asset write.
public static class ImportAccessoryNativeArt20261004
{
    const string Review = "ArtDirection/Review-20261004/item-style/";
    const string TargetRoot = "Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Accessory/";
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

    sealed class Entry
    {
        public string Id, Source, Target, SourceHash, License, AssetGuid, SpriteId;
        public long LocalFileId;
        public int Width, Height;
        public SpriteRect Rect;
    }

    static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }

    static string WorkspacePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new Exception("Manifest path is empty.");
        var root = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(path);
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new Exception("Path is outside project: " + path);
        return absolute;
    }

    static Vector2Int PngSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        byte[] magic = { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (bytes.Length < 33 || !bytes.Take(8).SequenceEqual(magic) || Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR")
            throw new Exception("Not a complete PNG header: " + path);
        int BigEndian(int i) => (bytes[i] << 24) | (bytes[i + 1] << 16) | (bytes[i + 2] << 8) | bytes[i + 3];
        return new Vector2Int(BigEndian(16), BigEndian(20));
    }

    static ISpriteEditorDataProvider Provider(TextureImporter importer, SpriteDataProviderFactories factories)
    {
        var data = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (data == null) throw new Exception("Sprite data provider unavailable: " + importer.assetPath);
        data.InitSpriteEditorDataProvider();
        var edit = data.GetDataProvider<ISpriteFrameEditCapability>();
        if (edit == null || !edit.GetEditCapability().HasCapability(EEditCapability.EditPivot) ||
            !edit.GetEditCapability().HasCapability(EEditCapability.EditSpriteRect))
            throw new Exception("Sprite rect/pivot edit capability unavailable: " + importer.assetPath);
        return data;
    }

    static Entry[] Preflight(SpriteDataProviderFactories factories)
    {
        var manifest = JArray.Parse(File.ReadAllText(Review + "accepted-selection.json", Encoding.UTF8));
        var expected = new HashSet<string>(RuinRunner.AccessoryLootTable.All.Select(x => x.Id), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<Entry>();
        foreach (JObject row in manifest)
        {
            var e = new Entry { Id = (string)row["id"], Source = (string)row["source"], Target = (string)row["target"],
                Width = (int)row["nativeWidth"], Height = (int)row["nativeHeight"],
                SourceHash = ((string)row["sourceSha256"] ?? "").ToLowerInvariant(), License = (string)row["license"] };
            if (string.IsNullOrWhiteSpace(e.Id) || !expected.Contains(e.Id) || !seen.Add(e.Id))
                throw new Exception("Unknown/duplicate active accessory: " + e.Id);
            var target = TargetRoot + e.Id + ".png";
            if (!string.Equals(WorkspacePath(e.Target), WorkspacePath(target), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Target must be existing accessory PNG: " + e.Id);
            e.Target = target;
            e.Source = WorkspacePath(e.Source);
            if (string.Equals(e.Source, WorkspacePath(e.Target), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Source cannot be target: " + e.Id);
            if (!File.Exists(e.Source) || !File.Exists(e.Target) || !File.Exists(e.Target + ".meta"))
                throw new Exception("Source or existing target/meta missing: " + e.Id);
            if (string.IsNullOrWhiteSpace(e.License) || e.SourceHash.Length != 64 || Hash(e.Source) != e.SourceHash)
                throw new Exception("License/hash preflight failed: " + e.Id);
            if (e.Width < 1 || e.Height < 1 || e.Width > 512 || e.Height > 512 || PngSize(e.Source) != new Vector2Int(e.Width, e.Height))
                throw new Exception("Native PNG size mismatch: " + e.Id);
            var importer = AssetImporter.GetAtPath(e.Target) as TextureImporter;
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Single)
                throw new Exception("Existing target must be single-sprite TextureImporter: " + e.Id);
            var rects = Provider(importer, factories).GetSpriteRects();
            if (rects.Length != 1) throw new Exception("Expected exactly one existing sprite: " + e.Id);
            e.Rect = rects[0]; e.SpriteId = e.Rect.spriteID.ToString();
            e.AssetGuid = AssetDatabase.AssetPathToGUID(e.Target);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.Target);
            if (sprite == null || string.IsNullOrEmpty(e.AssetGuid) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out e.LocalFileId) || guid != e.AssetGuid)
                throw new Exception("Existing sprite identity cannot be captured: " + e.Id);
            entries.Add(e);
        }
        if (!seen.SetEquals(expected)) throw new Exception("Manifest must cover all " + expected.Count + " active accessories exactly once.");
        return entries.ToArray();
    }

    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Stop Play Mode before accessory import.");
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var entries = Preflight(factories);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var backup = Review + "before/" + stamp + "/";
        Directory.CreateDirectory(backup);
        File.Copy(Review + "accepted-selection.json", backup + "accepted-selection.json");
        var originals = new JArray();
        foreach (var e in entries)
        {
            File.Copy(e.Target, backup + e.Id + ".png");
            File.Copy(e.Target + ".meta", backup + e.Id + ".png.meta");
            originals.Add(new JObject { ["id"] = e.Id, ["guid"] = e.AssetGuid, ["spriteId"] = e.SpriteId,
                ["localFileId"] = e.LocalFileId, ["pngSha256"] = Hash(e.Target), ["metaSha256"] = Hash(e.Target + ".meta") });
        }
        File.WriteAllText(backup + "identity.json", originals.ToString(), Utf8);
        var records = new JArray();
        try
        {
            foreach (var e in entries)
            {
                if (Hash(e.Source) != e.SourceHash) throw new Exception("Source changed after preflight: " + e.Id);
                File.Copy(e.Source, e.Target, true); // Byte-for-byte: no crop, padding, palette change or resampling.
                AssetDatabase.ImportAsset(e.Target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(e.Target);
                var data = Provider(importer, factories); // Check capability again before applying sprite metadata.
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.isReadable = true;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                var defaults = importer.GetDefaultPlatformTextureSettings();
                defaults.textureCompression = TextureImporterCompression.Uncompressed;
                defaults.crunchedCompression = false; defaults.maxTextureSize = Mathf.Max(512, defaults.maxTextureSize);
                importer.SetPlatformTextureSettings(defaults);
                foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                {
                    var p = importer.GetPlatformTextureSettings(platform);
                    if (!p.overridden) continue;
                    p.textureCompression = TextureImporterCompression.Uncompressed; p.crunchedCompression = false;
                    p.format = TextureImporterFormat.RGBA32; p.maxTextureSize = Mathf.Max(512, p.maxTextureSize);
                    importer.SetPlatformTextureSettings(p);
                }
                // Retain the original name and spriteID; only geometry changes for the new native canvas.
                e.Rect.rect = new Rect(0, 0, e.Width, e.Height);
                e.Rect.alignment = SpriteAlignment.Center;
                e.Rect.pivot = new Vector2(.5f, .5f);
                data.SetSpriteRects(new[] { e.Rect }); data.Apply();
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.Target);
                importer = (TextureImporter)AssetImporter.GetAtPath(e.Target);
                var finalRects = Provider(importer, factories).GetSpriteRects();
                importer.ReadTextureSettings(settings);
                if (sprite == null || sprite.texture.width != e.Width || sprite.texture.height != e.Height ||
                    sprite.rect != new Rect(0, 0, e.Width, e.Height) || sprite.pixelsPerUnit != 32 ||
                    Vector2.Distance(sprite.pivot, new Vector2(e.Width * .5f, e.Height * .5f)) > .001f ||
                    importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.isReadable ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed || settings.spriteMeshType != SpriteMeshType.FullRect)
                    throw new Exception("Imported geometry/texture settings mismatch: " + e.Id);
                if (finalRects.Length != 1 || finalRects[0].spriteID.ToString() != e.SpriteId ||
                    AssetDatabase.AssetPathToGUID(e.Target) != e.AssetGuid ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localFileId) ||
                    guid != e.AssetGuid || localFileId != e.LocalFileId)
                    throw new Exception("Existing asset/sprite identity changed: " + e.Id);
                if (Hash(e.Target) != e.SourceHash || PngSize(e.Target) != new Vector2Int(e.Width, e.Height))
                    throw new Exception("Native source bytes changed: " + e.Id);
                records.Add(new JObject { ["id"] = e.Id, ["target"] = e.Target, ["source"] = e.Source,
                    ["sourceSha256"] = e.SourceHash, ["targetSha256"] = Hash(e.Target), ["guid"] = e.AssetGuid,
                    ["spriteId"] = e.SpriteId, ["localFileId"] = e.LocalFileId, ["nativeWidth"] = e.Width,
                    ["nativeHeight"] = e.Height, ["license"] = e.License, ["ppu"] = 32, ["sourcePixelsUnchanged"] = true,
                    ["identityPreserved"] = true, ["textureSettingsVerified"] = true });
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Review + "import-result.json", new JObject { ["result"] = "PASS", ["utc"] = DateTime.UtcNow.ToString("O"),
                ["backup"] = backup, ["accessories"] = records }.ToString(), Utf8);
            return "Imported " + entries.Length + " accessories byte-for-byte; native dimensions, GUID, SpriteID, local file ID, pivot, PPU and texture settings verified. Backup: " + backup;
        }
        catch (Exception error)
        {
            File.WriteAllText(Review + "import-result.json", new JObject { ["result"] = "FAIL", ["utc"] = DateTime.UtcNow.ToString("O"),
                ["backup"] = backup, ["completed"] = records, ["error"] = error.ToString(), ["automaticRollback"] = false }.ToString(), Utf8);
            throw;
        }
    }
}
