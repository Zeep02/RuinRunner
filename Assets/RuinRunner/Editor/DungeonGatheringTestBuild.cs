#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public static class DungeonGatheringTestBuild
    {
        private const string Root = "Assets/RuinRunner/Resources/Art/Pixel/Separated/DungeonGathering/";
        private const string ScenePath = "Assets/RuinRunner/Scenes/DungeonGatheringTest.unity";
        private static int exported;

        [MenuItem("Prototype/Dungeon Gathering/Bake Individual Tiles")]
        public static void Bake()
        {
            exported = 0;
            var detail = Source("Set 1.1.png");
            Tile(detail, "Floor/plain", 7, 6);
            Tile(detail, "Floor/checker", 4, 6);
            Tile(detail, "Floor/cracks", 9, 6);
            Tile(detail, "Floor/bricks", 8, 6);
            Tile(detail, "Floor/engraved", 7, 5);
            Tile(detail, "Props/rubble", 10, 6);
            Tile(detail, "Props/stones", 11, 6);
            Tile(detail, "Props/urn", 1, 5);
            Tile(detail, "Wall/pillar_top", 3, 0);
            Tile(detail, "Wall/pillar_middle", 3, 1);
            for (var i = 0; i < 3; i++)
            {
                Tile(detail, "Wall/face_" + i, 3 + i, 2);
                Tile(detail, "Wall/base_" + i, 3 + i, 3);
            }
            for (var y = 0; y < 3; y++) Tile(detail, "Stairs/" + y, 16, y);
            UnityEngine.Object.DestroyImmediate(detail);

            BakeMasonry();
            var fence = Source("Set 3.5.png");
            Tile(fence, "Masonry/gate", 0, 2);
            UnityEngine.Object.DestroyImmediate(fence);

            var floor = Source("Set 1.2.png");
            var names = new[] { "tl", "t", "tr", "l", "c", "r", "bl", "b", "br" };
            var xs = new[] { 4, 5, 7 }; var ys = new[] { 0, 1, 3 };
            for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++) Tile(floor, "Trim/" + names[y * 3 + x], xs[x], ys[y]);
            UnityEngine.Object.DestroyImmediate(floor);

            var pool = Source("Set 4.5.png");
            for (var x = 0; x < 3; x++)
            {
                Tile(pool, "Pool/" + names[x], x, 0);
                Tile(pool, "Pool/" + names[3 + x], x, 1);
                Tile(pool, "Pool/" + names[6 + x], x, 0);
            }
            UnityEngine.Object.DestroyImmediate(pool);
            Sequence("Torch Yellow.png", "Torch", 8, 0);
            Sequence("Water.png", "Ripple", 8, 0);
            Sequence("Vase Shine Anim.png", "Urn", 16, 0);
            AssetDatabase.SaveAssets();
            Debug.Log("[Dungeon Gathering] Exported " + exported + " individual PNGs. Original palette, alpha and 16px grid preserved.");
        }

        [MenuItem("Prototype/Dungeon Gathering/Bake Masonry Tiles")]
        public static void BakeMasonry()
        {
            var structure = Source("Set 1.0.png");
            Tile(structure, "Masonry/north", 2, 0);
            Tile(structure, "Masonry/north_left", 1, 0);
            Tile(structure, "Masonry/north_right", 3, 0);
            Tile(structure, "Masonry/west", 1, 1);
            Tile(structure, "Masonry/east", 3, 1);
            Tile(structure, "Masonry/south", 2, 2);
            Tile(structure, "Masonry/south_left", 1, 2);
            Tile(structure, "Masonry/south_right", 3, 2);
            Tile(structure, "Masonry/inner_north_left", 5, 1);
            Tile(structure, "Masonry/inner_north_right", 6, 1);
            Tile(structure, "Masonry/inner_south_left", 5, 2);
            Tile(structure, "Masonry/inner_south_right", 6, 2);
            UnityEngine.Object.DestroyImmediate(structure);
        }

        private static Texture2D Source(string name)
        {
            var path = Path.Combine(Application.dataPath, "../Dungeon Gathering Free Version", name);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot decode " + path);
            return texture;
        }

        private static void Sequence(string file, string id, int count, int row)
        {
            var source = Source(file);
            for (var i = 0; i < count; i++) Tile(source, id + "/" + i.ToString("D2"), i, row);
            UnityEngine.Object.DestroyImmediate(source);
        }

        // Source coordinates are top-left 16px cells. Runtime never receives sheet rectangles.
        private static void Tile(Texture2D source, string id, int x, int y)
        {
            var path = Root + id + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tile = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = source.GetPixels(x * 16, source.height - (y + 1) * 16, 16, 16);
            if (id.StartsWith("Masonry/", StringComparison.Ordinal) && id != "Masonry/gate")
            {
                // The source's flat void is not masonry. Leave it transparent so the
                // same camera background continues to the wall in every stage tint.
                // Preserve all brick/mortar shades; only this exact palette entry is void.
                for (var i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.r == 24 && pixel.g == 20 && pixel.b == 37 && pixel.a == 255)
                        pixels[i] = Color.clear;
                }
            }
            tile.SetPixels(pixels);
            tile.Apply();
            File.WriteAllBytes(path, tile.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tile);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            exported++;
        }

        [MenuItem("Prototype/Dungeon Gathering/Open Test Map")]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Dungeon Gathering - standalone map test").AddComponent<DungeonGatheringTest>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [MenuItem("Prototype/Dungeon Gathering/Build Separate Windows Test")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Bake();
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var product = PlayerSettings.productName;
            var width = PlayerSettings.defaultScreenWidth; var height = PlayerSettings.defaultScreenHeight;
            var mode = PlayerSettings.fullScreenMode;
            try
            {
                CreateScene();
                // Different product isolates persistent settings; no save-slot service is created.
                PlayerSettings.productName = "Dungeon Gathering Test";
                PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                const string destination = "Builds/DungeonGatheringTest/DungeonGatheringTest.exe";
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = destination,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.CleanBuildCache
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Dungeon Gathering test build failed: " + report.summary.result);
                Debug.Log("[Dungeon Gathering] BUILD PASS " + Path.GetFullPath(destination));
            }
            finally
            {
                PlayerSettings.productName = product;
                PlayerSettings.defaultScreenWidth = width; PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.fullScreenMode = mode;
                // Batch mode can begin with an unnamed/unsaved scene; it has no restorable setup.
                if (!Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }
}
#endif
