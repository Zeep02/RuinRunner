using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

// Run through the connected Editor only, out of Play. Original resources and
// their GUIDs stay in place. This standalone importer creates one new version.
public static class ImportSealCore20261009
{
    const string ReviewRoot="ArtDirection/Generated/SealCore-20261009/";
    const string SourceRoot=ReviewRoot+"frames-v4/";
    const string TargetRoot="Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/SealCoreV4/";
    static readonly string[] Clips={"idle","move","cast","sweep","charge","hurt","death","entrance"};
    static string Hash(byte[] bytes)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Folder(string path)
    {
        path=path.TrimEnd('/');if(AssetDatabase.IsValidFolder(path))return;
        var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static void ValidatePixels(string path)
    {
        var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            if(!image.LoadImage(File.ReadAllBytes(path))||image.width!=80||image.height!=80)throw new Exception("Expected an individual 80px canvas: "+path);
            var pixels=image.GetPixels32();var opaque=pixels.Where(p=>p.a>0).ToArray();
            if(opaque.Length==0||opaque.Any(p=>p.a!=255)||opaque.Distinct().Count()>14)throw new Exception("Invalid palette/alpha: "+path);
            if(pixels.Any(p=>p.a==0&&(p.r!=0||p.g!=0||p.b!=0)))throw new Exception("Invisible RGB must be zero: "+path);
            if(Enumerable.Range(0,80).Any(i=>pixels[i].a>0||pixels[79*80+i].a>0||pixels[i*80].a>0||pixels[i*80+79].a>0))throw new Exception("Clipped canvas edge: "+path);
        }
        finally{UnityEngine.Object.DestroyImmediate(image);}
    }
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before SealCore import");
        var expected=JObject.Parse(File.ReadAllText(ReviewRoot+"export-v4.json",Encoding.UTF8));
        var preflight=new System.Collections.Generic.List<JObject>();
        // Preflight all frames before the first AssetDatabase mutation.
        foreach(string clip in Clips)
        {
            int count=clip=="idle"?4:8;
            if(Directory.GetFiles(SourceRoot+clip,"*.png").Length!=count)throw new Exception("Unexpected source frame count: "+clip);
            for(int frame=0;frame<count;frame++)
            {
                string name=clip+"/"+frame.ToString("D2")+".png",source=SourceRoot+name,target=TargetRoot+name;
                string hash=Hash(File.ReadAllBytes(source));
                var record=expected["records"].Single(r=>(string)r["clip"]==clip&&(int)r["frame"]==frame);
                if(hash!=(string)record["sha256"])throw new Exception("Reviewed PNG hash changed: "+source);
                ValidatePixels(source);
                if(File.Exists(target)&&Hash(File.ReadAllBytes(target))!=hash)throw new Exception("Preserve existing different pixels: "+target);
                preflight.Add(new JObject{{"clip",clip},{"frame",frame},{"source",source},{"path",target},{"sha256",hash}});
            }
        }
        var factories=new SpriteDataProviderFactories();factories.Init();
        foreach(var record in preflight)
        {
            string source=(string)record["source"],target=(string)record["path"];
            Folder(Path.GetDirectoryName(target).Replace('\\','/'));
            if(!File.Exists(target))File.Copy(source,target);
            AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(target) as TextureImporter;
            if(importer==null)throw new Exception("Expected texture importer: "+target);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
            if(provider==null)throw new Exception("Sprite data provider absent; import aborted: "+target);
            provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                throw new Exception("EditPivot capability absent; import aborted: "+target);
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected one full sprite: "+target);
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2(.5f,.1f);
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(target);
            if(sprite==null||sprite.pixelsPerUnit!=32||sprite.rect.size!=new Vector2(80,80)||sprite.pivot!=new Vector2(40,8))
                throw new Exception("Imported sprite geometry differs: "+target);
            if(Hash(File.ReadAllBytes(target))!=(string)record["sha256"])throw new Exception("PNG changed during import: "+target);
            record["guid"]=AssetDatabase.AssetPathToGUID(target);record["ppu"]=32;record["pivot"]=new JArray(40,8);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(ReviewRoot+"unity-import-v4.json",new JObject{
            {"method","60 reviewed individual generated poses; no previous PNG/meta/GUID replaced; provider capability checked; source file hash and sprite geometry verified"},
            {"sourceManifest","export-v4.json"},{"images",new JArray(preflight)}}.ToString(),new UTF8Encoding(false));
        return new {frames=preflight.Count,uniqueFileHashes=preflight.Select(r=>(string)r["sha256"]).Distinct().Count(),root=TargetRoot,
            sourcePNGsUnchanged=true,ppu=32,pivot=new[]{40,8},runtimeBindingNotInstalled=true};
    }
}
