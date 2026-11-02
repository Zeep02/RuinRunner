using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json.Linq;

// Copies reviewed native pixels without resizing/repainting. Runs in the connected Editor.
public static class RespriteEquipment20260930
{
    const string Review="ArtDirection/Review-20260930/";
    const string Selection="ArtDirection/ExternalCandidates/20260922/native-item-selection/";
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static ISpriteEditorDataProvider Provider(TextureImporter importer,SpriteDataProviderFactories factories)
    {
        var p=factories.GetSpriteEditorDataProviderFromObject(importer);p.InitSpriteEditorDataProvider();
        var c=p.GetDataProvider<ISpriteFrameEditCapability>();
        if(c==null||!c.GetEditCapability().HasCapability(EEditCapability.EditPivot)||!c.GetEditCapability().HasCapability(EEditCapability.EditSpriteRect))
            throw new Exception("Sprite importer cannot edit rect and pivot: "+importer.assetPath);
        return p;
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode for art adoption");
        var changes=JObject.Parse(File.ReadAllText(Review+"equipment-resprite.json",Encoding.UTF8));
        var weapons=JObject.Parse(File.ReadAllText(Selection+"all-weapons-import.json",Encoding.UTF8));
        var accessories=JObject.Parse(File.ReadAllText(Selection+"accessory-import.json",Encoding.UTF8));
        var factories=new SpriteDataProviderFactories();factories.Init();
        var backup=Review+"before/";Directory.CreateDirectory(backup);
        foreach(JObject row in changes["images"])
        {
            var path="Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/"+(string)row["kind"]+"/"+(string)row["id"]+".png";
            Provider((TextureImporter)AssetImporter.GetAtPath(path),factories);
            var save=backup+(string)row["kind"]+"/"+(string)row["id"]+".png";Directory.CreateDirectory(Path.GetDirectoryName(save));
            if(!File.Exists(save)){File.Copy(path,save);File.Copy(path+".meta",save+".meta");}
        }
        foreach(var path in new[]{Selection+"all-weapons-import.json",Selection+"accessory-import.json","Assets/RuinRunner/Runtime/Player/NativeWeaponArt.cs"})
            if(!File.Exists(backup+Path.GetFileName(path)))File.Copy(path,backup+Path.GetFileName(path));
        foreach(JObject row in changes["images"])
        {
            var source=(string)row["source"];var id=(string)row["id"];var kind=(string)row["kind"];
            var path="Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/"+kind+"/"+id+".png";
            var input=new Texture2D(2,2,TextureFormat.RGBA32,false);input.LoadImage(File.ReadAllBytes(source));
            var output=new Texture2D(input.width+4,input.height+4,TextureFormat.RGBA32,false);
            output.SetPixels32(new Color32[output.width*output.height]);output.SetPixels32(2,2,input.width,input.height,input.GetPixels32());output.Apply();
            File.WriteAllBytes(path,output.EncodeToPNG());
            row["path"]=path;row["sourceCell"]=null;row["sourceSize"]=new JArray(input.width,input.height);
            row["spriteSize"]=new JArray(output.width,output.height);row["sourceSha256"]=Hash(File.ReadAllBytes(source));
            var pivot=new Vector2(.5f,.5f);
            if(kind=="Weapon")
            {
                row["grip"]=new JArray((float)row["grip"][0]+2,(float)row["grip"][1]+2);
                row["muzzle"]=new JArray((float)row["muzzle"][0]+2,(float)row["muzzle"][1]+2);
                pivot=new Vector2((float)row["grip"][0]/output.width,1-(float)row["grip"][1]/output.height);
            }
            row["pivot"]=new JArray(pivot.x,pivot.y);
            var entries=(JArray)(kind=="Weapon"?weapons:accessories)["images"];
            for(int i=0;i<entries.Count;i++)if((string)entries[i]["id"]==id){entries[i]=row.DeepClone();break;}
            UnityEngine.Object.DestroyImmediate(input);UnityEngine.Object.DestroyImmediate(output);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(JObject row in changes["images"])
        {
            var path=(string)row["path"];var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.spritePixelsPerUnit=(float)row["ppu"];importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.isReadable=true;
            var provider=Provider(importer,factories);var rects=provider.GetSpriteRects();
            if(rects.Length!=1)throw new Exception("Expected one sprite: "+path);
            rects[0].rect=new Rect(0,0,(int)row["spriteSize"][0],(int)row["spriteSize"][1]);
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2((float)row["pivot"][0],(float)row["pivot"][1]);
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.pixelsPerUnit!=(float)row["ppu"])throw new Exception("Import verification failed "+path);
        }
        File.WriteAllText(Selection+"all-weapons-import.json",weapons.ToString(),new UTF8Encoding(false));
        File.WriteAllText(Selection+"accessory-import.json",accessories.ToString(),new UTF8Encoding(false));
        File.WriteAllText(Review+"equipment-adopted.json",changes.ToString(),new UTF8Encoding(false));
        AssetDatabase.SaveAssets();
        return "Adopted10 native replacements, source pixels retained, GUIDs preserved. Update NativeWeaponArt poses from equipment-adopted.json before Play Mode.";
    }
}
