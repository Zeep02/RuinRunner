using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

// Main runs this after the hero importer completes. No original art or .meta edits.
public static class GoalPlinthFinishImport20261009
{
    const string Manifest="Tools/Art/Staging/PlinthFinish/prepared.json";
    const string Target="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/Cainos/StonePaving/";
    const string Review="ArtDirection/Review-20261009/plinth-finish/v2-stone-only/";
    const string Lease="GoalPlinthFinish20261009.ImportLease";
    static JObject report;static JArray cells;static string token,manifestHash;static int index;
    static string Hash(string path)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static string Main()
    {
        Require(!Application.isPlaying&&!EditorApplication.isCompiling,"Idle Edit Mode required.");
        Require(string.IsNullOrEmpty(SessionState.GetString(Lease,"")),"Another plinth importer is active.");
        manifestHash=Hash(Manifest);report=JObject.Parse(File.ReadAllText(Manifest,Encoding.UTF8));cells=(JArray)report["extraction"];
        Require(cells!=null&&cells.Count==32,"Exactly 32 native extraction records required.");
        foreach(JObject cell in cells)
        {
            Require(Hash((string)cell["source"])==(string)cell["sourceSha256"],"Original paving source changed.");
            Require(Hash((string)cell["staged"])==(string)cell["sha256"],"Staged paving bytes changed.");
            Require((int)cell["width"]==16&&(int)cell["height"]==16&&(int)cell["ppu"]==32,"Unexpected native geometry.");
        }
        index=0;token=Guid.NewGuid().ToString("N");SessionState.SetString(Lease,token);
        Directory.CreateDirectory(Target);Directory.CreateDirectory(Review);
        EditorApplication.update+=Tick;Status("RUNNING",null);return Review+"import-status.json";
    }
    public static string Cancel(){SessionState.EraseString(Lease);return "Cancellation requested; imported prefix remains verifiable.";}
    static void Tick()
    {
        try
        {
            if(SessionState.GetString(Lease,"")!=token){Finish("STOPPED",null);return;}
            Require(!Application.isPlaying&&!EditorApplication.isCompiling,"Editor entered Play/compile while importing.");
            var cell=(JObject)cells[index];Import(cell,index);index++;Status("RUNNING",null);
            if(index<32)return;
            Require(Hash(Manifest)==manifestHash,"Manifest changed during adoption.");
            foreach(JObject entry in cells)Require(Hash((string)entry["source"])==(string)entry["sourceSha256"],"Original source changed during adoption.");
            Finish("PASS exact native-stone extraction import; Unity appearance/collision review pending",null);
        }
        catch(Exception error){Finish("FAIL",error.ToString());Debug.LogException(error);}
    }
    static void Import(JObject cell,int cellIndex)
    {
        string source=(string)cell["staged"],path=Target+cellIndex.ToString("D2")+".png";
        if(File.Exists(path))Require(Hash(path)==(string)cell["sha256"],"Existing target differs; refusing overwrite: "+path);
        else File.Copy(source,path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;Require(importer!=null,"Missing PNG importer.");
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=true;importer.npotScale=TextureImporterNPOTScale.None;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
        Require(provider!=null,"Sprite Editor provider unavailable.");provider.InitSpriteEditorDataProvider();
        var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        Require(capability!=null&&capability.GetEditCapability().HasCapability(EEditCapability.EditPivot),"Provider cannot edit pivot; aborting.");
        var rects=provider.GetSpriteRects();Require(rects.Length==1,"Expected one standalone sprite.");
        rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2(.5f,.5f);provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Require(sprite!=null&&sprite.rect==new Rect(0,0,16,16)&&sprite.pivot==new Vector2(8,8)&&sprite.pixelsPerUnit==32&&sprite.texture.filterMode==FilterMode.Point,"Native sprite canvas/pivot/PPU failed.");
        Require(Hash(path)==(string)cell["sha256"],"Adopted PNG differs.");
        var original=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            Require(original.LoadImage(File.ReadAllBytes((string)cell["source"])),"Cannot read original paving.");
            // Sprite.texture may point at an Editor preview atlas. Verify the
            // actual imported Texture2D asset, not the backing atlas dimensions.
            var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(imported!=null,"Native imported texture asset is missing.");
            var before=original.GetPixels32();var after=imported.GetPixels32();
            var geometry=new JObject{{"cell",cellIndex},{"sourceWidth",original.width},{"sourceHeight",original.height},{"sourcePixels",before.Length},
                {"importWidth",imported.width},{"importHeight",imported.height},{"importPixels",after.Length},{"spriteTextureWidth",sprite.texture.width},
                {"spriteTextureHeight",sprite.texture.height},{"spriteTexturePath",AssetDatabase.GetAssetPath(sprite.texture)}};
            File.WriteAllText(Review+"last-import-texture.json",geometry.ToString(),new UTF8Encoding(false));
            Require(original.width==16&&original.height==16&&imported.width==16&&imported.height==16&&before.Length==256&&after.Length==256,"Native pixel count/geometry differs: "+geometry.ToString());
            int removed=0;var palette=(JArray)report["transparentSourceColors"];
            for(int p=0;p<256;p++)
            {
                var expected=before[p];bool grass=false;
                foreach(JArray color in palette)grass|=expected.r==(byte)(int)color[0]&&expected.g==(byte)(int)color[1]&&expected.b==(byte)(int)color[2]&&expected.a==(byte)(int)color[3];
                if(grass){removed++;Require(after[p].a==0,"Grass color was not made transparent.");}
                else Require(after[p].Equals(expected),"Original stone pixel changed.");
            }
            Require(removed==(int)cell["removedExactGrassPixels"],"Source palette-removal count differs.");
        }
        finally{UnityEngine.Object.DestroyImmediate(original);}
    }
    static void Status(string status,string error)
    {
        var result=new JObject{{"status",status},{"imported",index},{"expected",32},{"manifest",Manifest},{"manifestSha256",manifestHash},
            {"method","Retain native 16x16 stone pixels; exactly six original grass colors become transparent. Point/32PPU/custom center pivot; no resize/recolor/generated art."},
            {"license","Assets/StreamingAssets/ThirdPartyNotices/Cainos-SOURCE.txt"},{"runtimeAppearanceVerified",false},{"error",error}};
        File.WriteAllText(Review+"import-status.json",result.ToString(),new UTF8Encoding(false));
        if(status.StartsWith("PASS",StringComparison.Ordinal)){result["cells"]=cells.DeepClone();File.WriteAllText(Review+"imported.json",result.ToString(),new UTF8Encoding(false));}
    }
    static void Finish(string status,string error)
    {EditorApplication.update-=Tick;if(SessionState.GetString(Lease,"")==token)SessionState.EraseString(Lease);Status(status,error);}
}
