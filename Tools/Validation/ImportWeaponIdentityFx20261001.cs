using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

public static class ImportWeaponIdentityFx20261001
{
    const string Ninja="ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/";
    const string Warped="ArtDirection/ExternalCandidates/20261001/ansimuz-shooting/original/Warped shooting fx files/Assets/Pixelart/";
    const string Root="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/";
    static readonly JArray rows=new JArray();
    static readonly JObject geometry=new JObject();
    static string Hash(byte[] b){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
    static void Clip(string id,string source,int w,int h,int first,int count,Vector2 pivot)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(source));
        var seen=new HashSet<string>();var factories=new SpriteDataProviderFactories();factories.Init();int diameter=0;
        Directory.CreateDirectory(Root+id);
        for(int i=0;i<count;i++)
        {
            var frame=new Texture2D(w,h,TextureFormat.RGBA32,false);frame.SetPixels(texture.GetPixels((first+i)*w,0,w,h));frame.Apply();
            var pixels=frame.GetPixels32();int minX=w,minY=h,maxX=-1,maxY=-1;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(pixels[y*w+x].a>0){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            if(maxX<0||!seen.Add(Hash(frame.GetRawTextureData<byte>().ToArray())))throw new Exception("Empty/duplicate authored pose "+id+i);
            diameter=Math.Max(diameter,Math.Max(maxX-minX+1,maxY-minY+1));
            string path=Root+id+"/"+i.ToString("D2")+".png";if(File.Exists(path))throw new Exception("Preserve existing art "+path);
            File.WriteAllBytes(path,frame.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
            imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;
            imp.npotScale=TextureImporterNPOTScale.None;imp.wrapMode=TextureWrapMode.Clamp;imp.alphaIsTransparency=true;imp.SaveAndReimport();
            var provider=factories.GetSpriteEditorDataProviderFromObject(imp);provider.InitSpriteEditorDataProvider();
            var cap=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(cap==null||!cap.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot edit unsupported "+path);
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One frame per PNG required");
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=pivot;provider.SetSpriteRects(rects);provider.Apply();imp.SaveAndReimport();
            rows.Add(new JObject{{"path",path},{"source",source},{"cell",new JArray((first+i)*w,0,w,h)},{"sourceSha256",Hash(File.ReadAllBytes(source))},{"pixelsSha256",Hash(frame.GetRawTextureData<byte>().ToArray())},{"pivot",new JArray(pivot.x,pivot.y)}});
            UnityEngine.Object.DestroyImmediate(frame);
        }
        geometry[id]=diameter;UnityEngine.Object.DestroyImmediate(texture);
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before sprite import");rows.Clear();geometry.RemoveAll();
        // Retain the partial first import (incorrect leaf/flame cell dimensions) outside Resources.
        var archive="Assets/RuinRunner/ArtReview/WeaponFxPartialImport20261001";
        if(!Directory.Exists(archive))
        {
            Directory.CreateDirectory(archive);AssetDatabase.Refresh();
            foreach(var id in new[]{"RifleFlash","RifleWake","ParryCross","PiercingHit","RicochetHit","VoidBolt","PowderFlash","CannonBlast","GroundDust","TwinCrescent","CutStreak","MetalSpark","StoneChip","LeafChip"})
                if(Directory.Exists(Root+id)){var error=AssetDatabase.MoveAsset(Root+id,archive+"/"+id);if(!string.IsNullOrEmpty(error))throw new Exception(error);}
        }
        var center=new Vector2(.5f,.5f);
        Clip("RifleFlash",Warped+"spark/spritesheet.png",63,32,0,5,new Vector2(.12f,.5f));
        Clip("RifleWake",Warped+"waveform/spritesheet.png",95,32,0,4,center);
        Clip("ParryCross",Warped+"hits/Hits-3/spritesheet.png",32,32,0,5,center);
        Clip("PiercingHit",Warped+"hits/Hits-6/spritesheet.png",32,32,0,7,center);
        Clip("RicochetHit",Warped+"hits/Hits-2/spritesheet.png",32,32,0,7,center);
        Clip("VoidBolt",Warped+"charged/spritesheet.png",63,48,0,6,new Vector2(.60f,.5f));
        Clip("PowderFlash",Ninja+"FX/Elemental/Flam/SpriteSheet.png",25,30,0,8,new Vector2(.20f,.42f));
        Clip("CannonBlast",Ninja+"FX/Elemental/Explosion/SpriteSheet.png",40,40,2,7,center);
        Clip("GroundDust",Ninja+"FX/Smoke/SmokeCircular/SpriteSheet.png",24,14,0,10,center);
        Clip("TwinCrescent",Ninja+"FX/Attack/SlashDoubleCurved/SpriteSheet.png",32,32,0,4,center);
        Clip("CutStreak",Ninja+"FX/Attack/Cut/SpriteSheet.png",32,32,0,4,center);
        // Particles are original visual variants, not invented animation poses.
        Clip("MetalSpark",Ninja+"FX/Particle/Spark.png",10,8,0,7,center);
        Clip("StoneChip",Ninja+"FX/Particle/RockGray.png",16,16,0,5,center);
        Clip("LeafChip",Ninja+"FX/Particle/Leaf.png",9,7,0,8,center);
        Clip("Slug", "ArtDirection/ExternalCandidates/20260922/scrython-guns/original/GunsAssetPack/Individual_Gun_Sprites/Extras/Bullet.png",16,16,0,1,new Vector2(.2f,.5f));
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20261001/weapon-fx/native-import.json",new JObject{{"frames",rows},{"visibleDiameters",geometry},{"note","Exact authored source cells, no resampling or duplicate intermediates. Particle variants are secondary motion sprites."}}.ToString(),new UTF8Encoding(false));
        return geometry.ToString();
    }
    public static string ImportHeavy()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        var path="ArtDirection/Review-20261001/weapon-fx/native-import.json";
        var previous=JObject.Parse(File.ReadAllText(path,Encoding.UTF8));rows.Clear();geometry.RemoveAll();
        foreach(var row in previous["frames"])rows.Add(row.DeepClone());
        foreach(var entry in (JObject)previous["visibleDiameters"])geometry[entry.Key]=entry.Value.DeepClone();
        const string source="ArtDirection/ExternalCandidates/20260921/tiny-swords/original/Tiny Swords (Free Pack)/Particle FX/";
        Clip("HeavyBlast",source+"Explosion_02.png",192,192,0,10,new Vector2(.5f,.5f));
        Clip("SlamDust",source+"Dust_02.png",64,64,0,10,new Vector2(.5f,.5f));
        AssetDatabase.SaveAssets();
        File.WriteAllText(path,new JObject{{"frames",rows},{"visibleDiameters",geometry},{"note","Exact authored source cells. Heavy blast/dust use higher-detail source art; no resampling/repaint or synthetic intermediates. Particle variants are secondary motion."}}.ToString(),new UTF8Encoding(false));
        return geometry.ToString();
    }
}
