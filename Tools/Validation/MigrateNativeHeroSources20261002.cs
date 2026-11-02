using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;
using RuinRunner.EditorTools;
using Newtonsoft.Json.Linq;

// Reversible AssetDatabase folder moves. No source/metadata deletion or rewrite.
public static class MigrateNativeHeroSources20261002
{
    const string Review = "ArtDirection/Review-20261002/native-frame-atlas/";
    const string Previous = "Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMaleCombat/";
    static void Stopped()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new Exception("Native source migration requires a stopped, compiled Editor.");
    }
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
    static void AssertWithinAssets(string path)
    {
        string boundary = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new Exception("Outside Assets: " + path);
    }
    public static string Inventory()
    {
        Stopped(); Directory.CreateDirectory(Review);
        if (File.Exists(Review + "source-inventory.json")) throw new Exception("Preserve the existing migration inventory.");
        var groups = new JArray(); var rows = new JArray();
        foreach (string version in NativeHeroFrameCatalog.CurrentVersions.Concat(NativeHeroFrameCatalog.ArchivedVersions))
        {
            bool active = NativeHeroFrameCatalog.CurrentVersions.Contains(version);
            string source = Previous + version, destination = (active ? NativeHeroFrameCatalog.SourceRoot : NativeHeroFrameCatalog.ArchiveRoot) + version;
            AssertWithinAssets(source); AssertWithinAssets(destination);
            if (!AssetDatabase.IsValidFolder(source) || Directory.Exists(destination)) throw new Exception("Unexpected migration folder: " + source);
            var files = Directory.GetFiles(source, "*.png", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new Exception("No source PNGs: " + source);
            groups.Add(new JObject { { "version", version }, { "active", active }, { "before", source }, { "after", destination },
                { "folderGuid", AssetDatabase.AssetPathToGUID(source) }, { "folderMetaSha256", Hash(source + ".meta") }, { "sprites", files.Length } });
            foreach (string native in files)
            {
                string path = native.Replace('\\', '/');
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid) || !File.Exists(path + ".meta")) throw new Exception("Untracked native source: " + path);
                rows.Add(new JObject { { "before", path }, { "after", destination + path.Substring(source.Length) }, { "guid", guid },
                    { "pngSha256", Hash(path) }, { "metaSha256", Hash(path + ".meta") } });
            }
            File.WriteAllText(Review + "inventory-progress.txt", version + ": " + rows.Count + " source PNGs inspected", new UTF8Encoding(false));
        }
        var report = new JObject { { "createdAtUtc", DateTime.UtcNow.ToString("o") }, { "groups", groups }, { "sources", rows } };
        File.WriteAllText(Review + "source-inventory.json", report.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Review + "source-folder-aliases.json", groups.ToString(), new UTF8Encoding(false));
        return "Recorded " + rows.Count + " PNG bytes, metadata bytes and original GUIDs before any move.";
    }
    public static string Move()
    {
        Stopped(); var inventory = JObject.Parse(File.ReadAllText(Review + "source-inventory.json", Encoding.UTF8));
        foreach (JObject group in inventory["groups"])
        {
            string source = group.Value<string>("before"), destination = group.Value<string>("after");
            AssertWithinAssets(source); AssertWithinAssets(destination);
            NativeHeroAtlasPrebuild.EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
        }
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (JObject group in inventory["groups"])
            {
                string source = group.Value<string>("before"), destination = group.Value<string>("after");
                if (AssetDatabase.IsValidFolder(destination) && !AssetDatabase.IsValidFolder(source))
                {
                    if (AssetDatabase.AssetPathToGUID(destination) != group.Value<string>("folderGuid")) throw new Exception("Partial move GUID mismatch: " + destination);
                    continue;
                }
                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);
                File.WriteAllText(Review + "move-progress.txt", source + " -> " + destination, new UTF8Encoding(false));
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets();
        return Verify();
    }
    public static string Verify()
    {
        Stopped(); var inventory = JObject.Parse(File.ReadAllText(Review + "source-inventory.json", Encoding.UTF8)); int count = 0;
        foreach (JObject group in inventory["groups"])
        {
            string source = group.Value<string>("before"), destination = group.Value<string>("after");
            if (Directory.Exists(source) || AssetDatabase.AssetPathToGUID(destination) != group.Value<string>("folderGuid") ||
                Hash(destination + ".meta") != group.Value<string>("folderMetaSha256")) throw new Exception("Folder identity changed: " + destination);
        }
        foreach (JObject row in inventory["sources"])
        {
            string path = row.Value<string>("after");
            if (AssetDatabase.AssetPathToGUID(path) != row.Value<string>("guid") || Hash(path) != row.Value<string>("pngSha256") ||
                Hash(path + ".meta") != row.Value<string>("metaSha256")) throw new Exception("Source/metadata identity changed: " + path);
            count++;
        }
        var result = new JObject { { "result", "PASS" }, { "verifiedAtUtc", DateTime.UtcNow.ToString("o") }, { "individualPngs", count },
            { "pngBytesChanged", 0 }, { "metadataBytesChanged", 0 }, { "guidsChanged", 0 }, { "scope", "Actual AssetDatabase moves and every PNG/metadata byte; not packed Player validation." } };
        File.WriteAllText(Review + "migration-result.json", result.ToString(), new UTF8Encoding(false));
        return result.ToString();
    }
    public static string Generate()
    {
        Stopped();
        string native = NativeHeroAtlasPrebuild.Generate();
        string external = TerrainAtlasPrebuild.Generate();
        File.WriteAllText(Review + "generation-result.txt", native + "\n" + external, new UTF8Encoding(false));
        return native + "\n" + external;
    }
}
