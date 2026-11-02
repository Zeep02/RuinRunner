using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum PixelUiElement
    {
        Panel,
        Slot,
        SelectedSlot,
        Button
    }

    public enum PixelUiButtonState
    {
        Normal,
        Hover,
        Pressed,
        Disabled
    }

    /// <summary>Image-based UI backed by separate files; no runtime sheet slicing.</summary>
    public static class PixelUiSkin
    {
        private const string MenuBackgroundPath = "Art/Pixel/UI/main_menu_grove_v2";
        public static RectOffset FrameBorder => new RectOffset(6, 6, 6, 6);
        public static bool Available => IndividualArt.Exists("UI/Panel");
        public static bool HasMenuBackground => Resources.Load<Texture2D>(MenuBackgroundPath) != null;
        public static bool HasAccessoryAtlas => ItemIconLibrary.Get(AccessoryLootTable.All[0].Id, InventoryIconKind.Accessory) != null;
        public static Texture2D MenuBackground => Resources.Load<Texture2D>(MenuBackgroundPath);
        public static Texture2D MenuLogo => IndividualArt.Texture("UI/Logo");
        public static Texture2D GetInterfaceElement(PixelUiElement element) => IndividualArt.Texture("UI/" + element);
        public static Texture2D GetAccessoryIcon(string id) => InventoryIconFactory.Get(id, Color.white, InventoryIconKind.Accessory);
        public static Texture2D GetButtonState(PixelUiButtonState state) => IndividualArt.Texture("UI/ButtonStates/" + state);

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

    }
}
