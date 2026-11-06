using UnityEngine;

namespace RuinRunner
{
    /// <summary>In-run HUD: bars, quick slots, banners, boss health and notices.</summary>
    public sealed partial class GameHud
    {
        private void DrawRunHud()
        {
            if (game.Player != null)
            {
                var healthRatio = Mathf.Clamp01(game.Player.Health / game.Player.MaxHealth);
                DrawPixelBar(new Rect(20, 20, 248, 24), healthRatio, PixelHudBarType.Health,
                    Mathf.CeilToInt(game.Player.Health) + " / " + Mathf.CeilToInt(game.Player.MaxHealth));

                var manaRatio = Mathf.Clamp01(game.Player.Mana / game.Player.MaxMana);
                DrawPixelBar(new Rect(20, 43, 248, 22), manaRatio, PixelHudBarType.Mana,
                    Mathf.CeilToInt(game.Player.Mana) + " / " + Mathf.CeilToInt(game.Player.MaxMana));
            }

            if (game.Progression != null)
            {
                var xpRatio = Mathf.Clamp01(game.Progression.Experience / (float)game.Progression.RequiredExperience);
                DrawPixelBar(new Rect(20, 68, 248, 12), xpRatio, PixelHudBarType.Experience, string.Empty);
                GUI.Label(new Rect(22, 82, 100, 28), "Lv " + game.Progression.Level, detailStyle);
            }
        }

        long goldShown;
        float goldBumpAt = -9f;
        private void DrawGold(float x, float y, GUIStyle style)
        {
            if (game.DisplayedGold > goldShown && Event.current.type == EventType.Repaint)
                goldBumpAt = Time.unscaledTime;
            if (Event.current.type == EventType.Repaint)
                goldShown = game.DisplayedGold;
            var bump = 1f + .35f * Mathf.Clamp01(1f - (Time.unscaledTime - goldBumpAt) / .16f);
            ItemIconLibrary.DrawGUI(new Rect(x + 12 - 12 * bump, y + 12 - 12 * bump, 24 * bump, 24 * bump), IndividualArt.Texture("HUD/Gold"));
            GUI.Label(new Rect(x + 30, y - 1, 200, 28), game.DisplayedGold.ToString(), style);
        }

        readonly System.Collections.Generic.Dictionary<PixelHudBarType, float> barMemory = new System.Collections.Generic.Dictionary<PixelHudBarType, float>();
        readonly System.Collections.Generic.Dictionary<PixelHudBarType, float> barChangedAt = new System.Collections.Generic.Dictionary<PixelHudBarType, float>();
        const float BarHoldSeconds = .28f, BarRunPerSecond = .7f, BarShakeSeconds = .26f, BarShakePixels = 4f;
        float healthHitAt = -9f;

        float RememberBar(PixelHudBarType type, float ratio)
        {
            if (!barMemory.TryGetValue(type, out var memory))
            {
                barMemory[type] = ratio;
                barChangedAt[type] = -9f;
                return ratio;
            }
            if (Event.current.type != EventType.Repaint)
                return memory;
            if (!Mathf.Approximately(memory, ratio) && Time.unscaledTime - barChangedAt[type] > BarHoldSeconds + 1f)
                barChangedAt[type] = Time.unscaledTime;
            if (type == PixelHudBarType.Health && ratio < memory - .001f && Time.unscaledTime - healthHitAt > BarShakeSeconds)
                healthHitAt = Time.unscaledTime;
            if (Time.unscaledTime - barChangedAt[type] >= BarHoldSeconds)
            {
                memory = Mathf.MoveTowards(memory, ratio, BarRunPerSecond * Time.unscaledDeltaTime);
                barMemory[type] = memory;
                if (Mathf.Approximately(memory, ratio))
                    barChangedAt[type] = -9f;
            }
            return memory;
        }

        float HealthBarShake()
        {
            var left = 1f - (Time.unscaledTime - healthHitAt) / BarShakeSeconds;
            return left <= 0f ? 0f : Mathf.Round(Mathf.Sin(Time.unscaledTime * 70f) * BarShakePixels * left);
        }

        private void DrawPixelBar(Rect rect, float ratio, PixelHudBarType type, string label)
        {
            ratio = Mathf.Clamp01(ratio);
            var memory = RememberBar(type, ratio);
            if (type == PixelHudBarType.Health)
                rect.x += HealthBarShake();
            // The same image frame as the rest of the interface, preserving its corners.
            GUI.Box(rect, GUIContent.none, gaugeFrameStyle);
            var oldColor = GUI.color;
            GUI.color = type == PixelHudBarType.Mana ? new Color(.48f, .73f, .94f) :
                type == PixelHudBarType.Experience ? new Color(.72f, .88f, .38f) : new Color(.81f, .25f, .27f);
            const float padding = 5f;
            var fill = new Rect(rect.x + padding, rect.y + padding, Mathf.Floor((rect.width - padding * 2) * ratio), rect.height - padding * 2);
            var full = rect.width - padding * 2;
            if (memory > ratio)
            {
                // What was just lost stays lit for a moment, then runs down to the new level.
                if (fill.width > 0)
                    GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = new Color(1f, .93f, .78f, 1f);
                GUI.DrawTexture(new Rect(fill.xMax, fill.y, Mathf.Floor(full * memory) - fill.width, fill.height), Texture2D.whiteTexture);
            }
            else
            {
                // What was just gained arrives bright, ahead of the bar filling to meet it.
                var settled = Mathf.Floor(full * memory);
                if (settled > 0)
                    GUI.DrawTexture(new Rect(fill.x, fill.y, settled, fill.height), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(GUI.color, Color.white, .7f);
                if (fill.width > settled)
                    GUI.DrawTexture(new Rect(fill.x + settled, fill.y, fill.width - settled, fill.height), Texture2D.whiteTexture);
            }
            GUI.color = oldColor;
            if (!string.IsNullOrEmpty(label))
            {
                GUI.Label(rect, label, gaugeTextStyle);
            }
        }

        private void DrawQuickSlots(float width, float height)
        {
            if (game.Inventory == null || game.IsGameOver)
                return;

            var activeWeapon = game.Inventory.EquippedWeapon;
            var nameY = game.IsStageResultReady && game.IsNearStageExit ? height - 196f : height - 136f;
            if (activeWeapon != null)
                DrawHudName(new Rect(width * .5f - 230f, nameY, 460f, 24f), activeWeapon.Name, quickNameStyle);

            var startX = width * .5f - 132f;
            for (var index = 0; index < game.Inventory.QuickSlots.Count; index++)
            {
                var slot = game.Inventory.QuickSlots[index];
                var active = index == game.Inventory.ActiveSlotIndex;
                var rect = new Rect(startX + index * 92f, height - 108f, 80f, 92f);
                DrawInventorySlot(rect, active);
                var action = index == 0 ? GameAction.Weapon1 : index == 1 ? GameAction.Weapon2 : GameAction.Weapon3;
                GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, 68f, 14f), Key(action), quickKeyStyle);
                if (slot.Weapon == null)
                {
                    GUI.Label(rect, "—", quickNameStyle);
                    continue;
                }

                var icon = InventoryIconFactory.Get(slot.Weapon.Id, slot.Weapon.Color, InventoryIconKind.Weapon);
                ItemIconLibrary.DrawGUI(new Rect(rect.center.x - 26f, rect.y + 20f, 52f, 54f), icon);
                if (slot.Weapon.AttackType == WeaponAttackType.Ranged)
                {
                    GUI.Label(new Rect(rect.x + 6f, rect.y + 76f, 68f, 14f), slot.Ammo.ToString(), quickAmmoStyle);
                }
            }
        }

        private void DrawMiniMap(float width) => mapView.DrawMini(game, width);

        private void DrawWaveBanner(float width)
        {
            if (Time.unscaledTime >= game.WaveBannerUntil)
                return;

            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((game.WaveBannerUntil - Time.unscaledTime) * 2f));
            GUI.color = Color.white;
            // The stroke is as long as the words need: a short count should not cross the whole view.
            var strokeWidth = Mathf.Clamp(titleStyle.CalcSize(new GUIContent(game.WaveBanner)).x + 200f, 300f, 720f);
            UiFx.BrushStroke(new Rect(width / 2f - strokeWidth / 2f, 130, strokeWidth, 60), Time.unscaledTime - game.WaveBannerSince, game.WaveBannerUntil - Time.unscaledTime);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Time.unscaledTime - game.WaveBannerSince - .08f) / .14f) * Mathf.Clamp01((game.WaveBannerUntil - Time.unscaledTime) / .25f));
            GUI.Label(new Rect(width / 2f - 300, 125, 600, 70), game.WaveBanner, titleStyle);
            GUI.color = oldColor;
        }

        private void DrawBossHealth(float width)
        {
            if (game.Boss == null || game.IsGameOver)
                return;

            var bossRatio = Mathf.Clamp01(game.Boss.Health / game.Boss.MaxHealth);
            DrawHudName(new Rect(width / 2f - 240, 14, 480, 30), game.CurrentStage.BossName, titleStyle);
            DrawPixelBar(new Rect(width / 2f - 300, 48, 600, 42), bossRatio, PixelHudBarType.Boss,
                Mathf.CeilToInt(game.Boss.Health) + " / " + Mathf.CeilToInt(game.Boss.MaxHealth));
        }

        const float ChoiceStagger = .05f, ChoiceLand = .2f;
        float choiceOpenedAt = -1f;
        private void DrawAccessorySelection(float width, float height)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(.025f, .035f, .035f, .9f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = oldColor;
            if (choiceOpenedAt < 0f)
            {
                choiceOpenedAt = Time.unscaledTime;
                UiFx.Play("UiLevelUp", new Vector2(width / 2f, height / 2f), 460f);
            }
            GUI.Label(new Rect(width / 2f - 300, height / 2f - 282, 600, 44), "레벨 업", titleStyle);
            var hovered = -1;
            for (var index = 0; index < game.CurrentAccessoryChoices.Length; index++)
            {
                var definition = game.CurrentAccessoryChoices[index];
                var angle = index * Mathf.PI * 2f / game.CurrentAccessoryChoices.Length;
                var rect = new Rect(width / 2f + Mathf.Sin(angle) * 190f - 48f,
                    height / 2f - 5f - Mathf.Cos(angle) * 155f - 48f, 96f, 96f);
                // Each one lands in its turn with a little overshoot.
                var landing = Mathf.Clamp01((Time.unscaledTime - choiceOpenedAt - index * ChoiceStagger) / ChoiceLand);
                var pop = landing * (1f + 1.1f * (1f - landing));
                if (pop < .999f || pop > 1.001f)
                    rect = new Rect(rect.center.x - rect.width * pop * .5f, rect.center.y - rect.height * pop * .5f, rect.width * pop, rect.height * pop);
                var isHovered = rect.Contains(Event.current.mousePosition);
                GUI.color = Color.Lerp(Color.white, definition.Color, .5f);
                GUI.Box(rect, string.Empty, isHovered ? selectedSlotStyle : slotStyle);
                GUI.color = oldColor;
                var icon = InventoryIconFactory.Get(definition.Id, definition.Color, InventoryIconKind.Accessory);
                ItemIconLibrary.DrawGUI(new Rect(rect.x + rect.width / 6f, rect.y + rect.height / 6f, rect.width * 2f / 3f, rect.height * 2f / 3f), icon);
                GUI.Label(new Rect(rect.x - 40, rect.yMax + 3, rect.width + 80, 30),
                    "[" + Key(GameSettingsService.AccessoryChoiceAction(index)) + "]", centerStyle);
                if (isHovered)
                    hovered = index;
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    UiFx.Play("UiPick", rect.center, 260f);
                    game.SelectAccessory(definition);
                    return;
                }
            }
            if (hovered >= 0)
                DrawItemTooltip(width, height, "C:" + hovered);
        }

        // A corner note, out of the fight's way: as wide as its words and gone soon.
        private void DrawNotifications(float height)
        {
            if (Time.unscaledTime >= game.LootMessageUntil)
                return;
            var words = new GUIContent(game.LootMessage);
            var size = bodyStyle.CalcSize(words);
            var box = new Rect(20f, height - 32f - size.y - 14f, size.x + 28f, size.y + 14f);
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((game.LootMessageUntil - Time.unscaledTime) * 4f));
            DrawPanel(box);
            GUI.Label(new Rect(box.x + 14f, box.y + 7f, size.x, size.y), words, bodyStyle);
            GUI.color = oldColor;
        }

        private void DrawStageGuidance(float width, float height)
        {
            GUI.Label(new Rect(20, 112, 300, 30), (game.CurrentStageIndex + 1) + "/3  " + game.CurrentStage.Name, bodyStyle);
            if (!game.IsStageResultReady || !game.IsNearStageExit)
                return;
            var prompt = new Rect(width / 2f - 270f, height - 168f, 540f, 58f);
            if (game.PendingStageRewards > 0)
            {
                var hold = game.LeaveEquipmentProgress > 0 ? "  " + Mathf.CeilToInt(game.LeaveEquipmentProgress * 100) + "%" : "";
                if (GUI.Button(prompt, "남은 장비 " + game.PendingStageRewards + "개 두고 가기  [" + Key(GameAction.Interact) + " 길게]" + hold, buttonStyle))
                    game.LeaveUncollectedStageEquipment();
                return;
            }
            var action = game.CurrentStage.IsFinal ? "마지막 봉인 해제" : CampaignStage.Get(game.CurrentStageIndex + 1).Name + " 진입";
            if (GUI.Button(prompt, action + "  [" + Key(GameAction.Interact) + "]", buttonStyle))
                game.ShowStageResult();
        }
    }
}
