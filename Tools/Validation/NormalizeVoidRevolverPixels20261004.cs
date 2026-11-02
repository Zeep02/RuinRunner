using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NormalizeVoidRevolverPixels20261004
{
    public static string Main()
    {
        if (Application.isPlaying) throw new Exception("Stop Play Mode before reimport.");
        const string path = "Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon/void_revolver.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new Exception("Missing original revolver.");
        var guid = AssetDatabase.AssetPathToGUID(path);
        var pivot = sprite.pivot;
        var rect = sprite.rect;
        string hash;
        using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        var backup = "ArtDirection/Review-20261004/item-pixel-presentation/void-revolver-before/";
        Directory.CreateDirectory(backup);
        if (!File.Exists(backup + "void_revolver.png.meta"))
        {
            File.Copy(path, backup + "void_revolver.png");
            File.Copy(path + ".meta", backup + "void_revolver.png.meta");
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.spritePixelsPerUnit = 32;
        importer.SaveAndReimport();
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        using (var sha = SHA256.Create())
            if (Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))) != hash) throw new Exception("Source pixels changed.");
        if (sprite.pixelsPerUnit != 32 || sprite.pivot != pivot || sprite.rect != rect || AssetDatabase.AssetPathToGUID(path) != guid)
            throw new Exception("Import identity or pivot mismatch.");
        File.WriteAllText(backup + "result.txt", "PASS source bytes, GUID, rect and pixel pivot preserved. PPU48 -> 32; held native scale1. Actual eight-direction size/readability requires separate review.\n", new UTF8Encoding(false));
        return "PASS void_revolver PPU32; original pixels/pivot/GUID preserved.";
    }
}
