using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using RuinRunner;

public static class VerifySweepNative20261001
{
    public static object Main()
    {
        var families=new[]{WeaponFamily.Dagger,WeaponFamily.Longsword,WeaponFamily.Mace,WeaponFamily.Katana,WeaponFamily.Greatsword};
        var counts=new[]{5,6,6,4,6};var records=new JArray();
        for(int i=0;i<families.Length;i++)
        {
            var frames=PixelArtAtlas.GetMeleeSlashFrames(families[i],false);
            if(frames.Length!=counts[i])throw new Exception("Complete original source count "+families[i]);
            foreach(var sprite in frames)
            {
                var path=AssetDatabase.GetAssetPath(sprite);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer==null||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.spriteImportMode!=SpriteImportMode.Single)throw new Exception("Native import changed "+path);
                if(families[i]==WeaponFamily.Katana&&(sprite.rect.size!=new Vector2(57,42)||sprite.pivot!=new Vector2(28.5f,21)||sprite.pixelsPerUnit!=57))throw new Exception("Clipped katana cell remains active");
                records.Add(new JObject{{"family",families[i].ToString()},{"path",path},{"rect",new JArray(sprite.rect.width,sprite.rect.height)},{"pivot",new JArray(sprite.pivot.x,sprite.pivot.y)},{"ppu",sprite.pixelsPerUnit},{"point",true},{"mipmaps",false},{"compressed",false}});
            }
        }
        const string retired="Assets/RuinRunner/ArtReview/IncorrectKatanaSlices20261001";
        if(AssetDatabase.FindAssets("t:Sprite",new[]{retired}).Length!=6||Resources.Load<Sprite>(IndividualArt.Root+"External/NinjaAdventure/Slash/Slash03/04")!=null)throw new Exception("Rejected fragments remain active or were not preserved");
        File.WriteAllText("ArtDirection/Review-20261001/melee-fx-binding/actual-native-import-qa.json",new JObject{{"result","PASS"},{"activeSourceCells",27},{"retiredKatanaFragments",6},{"scope","Connected Editor actual native Sprite assets and resource lookup; not screen/motion/campaign evidence."},{"images",records}}.ToString(),new UTF8Encoding(false));
        return new {result="PASS",activeOriginalCells=27,retiredKatanaFragments=6,katanaCompleteCells=4,point=true,mipmaps=false,compressed=false};
    }
}
