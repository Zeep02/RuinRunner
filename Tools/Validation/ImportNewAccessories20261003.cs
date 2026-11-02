using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class ImportNewAccessories20261003
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        string source="ArtDirection/ExternalCandidates/20260922/7soul-icons/original/";
        string target="Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Accessory/";
        string[] ids={"mana_thread","guard_bell","rush_spur","steady_locket","powder_satchel","last_light_pendant"};
        string[] names={"I_Fabric","Ac_Ring02","A_Shoes03","Ac_Necklace01","I_Ink","Ac_Necklace06"};
        foreach(string id in ids)if(File.Exists(target+id+".png"))throw new Exception("Preserve adopted icon "+id);
        var records=new JArray();var factories=new SpriteDataProviderFactories();factories.Init();
        for(int i=0;i<ids.Length;i++)
        {
            string from=source+names[i]+".png",to=target+ids[i]+".png";
            var native=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!native.LoadImage(File.ReadAllBytes(from)))throw new Exception(from);
            int width=native.width,height=native.height;
            var canvas=new Texture2D(width+4,height+4,TextureFormat.RGBA32,false);
            canvas.SetPixels32(new Color32[(width+4)*(height+4)]);canvas.SetPixels(2,2,width,height,native.GetPixels());canvas.Apply();
            File.WriteAllBytes(to,canvas.EncodeToPNG());UnityEngine.Object.DestroyImmediate(native);UnityEngine.Object.DestroyImmediate(canvas);
            AssetDatabase.ImportAsset(to,ImportAssetOptions.ForceSynchronousImport);
            var imp=(TextureImporter)AssetImporter.GetAtPath(to);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
            imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;
            imp.npotScale=TextureImporterNPOTScale.None;imp.wrapMode=TextureWrapMode.Clamp;imp.alphaIsTransparency=true;imp.isReadable=true;
            var setting=new TextureImporterSettings();imp.ReadTextureSettings(setting);setting.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(setting);imp.SaveAndReimport();
            var provider=factories.GetSpriteEditorDataProviderFromObject(imp);provider.InitSpriteEditorDataProvider();
            var cap=provider.GetDataProvider<ISpriteFrameEditCapability>();if(cap==null||!cap.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot capability "+to);
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("Single icon expected "+to);
            rects[0].alignment=SpriteAlignment.Center;rects[0].pivot=new Vector2(.5f,.5f);provider.SetSpriteRects(rects);provider.Apply();imp.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(to);if(sprite==null||sprite.pixelsPerUnit!=32||sprite.pivot!=new Vector2((width+4)*.5f,(height+4)*.5f))throw new Exception("Icon geometry "+to);
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(from))).Replace("-","").ToLowerInvariant();
            records.Add(new JObject{{"id",ids[i]},{"source",from},{"sourceSha256",hash},{"path",to},{"nativeSize",new JArray(width,height)},{"guid",AssetDatabase.AssetPathToGUID(to)},{"modification","2 native transparent pixels of padding only; no resizing, repainting or generated icon."}});
        }
        AssetDatabase.SaveAssets();File.WriteAllText("ArtDirection/Review-20261003/item-expansion/icon-import.json",new JObject{{"result","PASS"},{"icons",records}}.ToString(),new UTF8Encoding(false));
        return "Six distinct existing 7Soul icons imported, Point/FullRect/RGBA/no mip/readable/32PPU, original pixels unchanged.";
    }
}
