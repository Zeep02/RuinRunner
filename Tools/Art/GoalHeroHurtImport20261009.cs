using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

// Individual PNG adoption only. The existing NativeHeroAtlasPrebuild remains
// responsible for built-in catalog/atlas generation after Runtime integration.
public static class GoalHeroHurtImport20261009
{
    const string Source="ArtDirection/Generated/SscaryMotion-20261009/hurt-native-v5-full/";
    const string Target="Assets/RuinRunner/ArtSources/Hero/WeaponHurtV1/";
    const string Review="ArtDirection/Review-20261009/hero-hurt-native/v5/";
    const string Lease="RuinRunner.GoalHeroHurtImport20261009";
    static JObject manifest;static JObject[] unique;static Dictionary<string,string> originals;
    static string manifestHash,lease;static int next,phase;static bool running;static double started;
    static readonly string[] Phases={"copy/import","texture settings","Sprite Editor pivot","visible-pixel verification"};
    static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Require(bool yes,string message){if(!yes)throw new InvalidOperationException(message);}
    static string VisibleTopHash(Color32[] bottom,int width,int height)
    {
        var bytes=new byte[width*height*4];int index=0;
        for(int y=height-1;y>=0;y--)for(int x=0;x<width;x++)
        {var c=bottom[y*width+x];bytes[index++]=c.a==0?(byte)0:c.r;bytes[index++]=c.a==0?(byte)0:c.g;bytes[index++]=c.a==0?(byte)0:c.b;bytes[index++]=c.a;}
        return Hash(bytes);
    }
    public static string Main()
    {
        Require(!Application.isPlaying&&!EditorApplication.isCompiling,"Use idle Edit Mode.");
        Require(!running,"Hurt import is already running.");
        Require(string.IsNullOrEmpty(SessionState.GetString(Lease,"")),"Another hurt import owns the Editor; do not start a second one.");
        Require(!File.Exists(Review+"imported.json"),"Preserve completed import; inspect imported.json.");
        string path=Source+"manifest-reviewed.json";manifestHash=Hash(File.ReadAllBytes(path));manifest=JObject.Parse(File.ReadAllText(path,Encoding.UTF8));
        Require(manifest.Value<bool>("fullGaitVariants")&&manifest.Value<int>("profiles")==7&&((JArray)manifest["frames"]).Count==7*8*16*9,"Full 7-profile/8-direction/16-gait/9-key manifest required.");
        int count=manifest.Value<int>("uniqueSprites");Require(count>5021&&count<=8064,"Unexpected full unique count.");
        unique=new JObject[count];originals=new Dictionary<string,string>();var slots=new HashSet<string>();
        foreach(JObject row in manifest["frames"])
        {
            int index=row.Value<int>("runtimeIndex");Require(index>=0&&index<count,"Frame index outside native catalog.");
            string source=row.Value<string>("path");Require(source==Source+index.ToString("D5")+".png","Unexpected candidate path.");
            string slot=row["profile"]+"/"+row["direction"]+"/"+row["variant"]+"/"+row["key"];Require(slots.Add(slot),"Duplicate authored slot.");
            if(unique[index]!=null)Require(unique[index].Value<string>("visiblePixelSha256")==row.Value<string>("visiblePixelSha256"),"Inconsistent deduplication.");
            unique[index]=row;originals[row.Value<string>("source")]=row.Value<string>("sourcePngSha256");
        }
        for(int i=0;i<count;i++)Require(unique[i]!=null,"Missing unique PNG index "+i);
        VerifyOriginals();Directory.CreateDirectory(Review);Directory.CreateDirectory(Target);next=0;phase=0;started=EditorApplication.timeSinceStartup;running=true;
        lease=Guid.NewGuid().ToString("N");SessionState.SetString(Lease,lease);
        SaveStatus("RUNNING",null);EditorApplication.update+=Tick;
        return "Scheduled "+count+" native PNG imports; poll "+Review+"import-status.json. Keep Edit Mode and do not reload until complete.";
    }
    static void VerifyOriginals()
    {foreach(var source in originals)Require(Hash(File.ReadAllBytes(source.Key))==source.Value,"Approved original changed: "+source.Key);}
    static void Tick()
    {
        if(!running)return;
        if(SessionState.GetString(Lease,"")!=lease){running=false;EditorApplication.update-=Tick;SaveStatus("STOPPED; completed PNG prefix preserved",null);return;}
        try
        {
            Require(!Application.isPlaying&&!EditorApplication.isCompiling,"Editor state changed during adoption.");
            // Queue a small group per phase. Reimporting each PNG three times in
            // isolation repeats project refresh work thousands of times.
            // EndAssetEditing always runs, including failures; source files and
            // Unity-generated metadata remain intact for a checked restart.
            int end=Math.Min(next+(phase==3?128:64),unique.Length);
            if(phase<3)AssetDatabase.StartAssetEditing();
            try
            {
                for(;next<end;next++)
                {
                    if(phase==0)CopyAndImport(next);
                    else if(phase==1)ConfigureTexture(next);
                    else if(phase==2)ConfigurePivot(next);
                    else VerifyOne(next);
                }
            }
            finally{if(phase<3)AssetDatabase.StopAssetEditing();}
            if(next==unique.Length)
            {
                if(phase<3){phase++;next=0;SaveStatus("RUNNING",null);return;}
                VerifyOriginals();Require(Hash(File.ReadAllBytes(Source+"manifest-reviewed.json"))==manifestHash,"Candidate manifest changed during import.");
                AssetDatabase.SaveAssets();running=false;EditorApplication.update-=Tick;SessionState.SetString(Lease,"");SaveStatus("PASS PNG imports; Runtime/catalog/playback still require verification",null);
                File.Copy(Review+"import-status.json",Review+"imported.json");
            }
            else SaveStatus("RUNNING",null);
        }
        catch(Exception error)
        {running=false;EditorApplication.update-=Tick;SessionState.SetString(Lease,"");SaveStatus("FAILED; preserve imported prefix and inspect error",error.ToString());Debug.LogException(error);}
    }
    public static string Cancel()
    {SessionState.SetString(Lease,"");return "Stop requested; wait one Editor update and inspect "+Review+"import-status.json";}
    static void SaveStatus(string status,string error)
    {
        var report=new JObject{{"status",status},{"manifest",Source+"manifest-reviewed.json"},{"manifestSha256",manifestHash},{"target",Target},{"phase",Phases[phase]},{"phaseIndex",phase},{"phaseProcessed",next},{"imported",phase==3?next:0},{"expected",unique==null?0:unique.Length},{"elapsedSeconds",EditorApplication.timeSinceStartup-started},{"originalsPreserved",true},{"error",error}};
        File.WriteAllText(Review+"import-status.json",report.ToString(),new UTF8Encoding(false));
    }
    static string TargetPath(int index)=>Target+index.ToString("D5")+".png";
    static void CopyAndImport(int index)
    {
        string staged=Source+index.ToString("D5")+".png",target=Target+index.ToString("D5")+".png";
        byte[] bytes=File.ReadAllBytes(staged);string sha=Hash(bytes);
        if(File.Exists(target))Require(Hash(File.ReadAllBytes(target))==sha,"Existing target differs; never overwrite: "+target);
        else File.WriteAllBytes(target,bytes);
        AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceUpdate);
    }
    static void ConfigureTexture(int index)
    {
        string target=TargetPath(index);
        var importer=AssetImporter.GetAtPath(target) as TextureImporter;Require(importer!=null,"Missing native PNG importer.");
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(target);
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        bool configured=sprite!=null&&sprite.rect==new Rect(0,0,48,64)&&sprite.pivot==new Vector2(24,22)&&sprite.pixelsPerUnit==32&&
            importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Single&&importer.filterMode==FilterMode.Point&&
            importer.textureCompression==TextureImporterCompression.Uncompressed&&!importer.mipmapEnabled&&importer.isReadable&&
            importer.npotScale==TextureImporterNPOTScale.None&&settings.spriteMeshType==SpriteMeshType.FullRect;
        if(!configured)
        {
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
            importer.isReadable=true;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=128;
            importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
    }
    static void ConfigurePivot(int index)
    {
        string target=TargetPath(index);var importer=AssetImporter.GetAtPath(target) as TextureImporter;
        Require(importer!=null,"Missing native PNG importer before pivot.");
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(target);
        if(sprite!=null&&sprite.pivot==new Vector2(24,22))return;
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
            Require(provider!=null,"Sprite Editor provider missing.");provider.InitSpriteEditorDataProvider();
            var edit=provider.GetDataProvider<ISpriteFrameEditCapability>();Require(edit!=null&&edit.GetEditCapability().HasCapability(EEditCapability.EditPivot),"Sprite provider cannot edit pivot; aborted.");
            var rects=provider.GetSpriteRects();Require(rects.Length==1,"Expected one native sprite.");
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2(.5f,22f/64f);provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
    }
    static void VerifyOne(int index)
    {
        string target=TargetPath(index);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(target);
        string sha=Hash(File.ReadAllBytes(Source+index.ToString("D5")+".png"));
        Require(sprite!=null&&sprite.rect==new Rect(0,0,48,64)&&sprite.pivot==new Vector2(24,22)&&sprite.pixelsPerUnit==32,"Imported native geometry mismatch.");
        Require(sprite.texture.width==48&&sprite.texture.height==64&&sprite.texture.filterMode==FilterMode.Point,"Imported native texture mismatch.");
        Require(Hash(File.ReadAllBytes(target))==sha,"PNG changed during import.");
        Require(VisibleTopHash(sprite.texture.GetPixels32(),48,64)==unique[index].Value<string>("visiblePixelSha256"),"Imported visible pixels changed: "+target);
    }
}
