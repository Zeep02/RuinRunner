using UnityEngine;

namespace RuinRunner
{
    /// <summary>GUI styles, panels and small text helpers.</summary>
    public sealed partial class GameHud
    {
        private void PrepareStyles()
        {
            if (titleStyle != null)
                return;

            var koreanFont = GameTypography.Regular;
            GUI.skin.font = koreanFont;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = GameTypography.Emphasis,
                fontSize = GameTypography.BodySize,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(),
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.white;

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = koreanFont,
                fontSize = GameTypography.BodySize,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(),
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.MiddleLeft
            };
            bodyStyle.normal.textColor = new Color(.94f, .91f, .86f);

            centerStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            detailStyle = new GUIStyle(bodyStyle)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                font = GameTypography.Detail,
                fontSize = GameTypography.DetailSize
            };

            quickKeyStyle = new GUIStyle(detailStyle)
            {
                font = GameTypography.Regular,
                fontSize = 12,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            quickAmmoStyle = new GUIStyle(quickKeyStyle) { alignment = TextAnchor.MiddleRight };
            quickNameStyle = new GUIStyle(centerStyle)
            {
                font = GameTypography.Detail,
                fontSize = 20,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = koreanFont,
                fontSize = GameTypography.BodySize,
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };

            var panelBackground = PixelUiSkin.GetInterfaceElement(PixelUiElement.Panel);
            var slotBackground = PixelUiSkin.GetInterfaceElement(PixelUiElement.Slot);
            var selectedBackground = PixelUiSkin.GetInterfaceElement(PixelUiElement.SelectedSlot);
            gaugeTextStyle = new GUIStyle(centerStyle)
            {
                font = GameTypography.Detail,
                fontSize = 20,
                padding = new RectOffset(),
                alignment = TextAnchor.MiddleCenter
            };
            gaugeFrameStyle = new GUIStyle { border = PixelUiSkin.FrameBorder };
            gaugeFrameStyle.normal.background = slotBackground;
            var buttonBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Normal);
            var buttonHoverBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Hover);
            var buttonPressedBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Pressed);
            var buttonDisabledBackground = PixelUiSkin.GetButtonState(PixelUiButtonState.Disabled);

            panelStyle = new GUIStyle(GUI.skin.box);
            slotStyle = new GUIStyle(GUI.skin.box);
            selectedSlotStyle = new GUIStyle(GUI.skin.box);
            if (panelBackground != null)
            {
                panelStyle.normal.background = panelBackground;
                panelStyle.border = CreateNineSliceBorder(panelBackground, .12f);
                panelStyle.normal.textColor = Color.white;
            }
            if (slotBackground != null)
            {
                slotStyle.normal.background = slotBackground;
                slotStyle.border = CreateNineSliceBorder(slotBackground, .2f);
                GUI.skin.box.normal.background = slotBackground;
                GUI.skin.box.border = CreateNineSliceBorder(slotBackground, .2f);
                GUI.skin.box.normal.textColor = Color.white;
            }
            if (selectedBackground != null)
            {
                selectedSlotStyle.normal.background = selectedBackground;
                selectedSlotStyle.border = CreateNineSliceBorder(selectedBackground, .2f);
            }

            if (buttonBackground != null)
            {
                buttonStyle.normal.background = buttonBackground;
                buttonStyle.border = CreateNineSliceBorder(buttonBackground, .16f);
            }
            if (buttonHoverBackground != null)
                buttonStyle.hover.background = buttonHoverBackground;
            if (buttonPressedBackground != null)
                buttonStyle.active.background = buttonPressedBackground;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = new Color(1f, .92f, .7f);
            buttonStyle.active.textColor = new Color(.92f, .82f, .65f);
            buttonStyle.focused.background = buttonStyle.hover.background;
            buttonStyle.focused.textColor = buttonStyle.hover.textColor;
            buttonStyle.onNormal.background = buttonStyle.normal.background;
            buttonStyle.onHover.background = buttonStyle.hover.background;
            buttonStyle.onActive.background = buttonStyle.active.background;
            buttonStyle.onFocused.background = buttonStyle.focused.background;
            selectedTabStyle = new GUIStyle(buttonStyle);
            selectedTabStyle.normal.background = selectedBackground == null ? buttonBackground : selectedBackground;
            selectedTabStyle.border = CreateNineSliceBorder(selectedBackground, .2f);

            disabledButtonStyle = new GUIStyle(buttonStyle);
            disabledButtonStyle.normal.background = buttonDisabledBackground == null ? buttonBackground : buttonDisabledBackground;
            disabledButtonStyle.hover.background = disabledButtonStyle.normal.background;
            disabledButtonStyle.active.background = disabledButtonStyle.normal.background;
            disabledButtonStyle.focused.background = disabledButtonStyle.normal.background;
            disabledButtonStyle.normal.textColor = new Color(.48f, .52f, .58f);
            disabledButtonStyle.hover.textColor = disabledButtonStyle.normal.textColor;
            disabledButtonStyle.active.textColor = disabledButtonStyle.normal.textColor;

            ApplySkinBackground(GUI.skin.verticalScrollbar, slotBackground, .22f);
            ApplySkinBackground(GUI.skin.verticalScrollbarThumb, selectedBackground, .22f);
            ApplySkinBackground(GUI.skin.verticalScrollbarUpButton, buttonBackground, .2f);
            ApplySkinBackground(GUI.skin.verticalScrollbarDownButton, buttonBackground, .2f);
            ApplySkinBackground(GUI.skin.horizontalScrollbar, slotBackground, .22f);
            ApplySkinBackground(GUI.skin.horizontalScrollbarThumb, selectedBackground, .22f);
            ApplySkinBackground(GUI.skin.horizontalScrollbarLeftButton, buttonBackground, .2f);
            ApplySkinBackground(GUI.skin.horizontalScrollbarRightButton, buttonBackground, .2f);
        }

        private void DrawPanel(Rect rect)
        {
            if (panelStyle != null && panelStyle.normal.background != null)
                GUI.Box(rect, string.Empty, panelStyle);
            else
                GUI.Box(rect, string.Empty);
        }

        private static RectOffset CreateNineSliceBorder(Texture2D texture, float ratio)
        {
            if (texture == null)
                return new RectOffset();
            return PixelUiSkin.FrameBorder;
        }

        private static void ApplySkinBackground(GUIStyle style, Texture2D texture, float borderRatio)
        {
            if (style == null || texture == null)
                return;
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.border = CreateNineSliceBorder(texture, borderRatio);
        }

        private string Key(GameAction action)
        {
            var binding = game.Settings.GetBinding(action);
            var key = binding.primary != KeyCode.None ? binding.primary : binding.alternate;
            return GameSettingsService.GetKeyName(key);
        }

        private static string WeaponActionDescription(WeaponDefinition weapon, int level = 0)
        {
            if (weapon == null)
                return "없음";
            return WeaponActionCatalog.Get(weapon).Description;
        }

        private void DrawHudName(Rect bounds, string name, GUIStyle textStyle)
        {
            hudNameContent.text = name;
            // Reuse the opaque pixel frame so bright world effects cannot erase the label.
            // Fit the name instead of covering the whole available HUD row.
            var width = Mathf.Min(bounds.width, Mathf.Ceil((textStyle.CalcSize(hudNameContent).x + 28f) / 4f) * 4f);
            var plate = new Rect(bounds.center.x - width * .5f, bounds.y - 2f, width, bounds.height + 4f);
            var scale = GameTypography.LayoutScale;
            plate.x = Mathf.Round(plate.x * scale) / scale;
            plate.y = Mathf.Round(plate.y * scale) / scale;
            GUI.Box(plate, GUIContent.none, gaugeFrameStyle);
            GUI.Label(new Rect(plate.x + 14f, bounds.y, plate.width - 28f, bounds.height), hudNameContent, textStyle);
        }

        private static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }
    }
}
