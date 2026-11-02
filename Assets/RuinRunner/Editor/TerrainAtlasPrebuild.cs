// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD] Built-in terrain data, no runtime download or Addressables.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public sealed class TerrainAtlasPrebuild : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-100;
        public void OnPreprocessBuild(BuildReport report)=>Generate();
        public static string Generate()
        {
            EditorSettings.spritePackerMode=SpritePackerMode.SpriteAtlasV2;
            if(EditorSettings.spritePackerMode!=SpritePackerMode.SpriteAtlasV2)throw new Exception("SpriteAtlas V2 packing did not enable");
            const string output="Assets/RuinRunner/ArtAtlases";Directory.CreateDirectory(output);
            const string external="Assets/RuinRunner/Resources/Art/Pixel/Separated/External";
            var sources=AssetDatabase.FindAssets("t:Texture2D",new[]{external})
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            if(sources.Length==0)throw new Exception("External art source list is empty");
            foreach(var source in sources)
                if(!source.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>().Count()!=1)
                    throw new Exception("Expected one complete Sprite per external PNG: "+source);
            // Resources has no runtime path enumeration. Retain an explicit list
            // so a Player smoke test can compare each packed frame to its own PNG.
            const string manifest="Assets/RuinRunner/Resources/Art/ExternalSourcePaths.txt";
            const string resourceRoot="Assets/RuinRunner/Resources/";
            File.WriteAllLines(manifest,sources.Select(path=>path.Substring(resourceRoot.Length,path.Length-resourceRoot.Length-4)),new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(manifest,ImportAssetOptions.ForceSynchronousImport);
            foreach(var theme in new[]{"Outer","Flooded","Core","External"})
            {
                var folder="Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Terrain/"+theme;
                var path=output+"/Terrain"+theme+".spriteatlasv2";
                // Recreate the packable list deterministically; keep the asset GUID.
                var atlas=new SpriteAtlasAsset();
                if(theme=="External")atlas.Add(new UnityEngine.Object[]{AssetDatabase.LoadAssetAtPath<DefaultAsset>(external)});
                else atlas.Add(new UnityEngine.Object[]{AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder+"/Ribbon")});
                SpriteAtlasAsset.Save(atlas,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                importer.includeInBuild=true;
                var packing=importer.packingSettings;packing.padding=4;packing.enableRotation=false;
                packing.enableTightPacking=false;packing.enableAlphaDilation=true;importer.packingSettings=packing;
                var texture=importer.textureSettings;texture.filterMode=FilterMode.Point;texture.generateMipMaps=false;
                texture.readable=true;importer.textureSettings=texture;
                importer.SetPlatformSettings(new TextureImporterPlatformSettings{name="DefaultTexturePlatform",maxTextureSize=2048,
                    format=TextureImporterFormat.RGBA32,textureCompression=TextureImporterCompression.Uncompressed});
                importer.SetPlatformSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=2048,
                    format=TextureImporterFormat.RGBA32,textureCompression=TextureImporterCompression.Uncompressed});
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            return "4 built-in terrain/external art atlases; packer="+EditorSettings.spritePackerMode+"; Point, RGBA32, 4px padding, no rotation/tight packing.";
        }
    }
}
