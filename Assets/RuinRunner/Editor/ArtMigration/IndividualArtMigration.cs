using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Old = RuinRunner.EditorTools.LegacyArt;

namespace RuinRunner.EditorTools
{
    /// <summary>One-time, lossless baking of the existing runtime results, including pivots and borders.</summary>
    public static class IndividualArtMigration
    {
        private const string Root = "Assets/RuinRunner/Resources/" + IndividualArt.Root;
        private const string ManifestPath = Root + "manifest.json";
        [Serializable] public sealed class Entry
        {
            public string id;
            public int width, height;
            public Vector2 pivot;
            public float ppu;
            public Vector4 border;
        }
        [Serializable] public sealed class Manifest { public int version = 1; public List<Entry> entries = new List<Entry>(); }
        private static Manifest manifest;

        [MenuItem("Prototype/Art/Archive Source Sheets Outside Runtime Resources")]
        public static void ArchiveSheets()
        {
            Validate();
            var candidates = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/RuinRunner/Resources/Art/Pixel" });
            var moved = 0;
            foreach (var guid in candidates)
            {
                var source = AssetDatabase.GUIDToAssetPath(guid);
                if (source.Contains("/Separated/") || source.Contains("/Items/")) continue;
                var name = Path.GetFileNameWithoutExtension(source);
                if (!name.Contains("atlas") && name != "live_grass_cliff_v2" &&
                    name != "player_topdown_locomotion_8dir_v2" && name != "player_topdown_attack_body_8dir_v1") continue;
                var relative = source.Substring("Assets/RuinRunner/Resources/".Length);
                var target = "Assets/RuinRunner/ArtSource/Sheets/" + relative;
                var absoluteTarget = Path.GetFullPath(target);
                if (!absoluteTarget.StartsWith(Path.GetFullPath("Assets/RuinRunner/ArtSource/Sheets") + Path.DirectorySeparatorChar))
                    throw new InvalidOperationException("Invalid archive destination: " + target);
                if (File.Exists(target)) throw new InvalidOperationException("Archive already exists: " + target);
                var backup = "Backups/2026-09-10-individual-art/" + source;
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) { File.Copy(source, backup); File.Copy(source + ".meta", backup + ".meta"); }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var error = AssetDatabase.MoveAsset(source, target);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                moved++;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Individual Art] Archived " + moved + " source sheets with GUIDs preserved; no source art deleted.");
        }

        [MenuItem("Prototype/Art/Bake Missing Individual Art and Validate")]
        public static void ExportMissing()
        {
            if (File.Exists(ManifestPath)) { Validate(); return; }
            PixelArtImportProcessor.EnsurePixelImports();
            manifest = new Manifest();
            /* Retired generated hero exports are preserved in versioned backups; never recreate them in Resources.
            ExportPlayer("move", "Art/Pixel/TopDown/player_topdown_locomotion_8dir_v2");
            ExportPlayer("attack", "Art/Pixel/TopDown/player_topdown_attack_body_8dir_v1");
            var library = Old.PixelArtAtlas.CreateDefaultPlayerLibrary();
            var states = new[] { "idle", "run", "brake", "jumpRise", "fall", "land", "attack", "heavyAttack", "guard", "dash", "hurt" };
            var rows = new[] { library.idle, library.run, library.brake, library.jumpRise, library.fall, library.land, library.attack, library.heavyAttack, library.guard, library.dash, library.hurt };
            for (var i = 0; i < rows.Length; i++) Frames("Player/Legacy/" + states[i], rows[i]);
            */
            for (var row = 0; row < 12; row++) Frames("Enemies/" + ((EnemyType)(row / 3)) + "/" + new[] { "idle", "run", "attack" }[row % 3], Old.GeneratedActorFrames.GetEnemy(row));
            for (var row = 0; row < 7; row++) Frames("Boss/" + row.ToString("D2"), Old.GeneratedActorFrames.GetBoss(row));
            foreach (var family in new[] { WeaponFamily.Dagger, WeaponFamily.Longsword, WeaponFamily.Mace, WeaponFamily.Katana, WeaponFamily.Greatsword })
                Frames("VFX/Slash/" + family, Old.PixelArtAtlas.GetMeleeSlashFrames(family, false));
            Frames("VFX/Slash/Critical", Old.PixelArtAtlas.GetMeleeSlashFrames(WeaponFamily.Longsword, true));
            for (var i = 0; i < 16; i++) Write("Terrain/Ruins/" + i.ToString("D2"), Old.PixelArtAtlas.GetTerrainSprite(i));
            Write("Terrain/Grass", Old.EnvironmentArt.Ground); Write("Terrain/Cliff", Old.EnvironmentArt.Cliff);
            for (var i = 0; i < 12; i++) Write("Props/" + i.ToString("D2"), Old.EnvironmentArt.Prop(i));
            foreach (WorldInteractionArt art in Enum.GetValues(typeof(WorldInteractionArt))) Write("World/" + art, Old.PixelWorldArt.GetSprite(art));
            foreach (PixelUiElement element in Enum.GetValues(typeof(PixelUiElement))) Write("UI/" + element, Old.PixelUiSkin.GetInterfaceElement(element));
            foreach (PixelUiButtonState state in Enum.GetValues(typeof(PixelUiButtonState))) Write("UI/ButtonStates/" + state, Old.PixelUiSkin.GetButtonState(state));
            Write("UI/Logo", Old.PixelUiSkin.MenuLogo);
            foreach (PixelHudBarType bar in Enum.GetValues(typeof(PixelHudBarType)))
            { Write("HUD/" + bar + "/Empty", Old.PixelHudSkin.GetEmpty(bar)); Write("HUD/" + bar + "/Filled", Old.PixelHudSkin.GetFilled(bar)); }
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("[Individual Art] Baked " + manifest.entries.Count + " PNGs. Existing files were preserved.");
        }

        private static void ExportPlayer(string action, string sourcePath)
        {
            var source = Old.LegacySource.Load(sourcePath);
            var tops = new[] { 14, 72, 129, 189, 253, 314, 373, 432 };
            var bottoms = new[] { 69, 126, 187, 247, 310, 371, 431, 489 };
            var feet = new[] { 67, 124, 184, 244, 307, 369, 429, 487 };
            for (var row = 0; row < 8; row++)
            for (var col = 0; col < 4; col++)
            {
                var height = bottoms[row] - tops[row];
                var sprite = Sprite.Create(source, new Rect(col * 64, source.height - bottoms[row], 64, height),
                    new Vector2(.5f, (bottoms[row] - feet[row] + 20f) / height), 40, 0, SpriteMeshType.FullRect);
                Write("Player/" + action + "/" + row.ToString("D2") + "/" + col.ToString("D2"), sprite);
                UnityEngine.Object.DestroyImmediate(sprite);
            }
        }
        private static void Frames(string prefix, Sprite[] frames)
        { for (var i = 0; i < frames.Length; i++) Write(prefix + "/" + i.ToString("D2"), frames[i]); }
        private static void Write(string id, Texture2D texture)
        {
            if (texture == null) throw new InvalidOperationException("Missing migration source: " + id);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100);
            Write(id, sprite); UnityEngine.Object.DestroyImmediate(sprite);
        }
        private static void Write(string id, Sprite sprite)
        {
            if (sprite == null) throw new InvalidOperationException("Missing migration sprite: " + id);
            var rect = sprite.rect;
            var width = Mathf.RoundToInt(rect.width); var height = Mathf.RoundToInt(rect.height);
            var entry = new Entry { id = id, width = width, height = height, pivot = sprite.pivot / rect.size, ppu = sprite.pixelsPerUnit, border = sprite.border };
            var path = Root + id + ".png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels(sprite.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height));
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = entry.pivot; importer.spritePixelsPerUnit = entry.ppu; importer.spriteBorder = entry.border;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            manifest.entries.Add(entry);
        }

        [MenuItem("Prototype/Art/Validate Individual Art Files")]
        public static void Validate()
        {
            var data = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            var ids = new HashSet<string>();
            foreach (var entry in data.entries)
            {
                if (!ids.Add(entry.id)) throw new InvalidOperationException("Duplicate art ID: " + entry.id);
                var path = entry.id.StartsWith("Player/", StringComparison.Ordinal)
                    ? "Assets/RuinRunner/ArtReview/RetiredGeneratedHero/" + entry.id + ".png" : Root + entry.id + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (sprite == null || importer == null || importer.spriteImportMode != SpriteImportMode.Single ||
                    sprite.rect.size != new Vector2(entry.width, entry.height) ||
                    Vector2.Distance(sprite.pivot / sprite.rect.size, entry.pivot) > .0001f ||
                    Mathf.Abs(sprite.pixelsPerUnit - entry.ppu) > .001f || sprite.border != entry.border ||
                    importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
                    throw new InvalidOperationException("Individual art metadata mismatch: " + path);
            }
            Debug.Log("[Individual Art] PASS " + ids.Count + " unique PNGs: dimensions, pivot, PPU, border, point filter, no compression.");
        }
    }
}

