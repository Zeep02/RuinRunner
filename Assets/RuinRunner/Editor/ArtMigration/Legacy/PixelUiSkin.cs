using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner.EditorTools.LegacyArt
{
    /// <summary>
    /// 생성형 원본을 런타임용 UI 조각으로 분리한다. IMGUI 배치는 코드로 유지하되
    /// Unity 기본 박스/버튼 이미지를 사용하지 않도록 시각 자산의 단일 진입점을 제공한다.
    /// </summary>
    public static class PixelUiSkin
    {
        private const string MenuBackgroundPath = "Art/Pixel/UI/main_menu_grove_v2";
        private const string InterfaceAtlasPath = "Art/Pixel/UI/interface_flat_atlas_v2";
        private const string MenuLogoPath = "Art/Pixel/UI/ruin_runner_logo_v1";
        private const string AccessoryAtlasPath = "Art/Pixel/UI/accessory_icon_atlas_v1";
        // Pack generated frame art into UI-sized cells; GUIStyle.border is in UI pixels,
        // not a percentage of the large source image. 6px includes the entire frame.
        private const int InterfaceCellSize = 64;
        public static RectOffset FrameBorder => new RectOffset(6, 6, 6, 6);
        private static Texture2D menuLogo;

        private static readonly Dictionary<int, Texture2D> InterfaceTextures = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<string, Texture2D> AccessoryTextures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<PixelUiButtonState, Texture2D> ButtonStateTextures = new Dictionary<PixelUiButtonState, Texture2D>();

        public static bool Available => LegacySource.Load(InterfaceAtlasPath) != null;
        public static bool HasMenuBackground => LegacySource.Load(MenuBackgroundPath) != null;
        public static bool HasAccessoryAtlas => LegacySource.Load(AccessoryAtlasPath) != null;
        public static Texture2D MenuBackground => LegacySource.Load(MenuBackgroundPath);
        public static Texture2D MenuLogo => menuLogo != null ? menuLogo :
            (menuLogo = CropCell(LegacySource.Load(MenuLogoPath), 1, 1, 0, true));

        public static Texture2D GetInterfaceElement(PixelUiElement element)
        {
            var index = (int)element;
            Texture2D cached;
            if (InterfaceTextures.TryGetValue(index, out cached)) return cached;

            var source = LegacySource.Load(InterfaceAtlasPath);
            cached = CropCell(source, 2, 2, index, true, InterfaceCellSize);
            if (cached != null)
            {
                cached.name = "PixelUi_" + element;
                InterfaceTextures[index] = cached;
            }
            return cached;
        }

        public static Texture2D GetAccessoryIcon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Texture2D cached;
            if (AccessoryTextures.TryGetValue(id, out cached)) return cached;

            var index = -1;
            for (var candidate = 0; candidate < AccessoryLootTable.All.Count; candidate++)
            {
                if (AccessoryLootTable.All[candidate].Id != id) continue;
                index = candidate;
                break;
            }
            if (index < 0) return null;

            var source = LegacySource.Load(AccessoryAtlasPath);
            cached = CropCell(source, 4, 2, index, false);
            if (cached != null)
            {
                cached.name = "AccessoryIcon_" + id;
                AccessoryTextures[id] = cached;
            }
            return cached;
        }

        public static GUIStyle CreateWorldPromptStyle(Color textColor, int fontSize, bool highlighted)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = highlighted ? GameTypography.Emphasis : GameTypography.Regular,
                fontSize = Mathf.Max(12, Mathf.RoundToInt(fontSize / 12f) * 12),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset(12, 12, 5, 6)
            };
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;

            var background = GetInterfaceElement(highlighted ? PixelUiElement.SelectedSlot : PixelUiElement.Slot);
            if (background != null)
            {
                style.normal.background = background;
                style.hover.background = background;
                style.active.background = background;
                style.border = FrameBorder;
            }
            return style;
        }

        public static Texture2D GetButtonState(PixelUiButtonState state)
        {
            Texture2D cached;
            if (ButtonStateTextures.TryGetValue(state, out cached)) return cached;

            var source = GetInterfaceElement(PixelUiElement.Button);
            if (source == null) return null;
            if (state == PixelUiButtonState.Normal) return source;

            var pixels = source.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                if (pixel.a == 0) continue;
                switch (state)
                {
                    case PixelUiButtonState.Hover:
                        pixel.r = (byte)Mathf.Lerp(pixel.r, 238f, .18f);
                        pixel.g = (byte)Mathf.Lerp(pixel.g, 211f, .18f);
                        pixel.b = (byte)Mathf.Lerp(pixel.b, 164f, .18f);
                        break;
                    case PixelUiButtonState.Pressed:
                        pixel.r = (byte)(pixel.r * .78f);
                        pixel.g = (byte)(pixel.g * .78f);
                        pixel.b = (byte)(pixel.b * .78f);
                        break;
                    case PixelUiButtonState.Disabled:
                        var luminance = (byte)((pixel.r * 3 + pixel.g * 6 + pixel.b) / 10);
                        pixel.r = (byte)(luminance * .5f);
                        pixel.g = (byte)(luminance * .54f);
                        pixel.b = (byte)(luminance * .58f);
                        pixel.a = (byte)(pixel.a * .78f);
                        break;
                }
                pixels[index] = pixel;
            }

            cached = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = "PixelUi_Button_" + state,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            cached.SetPixels32(pixels);
            cached.Apply(false, false);
            ButtonStateTextures[state] = cached;
            return cached;
        }

        private static Texture2D CropCell(Texture2D source, int columns, int rows, int index, bool trimTransparent, int outputSize = 0)
        {
            if (source == null || !source.isReadable || index < 0 || index >= columns * rows) return null;

            var column = index % columns;
            var row = index / columns;
            var xMin = Mathf.RoundToInt(column * source.width / (float)columns);
            var xMax = Mathf.RoundToInt((column + 1) * source.width / (float)columns);
            var yMin = Mathf.RoundToInt((rows - row - 1) * source.height / (float)rows);
            var yMax = Mathf.RoundToInt((rows - row) * source.height / (float)rows);
            var width = xMax - xMin;
            var height = yMax - yMin;
            var pixels = source.GetPixels32();

            if (trimTransparent)
            {
                var left = width;
                var right = -1;
                var bottom = height;
                var top = -1;
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    if (pixels[(yMin + y) * source.width + xMin + x].a < 128) continue;
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }

                if (right >= left && top >= bottom)
                {
                    var cropLeft = Mathf.Max(0, left - 2);
                    var cropBottom = Mathf.Max(0, bottom - 2);
                    var cropRight = Mathf.Min(width - 1, right + 2);
                    var cropTop = Mathf.Min(height - 1, top + 2);
                    xMin += cropLeft;
                    yMin += cropBottom;
                    width = cropRight - cropLeft + 1;
                    height = cropTop - cropBottom + 1;
                }
            }

            var outputWidth = outputSize > 0 ? outputSize : width;
            var outputHeight = outputSize > 0 ? outputSize : height;
            var output = new Color32[outputWidth * outputHeight];
            for (var y = 0; y < outputHeight; y++)
            for (var x = 0; x < outputWidth; x++)
            {
                var sourceX = Mathf.Min(width - 1, Mathf.FloorToInt((x + .5f) * width / outputWidth));
                var sourceY = Mathf.Min(height - 1, Mathf.FloorToInt((y + .5f) * height / outputHeight));
                var pixel = pixels[(yMin + sourceY) * source.width + xMin + sourceX];
                // Pixel UI is a cutout, not a translucent overlay. Background extraction
                // left 252-254 alpha in opaque panels; normalize the packed UI only.
                pixel = SpriteMatte.AlphaCutout(pixel);
                output[y * outputWidth + x] = pixel;
            }

            var texture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(output);
            texture.Apply(false, false);
            return texture;
        }
    }
}
