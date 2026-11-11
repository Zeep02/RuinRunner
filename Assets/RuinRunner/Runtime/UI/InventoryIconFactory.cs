using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum InventoryIconKind { Weapon, Passive, Accessory }

    public static class InventoryIconFactory
    {
        private static readonly Dictionary<string, Sprite> WorldSprites = new Dictionary<string, Sprite>();
        public static Texture2D Get(string id, Color rarityColor, InventoryIconKind kind)
        {
            if (kind == InventoryIconKind.Weapon)
                id = WeaponLootTable.ArtIdOf(id); // an admin-made variant draws its base weapon's icon
            var accessory = kind == InventoryIconKind.Accessory ? AccessoryLootTable.Find(id) : null;
            return ItemIconLibrary.Get(accessory == null ? id : accessory.IconId, accessory == null ? kind : accessory.IconKind);
        }
        public static Sprite GetWorldSprite(string id, Color rarityColor, InventoryIconKind kind)
        {
            var key = kind + ":" + id;
            if (WorldSprites.TryGetValue(key, out var sprite))
                return sprite;
            if (kind == InventoryIconKind.Weapon)
                id = WeaponLootTable.ArtIdOf(id); // an admin-made variant lies on the floor as its base weapon
            var texture = Get(id, rarityColor, kind);
            if (texture == null)
                return null;
            var accessory = kind == InventoryIconKind.Accessory ? AccessoryLootTable.Find(id) : null;
            var path = ItemIconLibrary.ResourcePath(accessory == null ? id : accessory.IconId,
                accessory == null ? kind : accessory.IconKind);
            var importedSprite = Resources.Load<Sprite>(path);
            if (importedSprite == null)
            {
                Debug.LogError("[Item Art] Missing imported item Sprite: Resources/" + path + ".png");
                return null;
            }
            var bounds = ItemIconLibrary.ContentBounds(texture);
            if (bounds.width == 0)
                return null;
            // Center the visible content without normalizing every item to one world unit.
            sprite = Sprite.Create(texture, new Rect(bounds.x, bounds.y, bounds.width, bounds.height),
                new Vector2(.5f, .5f), importedSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "WorldItem_" + id;
            WorldSprites[key] = sprite;
            return sprite;
        }
    }
}
