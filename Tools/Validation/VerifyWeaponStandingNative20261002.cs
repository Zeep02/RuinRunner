using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using RuinRunner;
public static class VerifyWeaponStandingNative20261002
{
 public static object Main()
 {
  string root=NativeHeroFrameCatalog.SourceRoot+"WeaponStandingV4";
  for(int i=0;i<WeaponStandingFrames.UniqueSpriteCount;i++)
  {
   string name=i.ToString("D5")+".png",path=root+"/"+name;
   if(!File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes("ArtDirection/Generated/SscaryMotion-20261001/weapon-standing-v4/"+name)))throw new Exception("Source changed "+path);
   var texture=(TextureImporter)AssetImporter.GetAtPath(path);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
   if(sprite==null||sprite.rect.size!=new Vector2(48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32||texture.filterMode!=FilterMode.Point||texture.mipmapEnabled||texture.textureCompression!=TextureImporterCompression.Uncompressed)throw new Exception("Native standing import "+path);
  }
  var report=new{result="PASS",root,sprites=WeaponStandingFrames.UniqueSpriteCount,sampleSlots=6912,exactPngBytes=true,geometry="48x64 / pivot24,22 / 32PPU / Point / no mipmaps / uncompressed",scope="Actual Editor imported sprites; not gameplay acceptance."};
  File.WriteAllText("ArtDirection/Review-20261001/weapon-carry/native-standing-v4-qa.json",JsonUtility.ToJson(new Proof{sprites=WeaponStandingFrames.UniqueSpriteCount}),System.Text.Encoding.UTF8);
  return report;
 }
 [Serializable]class Proof{public string result="PASS",root="WeaponStandingV4",scope="Every actual imported sprite and exact PNG source verified";public int sprites;}
}
