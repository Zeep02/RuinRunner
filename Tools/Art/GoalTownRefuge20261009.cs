using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json;

// Main owns execution in Edit Mode. Original PNG and metadata are read-only.
// Prepare writes lossless individual crops outside Assets; Import adopts only those crops.
public static class GoalTownRefuge20261009
{
    const string Original = "ArtDirection/ExternalCandidates/20260921/cainos-basic/original/Texture/TX Props.png";
    const string Review = "ArtDirection/Review-20261009/town-refuge/";
    const string Target = "Assets/RuinRunner/Resources/Art/Pixel/Separated/External/TownRefuge/";
    [Serializable] public sealed class Cell
    {
        public string id, sourceSprite, pngSha256;
        public int x, y, width, height;
        public float pivotX = .5f, pivotY;
        public Cell(string id, string sourceSprite, int x, int y, int width, int height, float px = .5f, float py = 0)
        { this.id=id;this.sourceSprite=sourceSprite;this.x=x;this.y=y;this.width=width;this.height=height;pivotX=px;pivotY=py; }
    }
    [Serializable] public sealed class Report
    {
        public string status, original=Original, originalSha256;
        public string sourceMetadata="ArtDirection/ExternalCandidates/20260921/cainos-basic/source-review/TX Props.meta.txt";
        public string license="Assets/StreamingAssets/ThirdPartyNotices/Cainos-SOURCE.txt";
        public string method="Exact original bottom-left pixel rectangles; no scale, recolor or resampling; 32 PPU, Point, native ground pivots.";
        public List<Cell> cells=new List<Cell>();
    }
    static Cell[] Cells() => new[] {
        new Cell("bench-s","TX Props Stone Bench S",292,452,56,41),
        new Cell("crate","TX Props Crate",160,448,32,46),
        new Cell("barrel","TX Props Barrel",162,323,28,36),
        new Cell("sign-e","TX Props Road Sign E",99,320,27,32,.44592682f,-.0006246567f)
    };
    static string Hash(string path)
    { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant(); }
    static void PreserveOrWrite(string path, byte[] bytes)
    {
        if(File.Exists(path))
        { if(!Convert.ToBase64String(File.ReadAllBytes(path)).Equals(Convert.ToBase64String(bytes),StringComparison.Ordinal))throw new Exception("Existing candidate differs; preserve it: "+path);return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,bytes);
    }
    public static string Prepare()
    {
        if(Application.isPlaying)throw new Exception("Use Edit Mode; no asset operation during play.");
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        var report=new Report{status="PREPARED only; not imported or visually accepted",originalSha256=Hash(Original)};
        try
        {
            if(!source.LoadImage(File.ReadAllBytes(Original))||source.width!=512||source.height!=512)throw new Exception("Unexpected Cainos original geometry.");
            var pixels=source.GetPixels32();
            foreach(var cell in Cells())
            {
                var crop=new Texture2D(cell.width,cell.height,TextureFormat.RGBA32,false);
                try
                {
                    var part=new Color32[cell.width*cell.height];
                    for(int y=0;y<cell.height;y++)Array.Copy(pixels,(cell.y+y)*source.width+cell.x,part,y*cell.width,cell.width);
                    crop.SetPixels32(part);crop.Apply();
                    var path=Review+"staged/"+cell.id+".png";PreserveOrWrite(path,crop.EncodeToPNG());cell.pngSha256=Hash(path);report.cells.Add(cell);
                }
                finally{UnityEngine.Object.DestroyImmediate(crop);}
            }
            if(report.originalSha256!=Hash(Original))throw new Exception("Original changed during extraction.");
            if(report.cells.Count!=4)throw new Exception("Expected exactly four prepared crops.");
            Directory.CreateDirectory(Review);File.WriteAllText(Review+"prepared.json",JsonConvert.SerializeObject(report,Formatting.Indented),new UTF8Encoding(false));
            return Review+"prepared.json";
        }
        finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    public static string Main() => Import();
    public static string Import()
    {
        Prepare();
        var report=JsonConvert.DeserializeObject<Report>(File.ReadAllText(Review+"prepared.json",Encoding.UTF8));
        if(report==null||report.cells==null||report.cells.Count!=4)throw new Exception("Expected exactly four manifest cells before import.");
        int imported=0;
        foreach(var cell in report.cells)
        {
            var path=Target+cell.id+".png";
            PreserveOrWrite(path,File.ReadAllBytes(Review+"staged/"+cell.id+".png"));
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new Exception("Missing PNG importer: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=true;importer.npotScale=TextureImporterNPOTScale.None;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
            if(provider==null)throw new Exception("Sprite provider unavailable: "+path);provider.InitSpriteEditorDataProvider();
            var edit=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(edit==null||!edit.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("EditPivot unsupported; aborted: "+path);
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One exact cell required: "+path);
            var pivot=new Vector2(cell.pivotX,cell.pivotY);rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=pivot;
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.pixelsPerUnit!=32||sprite.rect!=new Rect(0,0,cell.width,cell.height)||Vector2.Distance(sprite.pivot,Vector2.Scale(pivot,new Vector2(cell.width,cell.height)))>.001f||sprite.texture.filterMode!=FilterMode.Point||Hash(path)!=cell.pngSha256)
                throw new Exception("Native crop/pivot/pixel verification failed: "+path);
            imported++;
        }
        if(imported!=4)throw new Exception("Four actual Sprite imports required before PASS.");
        report.status="PASS exact-source import; actual town appearance and traversal still require Play inspection";
        File.WriteAllText(Review+"imported.json",JsonConvert.SerializeObject(report,Formatting.Indented),new UTF8Encoding(false));
        return report.status+"; "+report.cells.Count+" sprites; "+Review+"imported.json";
    }
}
