using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Read-only inventory. A clean importer report is not a visual art approval.
public static class ArtImportAudit
{
    public static string Main()
    {
        var output = "Logs/Validation/2026-09-21-art-audit";
        Directory.CreateDirectory(output);
        var rows = new StringBuilder("path,sourceWidth,sourceHeight,importWidth,importHeight,type,filter,mips,compression,maxSize,ppu,pivotX,pivotY,spriteCount\n");
        var textures = 0; var sprites = 0; var resized = 0; var filtered = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[]{"Assets/RuinRunner"}))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.GetSourceTextureWidthAndHeight(out var sw, out var sh);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;
            var count = 0; var pivot = Vector2.zero;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) { count++; pivot = sprite.pivot; }
            rows.AppendLine(string.Join(",", path, sw, sh, texture.width, texture.height, importer.textureType,
                importer.filterMode, importer.mipmapEnabled, importer.textureCompression, importer.maxTextureSize,
                importer.spritePixelsPerUnit, pivot.x, pivot.y, count));
            textures++; sprites += count;
            if (sw != texture.width || sh != texture.height) resized++;
            if (path.Contains("/Art/Pixel/") && (importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)) filtered++;
        }
        File.WriteAllText(output + "/imports.csv", rows.ToString());
        var result = "Textures=" + textures + "; sprites=" + sprites + "; resized imports=" + resized +
            "; pixel filter/compression violations=" + filtered + "; pipeline=" +
            (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name);
        File.WriteAllText(output + "/imports-summary.txt", result);
        return result;
    }
}
