using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
public static class VerifyGreatswordNative20261001
{
    public static object Main()
    {
        const string source="ArtDirection/ExternalCandidates/20261001/frostwindz-slashes/original/128x128/Slash 3/color5/frames";
        const string active="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/GreatswordCut";
        for(int i=0;i<9;i++)
        {
            var path=active+"/"+i.ToString("D2")+".png";
            using(var sha=SHA256.Create())
                if(!sha.ComputeHash(File.ReadAllBytes(path)).SequenceEqual(sha.ComputeHash(File.ReadAllBytes(source+"/Slash3_color5_frame"+(i+1)+".png"))))throw new Exception("Modified source "+path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite.rect.size!=new Vector2(128,128)||sprite.pivot!=new Vector2(64,64)||sprite.pixelsPerUnit!=32||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed)throw new Exception("Native geometry/import settings "+path);
        }
        if(AssetDatabase.IsValidFolder("Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/ChargedCut"))throw new Exception("Rejected branching wedge remains in Resources.");
        return new {exactSourcePngs=9,geometry="128x128 / 32PPU / pivot64,64 / Point / uncompressed / no mipmaps",rejectedResourcesAbsent=true};
    }
}
