using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>One transparent PNG per item ID; no runtime atlas coordinates.</summary>
    public static class ItemIconLibrary
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<Texture2D, RectInt> BoundsCache = new Dictionary<Texture2D, RectInt>();
        public static string ResourcePath(string id, InventoryIconKind kind)
        {
            if(kind==InventoryIconKind.Weapon && NativeWeaponArt.Supports(id))return NativeWeaponArt.Path(id);
            // Legacy passive enum names and current accessory IDs share the same source PNG.
            var accessory=kind==InventoryIconKind.Accessory?AccessoryLootTable.Find(id):null;
            if(kind==InventoryIconKind.Passive && System.Enum.TryParse<PassiveItemType>(id,out var passive))
                accessory=AccessoryLootTable.Find((AccessoryType)((int)passive+8));
            if(accessory!=null)return "Art/Pixel/Separated/Equipment/Accessory/"+accessory.Id;
            return "Art/Pixel/Items/" + ((kind==InventoryIconKind.Weapon && DemoContent.IsWeapon(id))?"Demo/":"")+kind+"/"+id;
        }
        public static Texture2D Get(string id, InventoryIconKind kind)
        {
            var path = ResourcePath(id, kind);
            if (Cache.TryGetValue(path, out var texture)) return texture;
            texture = Resources.Load<Texture2D>(path);
            if (texture == null) Debug.LogError("[Item Art] Missing individual icon: Resources/" + path + ".png");
            Cache[path] = texture;
            return texture;
        }
        public static RectInt ContentBounds(Texture2D texture)
        {
            if(BoundsCache.TryGetValue(texture,out var cached))return cached;
            var pixels = texture.GetPixels32();
            var left = texture.width; var bottom = texture.height; var right = -1; var top = -1;
            for (var y = 0; y < texture.height; y++)
            for (var x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a < 128) continue;
                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
            var bounds=right < left ? new RectInt() : new RectInt(left, bottom, right - left + 1, top - bottom + 1);
            BoundsCache[texture]=bounds;return bounds;
        }
        private const int SetIconPixels=30,SetIconMinimum=20;
        public static void DrawGUI(Rect area,Texture2D texture)
        {
            if(texture==null)return;
            var bounds=ContentBounds(texture);if(bounds.width==0||bounds.height==0)return;
            var uiScale=Mathf.Abs(GUI.matrix.m00);
            // Item art is drawn to about 30 pixels. Sized as one set, a 26 pixel icon cannot come out
            // twice as large as its 30 pixel neighbour when both round to whole physical pixels.
            var reference=Mathf.Max(bounds.width,bounds.height)>=SetIconMinimum?SetIconPixels:0;
            var fit=Mathf.Min(area.width*uiScale/Mathf.Max(bounds.width,reference),area.height*uiScale/Mathf.Max(bounds.height,reference));
            // Integer physical pixels keep a one-pixel contour equally thick on every side.
            var pixels=fit>=1f?Mathf.Floor(fit):fit;
            var width=bounds.width*pixels;var height=bounds.height*pixels;
            var rect=new Rect(Mathf.Round(area.center.x*uiScale-width*.5f)/uiScale,
                Mathf.Round(area.center.y*uiScale-height*.5f)/uiScale,width/uiScale,height/uiScale);
            var uv=new Rect((float)bounds.x/texture.width,(float)bounds.y/texture.height,
                (float)bounds.width/texture.width,(float)bounds.height/texture.height);
            GUI.DrawTextureWithTexCoords(rect,texture,uv,true);
        }
    }
}
