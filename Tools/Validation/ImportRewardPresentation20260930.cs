using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class ImportRewardPresentation20260930
{
    const string Ninja="ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/";
    const string Somi="ArtDirection/ExternalCandidates/20260930/somi-chests/original/Chests/";
    const string Root="Assets/RuinRunner/Resources/Art/Pixel/Separated/";
    static readonly JArray rows=new JArray();
    static string Hash(byte[] data){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    static void Frames(string id,string source,int width,int height,int count,float ppu,bool grounded)
    {
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes(source));
        var hashes=new HashSet<string>();
        for(int i=0;i<count;i++)
        {
            int x=i*width;var pixels=sheet.GetPixels(x,0,width,height);
            var frame=new Texture2D(width,height,TextureFormat.RGBA32,false);frame.SetPixels(pixels);frame.Apply();
            bool nonempty=false;foreach(var pixel in pixels)if(pixel.a>0){nonempty=true;break;}
            var bytes=frame.GetRawTextureData<byte>().ToArray();
            if(!nonempty||!hashes.Add(Hash(bytes)))throw new Exception("Empty or duplicate reward pose "+id+"/"+i);
            var path=Root+id+"/"+i.ToString("D2")+".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if(File.Exists(path))throw new Exception("Preserve existing reward artwork "+path);
            File.WriteAllBytes(path,frame.EncodeToPNG());
            rows.Add(new JObject{{"path",path},{"source",source},{"sourceCell",new JArray(x,0,width,height)},
                {"sourceSha256",Hash(File.ReadAllBytes(source))},{"ppu",ppu},{"grounded",grounded}});
            UnityEngine.Object.DestroyImmediate(frame);
        }
        UnityEngine.Object.DestroyImmediate(sheet);
    }
    static void Audio(string id,string source)
    {
        var path="Assets/RuinRunner/Resources/Audio/Combat/"+id+".wav";
        if(File.Exists(path))throw new Exception("Preserve existing sound "+path);
        File.Copy(Ninja+"Audio/Sounds/"+source+".wav",path);
        rows.Add(new JObject{{"path",path},{"source",Ninja+"Audio/Sounds/"+source+".wav"},{"sourceSha256",Hash(File.ReadAllBytes(path))}});
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode for native art import");
        rows.Clear();
        Frames("RewardChest/Common",Somi+"TheClassic.png",32,32,6,24,true);
        Frames("RewardChest/Uncommon",Somi+"TheOvergrownt.png",32,32,6,24,true);
        Frames("RewardChest/Rare",Somi+"TheMariner.png",32,32,6,24,true);
        Frames("RewardChest/Epic",Somi+"The Abyssal.png",32,32,6,24,true);
        Frames("RewardChest/Legendary",Somi+"TheRoyal.png",32,32,6,24,true);
        Frames("CombatFX/RewardSpark",Ninja+"FX/Magic/Spark/SpriteSheet.png",30,35,9,32,false);
        Frames("CombatFX/RewardRing",Ninja+"FX/Magic/Circle/SpriteSheetWhite.png",32,32,4,32,false);
        Frames("CombatFX/RewardGlint",Ninja+"FX/Magic/Circle/SpriteSheetSpark.png",32,32,6,32,false);
        Audio("reward-common","Bonus/Gold1");Audio("reward-uncommon","Bonus/Gold2");
        Audio("reward-rare","Bonus/Bonus2");Audio("reward-epic","Bonus/PowerUp1");
        Audio("reward-legendary","Bonus/PowerUp2");Audio("reward-pickup","Bonus/Coin3");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var factories=new SpriteDataProviderFactories();factories.Init();
        foreach(JObject row in rows)
        {
            var path=(string)row["path"];
            if(path.EndsWith(".wav"))
            {
                var a=(AudioImporter)AssetImporter.GetAtPath(path);var sample=a.defaultSampleSettings;
                sample.loadType=AudioClipLoadType.DecompressOnLoad;sample.compressionFormat=AudioCompressionFormat.PCM;
                sample.preloadAudioData=true;a.defaultSampleSettings=sample;a.forceToMono=true;a.SaveAndReimport();continue;
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=(float)row["ppu"];importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;
            importer.SaveAndReimport();
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var cap=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(cap==null||!cap.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("No pivot capability "+path);
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected one sprite "+path);
            rects[0].alignment=SpriteAlignment.Custom;
            rects[0].pivot=(bool)row["grounded"]?new Vector2(.5f,0):new Vector2(.5f,.5f);
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20260930/reward-presentation-import.json",new JObject{{"imagesAndSounds",rows},
            {"note","Six unique native opening poses per chest. Duplicate open holds and reverse closing frames excluded. No resampling, repaint or invented intermediate poses."}}.ToString(),new UTF8Encoding(false));
        return "49 unique native chest/reward frames and 6 native reward cues imported; visual review required.";
    }
}
