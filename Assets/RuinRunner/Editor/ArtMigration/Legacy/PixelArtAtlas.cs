using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner.EditorTools.LegacyArt
{
    public static class PixelArtAtlas
    {
        private const string PlayerPath = "Art/Pixel/Player/player_unarmed_animation_atlas";
        private const string EnemyPath = "Art/Pixel/Enemies/enemy_animation_atlas";
        private const string BossPath = "Art/Pixel/Enemies/boss_animation_atlas";
        private const string TerrainPath = "Art/Pixel/World/ruin_tile_atlas";
        private const string MeleeSlashPath = "Art/Pixel/VFX/melee_slash_effect_atlas";

        private static readonly Dictionary<string, Sprite[]> SpriteGrids = new Dictionary<string, Sprite[]>();
        private static readonly Dictionary<string, Texture2D> ChromaKeyTextures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> HeldWeaponSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<int, Sprite> TerrainSprites = new Dictionary<int, Sprite>();


        public static bool HasPlayerAtlas => LegacySource.Load(PlayerPath) != null;
        public static bool HasEnemyAtlas => LegacySource.Load(EnemyPath) != null && LegacySource.Load(BossPath) != null;
        public static bool HasTerrainAtlas => LegacySource.Load(TerrainPath) != null;
        public static bool HasMeleeAttackAtlas => LegacySource.Load(PlayerPath) != null && LegacySource.Load(MeleeSlashPath) != null;

        public static PlayerAnimationLibrary CreateDefaultPlayerLibrary()
        {
            var grid = GetChromaGrid(PlayerPath, 8, 12, true);
            if (grid == null) return null;

            var library = ScriptableObject.CreateInstance<PlayerAnimationLibrary>();
            library.framesPerSecond = 12f;
            library.visualScale = Vector2.one;
            library.visualOffset = new Vector2(0f, -.575f);
            library.idle = Row(grid, 0, 8);
            library.run = Row(grid, 1, 8);
            library.brake = Row(grid, 2, 8);
            library.jumpRise = Row(grid, 3, 8);
            library.fall = Row(grid, 4, 8);
            library.land = Row(grid, 5, 8);
            library.attack = Row(grid, 6, 8);
            library.heavyAttack = Row(grid, 7, 8);
            library.charge = Row(grid, 7, 8);
            library.guard = Row(grid, 8, 8);
            library.dash = Row(grid, 9, 8);
            library.hurt = Row(grid, 10, 8);
            return library;
        }

        public static Sprite[] GetMeleeSlashFrames(WeaponFamily family, bool critical)
        {
            var grid = GetChromaGrid(MeleeSlashPath, 8, 6, false);
            if (grid == null) return null;
            if (critical) return Row(grid, 5, 8);
            var row = family == WeaponFamily.Dagger ? 0
                : family == WeaponFamily.Mace ? 2
                : family == WeaponFamily.Katana ? 3
                : family == WeaponFamily.Greatsword ? 4
                : 1;
            return Row(grid, row, 8);
        }

        public static Sprite[] GetEnemyFrames(EnemyType type, int state)
        {
            if (type == EnemyType.Boss) return GetBossFrames(Mathf.Clamp(state, 0, 5));
            return GeneratedActorFrames.GetEnemy(Mathf.Clamp((int)type, 0, 3) * 3 + Mathf.Clamp(state, 0, 2));
        }

        public static Sprite[] GetBossFrames(int row)
        {
            return GeneratedActorFrames.GetBoss(row);
        }

        public static Texture2D GetInventoryIcon(string id, InventoryIconKind kind)
        {
            return ItemIconLibrary.Get(id, kind);
        }

        public static Sprite GetHeldWeaponSprite(WeaponDefinition weapon)
        {
            if (weapon == null) return null;

            Sprite cached;
            if (HeldWeaponSprites.TryGetValue(weapon.Id, out cached)) return cached;

            var icon = GetInventoryIcon(weapon.Id, InventoryIconKind.Weapon);
            if (icon == null || !icon.isReadable) return null;

            var bounds = ItemIconLibrary.ContentBounds(icon);
            if (bounds.width == 0) return null;
            cached = Sprite.Create(icon, new Rect(bounds.x, bounds.y, bounds.width, bounds.height),
                GetHeldWeaponPivot(weapon.Family), Mathf.Max(bounds.width, bounds.height), 0, SpriteMeshType.FullRect);
            cached.name = "HeldWeaponSprite_" + weapon.Id;
            HeldWeaponSprites[weapon.Id] = cached;
            return cached;
        }

        public static Sprite GetTerrainSprite(int index)
        {
            index = Mathf.Clamp(index, 0, 15);
            Sprite cached;
            if (TerrainSprites.TryGetValue(index, out cached)) return cached;

            var source = LegacySource.Load(TerrainPath);
            if (source == null) return null;

            const int columns = 4;
            const int rows = 4;
            var column = index % columns;
            var row = index / columns;
            var cellWidth = source.width / (float)columns;
            var cellHeight = source.height / (float)rows;
            var rect = new Rect(column * cellWidth, source.height - (row + 1) * cellHeight, cellWidth, cellHeight);
            cached = Sprite.Create(source, rect, new Vector2(.5f, .5f), cellHeight, 0, SpriteMeshType.FullRect);
            cached.name = "RuinTerrain_" + index;
            TerrainSprites[index] = cached;
            return cached;
        }


        private static Vector2 GetHeldWeaponPivot(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Bow: return new Vector2(.5f, .5f);
                case WeaponFamily.Crossbow: return new Vector2(.32f, .34f);
                case WeaponFamily.Pistol: return new Vector2(.28f, .27f);
                case WeaponFamily.Rifle:
                case WeaponFamily.Shotgun:
                case WeaponFamily.Cannon:
                    return new Vector2(.34f, .32f);
                default:
                    return new Vector2(.14f, .14f);
            }
        }

        private static Sprite[] GetChromaGrid(string path, int columns, int rows, bool footAnchored)
        {
            var key = path + ":" + columns + "x" + rows + (footAnchored ? ":feet" : ":center");
            Sprite[] cached;
            if (SpriteGrids.TryGetValue(key, out cached)) return cached;

            var texture = GetChromaKeyTexture(path);
            if (texture == null) return null;

            var pixels = footAnchored ? texture.GetPixels32() : null;
            cached = new Sprite[columns * rows];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var xMin = Mathf.RoundToInt(column * texture.width / (float)columns);
                var xMax = Mathf.RoundToInt((column + 1) * texture.width / (float)columns);
                var yMin = Mathf.RoundToInt((rows - row - 1) * texture.height / (float)rows);
                var yMax = Mathf.RoundToInt((rows - row) * texture.height / (float)rows);
                var rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                var pivot = footAnchored ? FindFootPivot(pixels, texture.width, xMin, yMin, xMax - xMin, yMax - yMin) : new Vector2(.5f, .5f);
                var sprite = Sprite.Create(texture, rect, pivot, rect.height * .72f,
                    0, SpriteMeshType.FullRect);
                sprite.name = path.Replace('/', '_') + "_" + row + "_" + column;
                cached[row * columns + column] = sprite;
            }

            SpriteGrids[key] = cached;
            return cached;
        }

        private static Vector2 FindFootPivot(Color32[] pixels, int textureWidth, int xMin, int yMin, int width, int height)
        {
            var lowest = height;
            var foundDark = false;
            for (var localY = 0; localY < height; localY++)
            for (var localX = Mathf.RoundToInt(width * .08f); localX < Mathf.RoundToInt(width * .92f); localX++)
            {
                var color = pixels[(yMin + localY) * textureWidth + xMin + localX];
                if (color.a < 32 || color.r + color.g + color.b > 390) continue;
                lowest = Mathf.Min(lowest, localY);
                foundDark = true;
            }

            if (!foundDark) return new Vector2(.5f, .08f);
            var bandTop = Mathf.Min(height - 1, lowest + Mathf.Max(3, Mathf.RoundToInt(height * .09f)));
            var footMinX = width;
            var footMaxX = 0;
            for (var localY = lowest; localY <= bandTop; localY++)
            for (var localX = 0; localX < width; localX++)
            {
                var color = pixels[(yMin + localY) * textureWidth + xMin + localX];
                if (color.a < 32 || color.r + color.g + color.b > 390) continue;
                footMinX = Mathf.Min(footMinX, localX);
                footMaxX = Mathf.Max(footMaxX, localX);
            }
            var footCenter = footMinX <= footMaxX ? (footMinX + footMaxX + 1) * .5f : width * .5f;
            return new Vector2(Mathf.Clamp01(footCenter / width), Mathf.Clamp01(lowest / (float)height));
        }

        private static Texture2D GetChromaKeyTexture(string path)
        {
            Texture2D cached;
            if (ChromaKeyTextures.TryGetValue(path, out cached)) return cached;

            var source = LegacySource.Load(path);
            if (source == null || !source.isReadable) return null;

            var pixels = source.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = SpriteMatte.ChromaCutout(pixels[index]);
            }

            cached = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            cached.name = source.name + "_ChromaKey";
            cached.filterMode = FilterMode.Point;
            cached.wrapMode = TextureWrapMode.Clamp;
            cached.SetPixels32(pixels);
            cached.Apply(false, false);
            ChromaKeyTextures[path] = cached;
            return cached;
        }

        private static Sprite[] Row(Sprite[] grid, int row, int columns)
        {
            var frames = new Sprite[columns];
            Array.Copy(grid, row * columns, frames, 0, columns);
            return frames;
        }

    }
}

