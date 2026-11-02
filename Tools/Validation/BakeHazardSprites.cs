using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

// Deterministic geometric UI authoring, baked once into PNG/Sprite assets.
// The contour is exact: the same normalized square/circle used by Contains.
public static class BakeHazardSprites
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before importer edits");
        const string root="Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Hazards";
        Directory.CreateDirectory(root);
        var factory=new SpriteDataProviderFactories();factory.Init();
        foreach(var round in new[]{false,true})
        {
            const int n=128;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);
            var colors=new Color32[n*n];
            for(var y=0;y<n;y++)for(var x=0;x<n;x++)
            {
                var dx=x+.5f-64;var dy=y+.5f-64;
                var inset=round?64-Mathf.Sqrt(dx*dx+dy*dy):Mathf.Min(Mathf.Min(x,y),Mathf.Min(127-x,127-y));
                var color=new Color32(255,255,255,18);
                if(inset<0)color=new Color32(0,0,0,0);
                else if(inset<1)color=new Color32(15,21,34,245);
                else if(inset<3)color=new Color32(255,255,255,255);
                else if(inset<4)color=new Color32(15,21,34,245);
                // Exact corners/cardinal markers remain inside the damage contour.
                else if(inset<8 && (round ? Mathf.Abs(dx)<1 || Mathf.Abs(dy)<1 : x<8 || x>119 || y<8 || y>119))
                    color=new Color32(255,255,255,220);
                colors[y*n+x]=color;
            }
            texture.SetPixels32(colors);texture.Apply();
            var path=root+"/"+(round?"Circle":"Rectangle")+".png";
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=128;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=128;
            var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(textureSettings);importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var edit=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(edit==null || !edit.GetEditCapability().HasCapability(EEditCapability.EditBorder|EEditCapability.EditPivot))throw new Exception("Missing frame edit capability");
            var rects=data.GetSpriteRects();foreach(var rect in rects)
            {rect.alignment=SpriteAlignment.Custom;rect.pivot=new Vector2(.5f,.5f);rect.border=round?Vector4.zero:Vector4.one*8;}
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();
        }
        return "Baked exact square/circle danger contours, transparent fill, rectangle 9-slice border8; individual128px PNG/Sprite.";
    }
}
