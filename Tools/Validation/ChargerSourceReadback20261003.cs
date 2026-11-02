using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ChargerSourceReadback20261003
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before importer changes");
        var paths=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/RuinRunner/Resources/Art/Pixel/Separated/External/CraftPix/Orc3"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        if(paths.Length!=168)throw new Exception("Expected complete native source set");
        int changed=0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var path in paths)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer.isReadable)continue;
                // Existing Windows integrity validation compares packed sprites
                // against each unscaled source texture. Preserve that check.
                importer.isReadable=true;AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path);changed++;
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        foreach(var path in paths)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(!importer.isReadable||sprite.pivot!=new Vector2(32,24)||sprite.pixelsPerUnit!=32||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed)
                throw new Exception("Source readback changed native import geometry/settings: "+path);
        }
        return new {sources=paths.Length,changed};
    }
}
