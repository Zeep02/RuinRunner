using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class ImportCombatPresentation20260930
{
    const string Native="ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/";
    const string Effects="ArtDirection/ExternalCandidates/20260930/codemanu-effects/original/";
    const string Output="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/";
    static readonly List<string> sprites=new List<string>();
    static readonly JArray provenance=new JArray();
    static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
    static void Clip(string id,string source,int width,int height,float ppu)
    {
        var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(source));
        var cols=t.width/width;var rows=t.height/height;Directory.CreateDirectory(Output+id);
        for(var i=0;i<cols*rows;i++)
        {
            var frame=new Texture2D(width,height,TextureFormat.RGBA32,false);
            frame.SetPixels(t.GetPixels((i%cols)*width,t.height-(i/cols+1)*height,width,height));frame.Apply();
            var path=Output+id+"/"+i.ToString("D2")+".png";
            if(File.Exists(path))throw new Exception("Preserve existing art; this importer is a one-time adoption: "+path);
            File.WriteAllBytes(path,frame.EncodeToPNG());sprites.Add(path);
            provenance.Add(new JObject{{"path",path},{"source",source},{"sourceSha256",Hash(File.ReadAllBytes(source))},
                {"sourceCell",new JArray((i%cols)*width,(i/cols)*height,width,height)},{"ppu",ppu}});
            UnityEngine.Object.DestroyImmediate(frame);
        }
        UnityEngine.Object.DestroyImmediate(t);
    }
    static void Sound(string id,string source)
    {
        var path="Assets/RuinRunner/Resources/Audio/Combat/"+id+".wav";Directory.CreateDirectory(Path.GetDirectoryName(path));
        if(File.Exists(path))throw new Exception("Existing audio "+path);
        File.Copy(Native+"Audio/Sounds/"+source+".wav",path);
        provenance.Add(new JObject{{"path",path},{"source",Native+"Audio/Sounds/"+source+".wav"},{"sourceSha256",Hash(File.ReadAllBytes(path))}});
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before adoption");
        sprites.Clear();provenance.Clear();
        Clip("ImpactRing",Effects+"10_weaponhit_spritesheet.png",100,100,64);
        Clip("FlameArc",Effects+"6_flamelash_spritesheet.png",100,100,64);
        Clip("Arcane",Effects+"1_magicspell_spritesheet.png",100,100,64);
        Clip("Wind",Effects+"14_phantom_spritesheet.png",100,100,64);
        Clip("BlueFire",Effects+"3_bluefire_spritesheet.png",100,100,64);
        Clip("Ember",Effects+"16_sunburn_spritesheet.png",100,100,64);
        Clip("Charge",Effects+"4_casting_spritesheet.png",100,100,64);
        Clip("Void",Effects+"18_midnight_spritesheet.png",100,100,64);
        Clip("Energy",Native+"FX/Projectile/EnergyBall.png",16,16,32);
        Clip("RifleEnergy",Native+"FX/Projectile/BigEnergyBall.png",24,24,48);
        Clip("Fireball",Native+"FX/Projectile/Fireball.png",16,16,32);
        Clip("Shell",Native+"FX/Projectile/CanonBall.png",16,16,32);
        Clip("Arrow",Native+"FX/Projectile/Arrow.png",13,5,24);
        Clip("Bolt",Native+"FX/Projectile/BigKunai.png",35,9,48);
        Clip("Smoke",Native+"FX/Smoke/Smoke/SpriteSheet.png",32,32,32);
        foreach(var pair in new[]{
            new[]{"dagger","Whoosh & Slash/Slash"},new[]{"sword","Whoosh & Slash/Sword2"},
            new[]{"katana","Whoosh & Slash/Slash4"},new[]{"cleave","Whoosh & Slash/Whoosh2"},
            new[]{"mace","Hit & Impact/Impact3"},new[]{"bow","Whoosh & Slash/Launch"},
            new[]{"crossbow","Whoosh & Slash/Slash2"},new[]{"pistol","Elemental/Explosion4"},
            new[]{"rifle","Elemental/Fireball2"},new[]{"shotgun","Elemental/Explosion3"},
            new[]{"cannon","Elemental/Explosion2"},new[]{"magic","Magic & Skill/Magic2"},
            new[]{"impact","Hit & Impact/Hit3"},new[]{"heavy-impact","Hit & Impact/Impact5"},
            new[]{"block","Hit & Impact/Impact2"},new[]{"dash","Whoosh & Slash/Whoosh"},
            new[]{"hurt","Hit & Impact/Hit5"},new[]{"upgrade","Bonus/Bonus"}})Sound(pair[0],pair[1]);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var factories=new SpriteDataProviderFactories();factories.Init();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(JObject row in provenance)
            {
                var path=(string)row["path"];
                if(path.EndsWith(".wav"))
                {
                    var a=(AudioImporter)AssetImporter.GetAtPath(path);var s=a.defaultSampleSettings;
                    s.loadType=AudioClipLoadType.DecompressOnLoad;s.compressionFormat=AudioCompressionFormat.PCM;
                    s.preloadAudioData=true;a.defaultSampleSettings=s;a.forceToMono=true;
                    AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);continue;
                }
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=(float)row["ppu"];importer.filterMode=FilterMode.Point;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
                AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var path in sprites)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                var p=factories.GetSpriteEditorDataProviderFromObject(importer);p.InitSpriteEditorDataProvider();
                var cap=p.GetDataProvider<ISpriteFrameEditCapability>();
                if(cap==null||!cap.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Unsupported pivot editing "+path);
                var rects=p.GetSpriteRects();if(rects.Length!=1)throw new Exception("One sprite expected "+path);
                rects[0].alignment=SpriteAlignment.Center;rects[0].pivot=new Vector2(.5f,.5f);
                p.SetSpriteRects(rects);p.Apply();AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20260930/combat-presentation-import.json",new JObject{{"imagesAndSounds",provenance}}.ToString(),new UTF8Encoding(false));
        return sprites.Count+" unscaled native frames and18 authored audio clips adopted. Visual/motion review remains required.";
    }
}
