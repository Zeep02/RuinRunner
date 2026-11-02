using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json.Linq;
public static class AlignChargedCut20261001
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        if(AssetDatabase.IsValidFolder("Assets/RuinRunner/ArtReview/RejectedChargedWedge20261001"))
            throw new Exception("This charged wedge was rejected by the user. Retired source/pivots are preserved; do not restore or adjust it in Resources.");
        var factories=new SpriteDataProviderFactories();factories.Init();
        for(int i=0;i<7;i++)
        {
            string path="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/ChargedCut/"+i.ToString("D2")+".png";
            var importer=AssetImporter.GetAtPath(path);var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot capability "+path);
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One native frame required");
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2(80/128f,76/128f);provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            if(AssetDatabase.LoadAssetAtPath<Sprite>(path).pivot!=new Vector2(80,76))throw new Exception("Pivot apply failed "+path);
        }
        string record="ArtDirection/Review-20261001/weapon-identity-more/native-import.json";var data=JObject.Parse(File.ReadAllText(record,Encoding.UTF8));
        foreach(var row in data["images"])if((string)row["clip"]=="ChargedCut")row["pivot"]=new JArray(80,76);
        data["contactAlignment"]="Charged source's bright blade junction anchored to actual committed contact center, rather than the lower transparent canvas. PNG bytes unchanged.";
        File.WriteAllText(record,data.ToString(),new UTF8Encoding(false));AssetDatabase.SaveAssets();return "Seven exact original charge source pivots aligned to bright junction; no image/color/scale edits.";
    }
}
