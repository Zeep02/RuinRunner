#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>One-time, deterministic extraction of existing art. Never overwrites individual replacements.</summary>
    public static class ItemIconMigration
    {
        private static readonly Dictionary<WeaponFamily, int[]> WeaponIconSlots = new Dictionary<WeaponFamily, int[]>
        {
            { WeaponFamily.Dagger, new[] { 0, 2, 6 } },
            { WeaponFamily.Longsword, new[] { 1, 4, 7 } },
            { WeaponFamily.Mace, new[] { 8, 9, 10, 11 } },
            { WeaponFamily.Katana, new[] { 12, 13, 14, 15 } },
            { WeaponFamily.Greatsword, new[] { 16, 17, 18, 19 } },
            { WeaponFamily.Bow, new[] { 20, 21, 22, 23 } },
            { WeaponFamily.Crossbow, new[] { 24, 25, 26, 27 } },
            { WeaponFamily.Pistol, new[] { 28, 29, 30, 31 } },
            { WeaponFamily.Rifle, new[] { 32, 33, 34, 35 } },
            { WeaponFamily.Shotgun, new[] { 36, 37, 38 } },
            { WeaponFamily.Cannon, new[] { 39 } }
        };

        private static int GetWeaponIconIndex(string id)
        {
            WeaponDefinition selected = null;
            foreach (var weapon in WeaponLootTable.All)
            {
                if (weapon.Id != id) continue;
                selected = weapon;
                break;
            }

            if (selected == null) return -1;
            var occurrence = 0;
            foreach (var weapon in WeaponLootTable.All)
            {
                if (weapon == selected) break;
                if (weapon.Family == selected.Family) occurrence++;
            }

            int[] slots;
            if (!WeaponIconSlots.TryGetValue(selected.Family, out slots) || slots.Length == 0) return -1;
            return slots[Mathf.Min(occurrence, slots.Length - 1)];
        }
        [MenuItem("Prototype/Art/Extract Missing Individual Item PNGs")]
        public static void ExportMissing()
        {
            PixelArtImportProcessor.EnsurePixelImports();
            var source = LegacyArt.LegacySource.Load("Art/Pixel/UI/item_icon_atlas");
            foreach (var weapon in WeaponLootTable.All)
            {
                var cell = GetWeaponIconIndex(weapon.Id);
                // Weapon kinds without a slot in the old atlas have only their own icon files.
                if (cell < 0) continue;
                Export(weapon.Id, InventoryIconKind.Weapon, Crop(source, cell % 8, cell / 8, 8, true));
            }
            // The original sixth row contains NINE icons, not eight. Enum order is not art order.
            var columns = new[] { 0, 1, 4, 5, 2, 6, 3, 7, 8, 0 };
            foreach (PassiveItemType type in Enum.GetValues(typeof(PassiveItemType)))
                Export(type.ToString(), InventoryIconKind.Passive, Crop(source, columns[(int)type], type == PassiveItemType.SoulCapacitor ? 6 : 5, 9, true));
            foreach (var accessory in AccessoryLootTable.All)
                if (!accessory.UsesLegacyPassiveIcon)
                    Export(accessory.Id, InventoryIconKind.Accessory, LegacyArt.PixelUiSkin.GetAccessoryIcon(accessory.Id));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Item Art] Individual PNG extraction complete; existing files preserved.");
        }

        private static Texture2D Crop(Texture2D source, int column, int row, int columns, bool removeMatte)
        {
            // Measured original row boundaries; the bottom three rows are not the same height as weapon rows.
            var boundaries = new[] { 0, 157, 314, 471, 628, 785, 931, 1076, 1254 };
            var x0 = Mathf.RoundToInt(source.width * column / (float)columns);
            var x1 = Mathf.RoundToInt(source.width * (column + 1) / (float)columns);
            var y0 = source.height - Mathf.RoundToInt(source.height * boundaries[row + 1] / 1254f);
            var y1 = source.height - Mathf.RoundToInt(source.height * boundaries[row] / 1254f);
            var crop = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGBA32, false);
            crop.SetPixels(source.GetPixels(x0, y0, crop.width, crop.height));
            if (removeMatte)
            {
                var pixels = crop.GetPixels32();
                var retain = new bool[pixels.Length];
                // Preserve existing dark outline pixels around the bright subject, not the navy cell panel.
                for (var y = 0; y < crop.height; y++)
                for (var x = 0; x < crop.width; x++)
                {
                    var pixel = pixels[y * crop.width + x];
                    if (pixel.a < 128 || Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) <= 52) continue;
                    for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++)
                        if (x + dx >= 0 && x + dx < crop.width && y + dy >= 0 && y + dy < crop.height)
                            retain[(y + dy) * crop.width + x + dx] = true;
                }
                for (var i = 0; i < pixels.Length; i++)
                    if (!retain[i]) pixels[i] = new Color32(); else pixels[i].a = 255;
                crop.SetPixels32(pixels);
            }
            crop.Apply();
            return crop;
        }

        private static void Export(string id, InventoryIconKind kind, Texture2D source)
        {
            var path = "Assets/RuinRunner/Resources/" + ItemIconLibrary.ResourcePath(id, kind) + ".png";
            if (File.Exists(path)) return;
            if (source == null) throw new InvalidOperationException("Missing source art: " + id);
            var bounds = ItemIconLibrary.ContentBounds(source);
            if (bounds.width == 0) throw new InvalidOperationException("Empty item art: " + id);
            // Preserve source pixels exactly: trim, then center on a square with consistent proportional padding.
            var side = Mathf.CeilToInt(Mathf.Max(bounds.width, bounds.height) * 1.2f / 8f) * 8;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Color32[side * side]);
            texture.SetPixels((side - bounds.width) / 2, (side - bounds.height) / 2, bounds.width, bounds.height,
                source.GetPixels(bounds.x, bounds.y, bounds.width, bounds.height));
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
#endif
