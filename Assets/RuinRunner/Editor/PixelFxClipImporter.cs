#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>
    /// Tools/Art/pixelfx-to-unity.mjs 가 만든 CombatFX 클립을 Sprite 로 임포트한다.
    /// 같은 폴더의 _pixelfx.json 이 있는 PNG 에만 적용한다. 원본 픽셀 크기를 유지하며
    /// 필터·압축·밉맵은 PixelArtImportProcessor 의 정책(Point, 무압축, 무밉)과 같다.
    /// </summary>
    public sealed class PixelFxClipImporter : AssetPostprocessor
    {
        const string MetaFile = "_pixelfx.json";

        [System.Serializable]
        sealed class ClipMeta
        {
            public string clip;
            public float ppu = 32;
            public float pivotX = .5f, pivotY = .5f;
        }

        // 다른 후처리기보다 늦게 실행되어 Sprite 설정이 유지되게 한다.
        public override int GetPostprocessOrder() => 10;

        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Separated/CombatFX/") || !path.EndsWith(".png")) return;
            var json = Path.Combine(Path.GetDirectoryName(path) ?? "", MetaFile);
            if (!File.Exists(json)) return;

            ClipMeta meta;
            try { meta = JsonUtility.FromJson<ClipMeta>(File.ReadAllText(json)); }
            catch (System.Exception e) { Debug.LogError("[pixelfx] " + json + " 을 읽지 못했습니다: " + e.Message); return; }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = meta.ppu > 0 ? meta.ppu : 32;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Light is stored in RGB under zero alpha; dilating transparent pixels would erase it.
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 4096;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(meta.pivotX, meta.pivotY);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
        }

        [MenuItem("Prototype/Art/Reimport pixelfx CombatFX Clips")]
        static void ReimportAll()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(Path.Combine(Path.GetDirectoryName(path) ?? "", MetaFile))) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
            Debug.Log("[pixelfx] CombatFX 프레임 " + count + "개를 다시 임포트했습니다.");
        }
    }
}
#endif
