using UnityEngine;

namespace RuinRunner
{
    /// <summary>Bag, equipment drag and drop, tooltips and the stats sheet.</summary>
    public sealed partial class GameHud
    {
        private void DrawInventory(float width, float height)
        {
            PrepareTownStyles();
            var panel = new Rect(width / 2f - 500, height / 2f - 310, 1000, 620);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 30, panel.y + 12, 620, 40), "INVENTORY", titleStyle);

            GUI.Label(new Rect(panel.x + 30, panel.y + 52, 620, 30), "QUICK SLOTS", detailStyle);
            var hoverKey = string.Empty;
            var quickSlotRects = new Rect[game.Inventory.QuickSlots.Count];
            for (var slotIndex = 0; slotIndex < game.Inventory.QuickSlots.Count; slotIndex++)
            {
                var slot = game.Inventory.QuickSlots[slotIndex];
                var slotRect = new Rect(panel.x + 30 + slotIndex * 205, panel.y + 80, 190, 76);
                quickSlotRects[slotIndex] = slotRect;
                var slotHovered = slotRect.Contains(Event.current.mousePosition);
                DrawWideInventorySlot(slotRect, slotHovered || slotIndex == game.InventoryFocusQuickSlot);
                GUI.Label(new Rect(slotRect.x + 8, slotRect.y + 4, 26, 16), Key((GameAction)((int)GameAction.Weapon1 + slotIndex)), quickKeyStyle);
                if (slot.Weapon == null)
                {
                    GUI.Label(new Rect(slotRect.x + 40, slotRect.y + 12, 140, 38), "비어 있음", quickNameStyle);
                    continue;
                }
                var icon = InventoryIconFactory.Get(slot.Weapon.Id, slot.Weapon.Color, InventoryIconKind.Weapon);
                ItemIconLibrary.DrawGUI(new Rect(slotRect.x + 8, slotRect.y + 6, 68, 68), icon);
                GUI.Label(new Rect(slotRect.x + 80, slotRect.y + 10, 100, 54), slot.Weapon.Name, talentText);
                DrawUpgradeBadge(slotRect, slot.Item);
                if (slotHovered)
                    hoverKey = "Q:" + slotIndex;
                if (slotHovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    game.FocusInventoryQuickSlot(slotIndex);
                TryBeginInventoryDrag(slotRect, "Q:" + slotIndex, icon);
            }

            var weapons = game.Inventory.StoredWeapons;
            var accessories = game.Inventory.Accessories;
            var inventoryCells = new Rect[game.Inventory.UnlockedCapacity];
            for (var slotIndex = 0; slotIndex < InventorySystem.MaximumCapacity; slotIndex++)
            {
                // Base 5x4 indices never shift when the additional columns unlock.
                var column = InventorySystem.BagColumn(slotIndex);
                var row = InventorySystem.BagRow(slotIndex);
                var cell = new Rect(panel.x + 30 + column * 78, panel.y + 164 + row * 78, 68, 68);
                if (slotIndex >= game.Inventory.UnlockedCapacity)
                {
                    var previousColor = GUI.color;
                    GUI.color = new Color(.6f, .6f, .6f, 1f);
                    DrawInventorySlot(cell, false);
                    GUI.color = previousColor;
                    GUI.DrawTexture(new Rect(cell.center.x - 16, cell.center.y - 16, 32, 32),
                        IndividualArt.Texture("Meadow/UI/InventoryLock"), ScaleMode.ScaleToFit, true);
                    continue;
                }
                Texture2D icon = null;
                inventoryCells[slotIndex] = cell;
                var entry = game.Inventory.GetCell(slotIndex);
                var key = "B:" + slotIndex;
                var itemIndex = entry == null ? -1 : entry.ItemIndex;
                if (entry != null && entry.Kind == InventoryEntryKind.Weapon)
                {
                    var weapon = weapons[itemIndex];
                    icon = InventoryIconFactory.Get(weapon.Id, weapon.Color, InventoryIconKind.Weapon);
                }
                else if (entry != null && entry.Kind == InventoryEntryKind.Accessory)
                {
                    var item = accessories[itemIndex];
                    icon = InventoryIconFactory.Get(item.Id, item.Color, InventoryIconKind.Accessory);
                }

                var hovered = cell.Contains(Event.current.mousePosition);
                DrawInventorySlot(cell, hovered || game.InventoryFocusQuickSlot < 0 && slotIndex == game.InventoryFocusCell);
                if (hovered && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    game.FocusInventoryCell(slotIndex);
                if (icon == null)
                    continue;

                ItemIconLibrary.DrawGUI(new Rect(cell.x + 3, cell.y + 3, cell.width - 6, cell.height - 6), icon);
                DrawUpgradeBadge(cell, game.Inventory.GetOwnedCell(slotIndex));
                if (hovered)
                    hoverKey = key;
                TryBeginInventoryDrag(cell, key, icon);
            }

            if (GUI.Button(new Rect(panel.x + 650, panel.y + 12, 154, 36), "장비", inventorySetTab ? buttonStyle : selectedTabStyle))
                inventorySetTab = false;
            if (GUI.Button(new Rect(panel.x + 816, panel.y + 12, 154, 36), "세트", inventorySetTab ? selectedTabStyle : buttonStyle))
                inventorySetTab = true;
            DrawGold(panel.x + 650, panel.y + 50, detailStyle);
            var equipmentRect = new Rect(panel.x + 650, panel.y + 80, 320, 410);
            if (inventorySetTab)
                DrawActiveSetEffects(equipmentRect);
            else
                DrawEquipmentMaintenance(equipmentRect);
            if (!string.IsNullOrEmpty(hoverKey))
                DrawItemTooltip(width, height, hoverKey);
            ProcessInventoryDrag(inventoryCells, quickSlotRects);
        }

        private void ClearViewportMargins()
        {
            if (!game.IsRunActive || Event.current.type != EventType.Repaint || game.CameraFollow == null)
                return;
            var camera = game.CameraFollow.GetComponent<Camera>();
            if (camera == null)
                return;
            var viewport = camera.pixelRect;
            // PixelPerfectCamera clears its cropped world viewport. IMGUI may draw a
            // tooltip into the bars; clear those margins before this frame's UI so a
            // closed/moved tooltip cannot leave a fragment outside the world image.
            var matrix = GUI.matrix;
            var color = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.black;
            if (viewport.y > 0)
                GUI.DrawTexture(new Rect(0, Screen.height - viewport.y, Screen.width, viewport.y), Texture2D.whiteTexture);
            if (viewport.yMax < Screen.height)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height - viewport.yMax), Texture2D.whiteTexture);
            if (viewport.x > 0)
                GUI.DrawTexture(new Rect(0, 0, viewport.x, Screen.height), Texture2D.whiteTexture);
            if (viewport.xMax < Screen.width)
                GUI.DrawTexture(new Rect(viewport.xMax, 0, Screen.width - viewport.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.matrix = matrix;
        }

        private void DrawUpgradeBadge(Rect rect, OwnedEquipment item)
        {
            if (!(item is OwnedWeapon w) || w.Mutation == EquipmentMutation.None && w.Oath == EquipmentOath.None)
                return;
            // Both badges are in Galmuri; missing glyphs can load OS fallback fonts in Player builds.
            GUI.Label(new Rect(rect.xMax - 24, rect.yMax - 24, 20, 20), w.Awakened ? "★" : "◆", compactCenter ?? centerStyle);
        }

        private OwnedEquipment lastEquipmentDescription;
        private void DrawEquipmentMaintenance(Rect rect)
        {
            PrepareTownStyles();
            GUI.Box(rect, "", slotStyle);
            var item = game.SelectedEquipment;
            if (item == null)
            {
                return;
            }
            if (lastEquipmentDescription != item)
            {
                equipmentScroll = Vector2.zero;
                lastEquipmentDescription = item;
            }
            string id = item is OwnedWeapon w ? w.Definition.Id : ((OwnedAccessory)item).Definition.Id;
            DrawContentIcon(new Rect(rect.x + 12, rect.y + 12, 72, 72), id, item is OwnedWeapon ? "weapon" : "accessory");
            GUI.Label(new Rect(rect.x + 96, rect.y + 20, rect.width - 112, 60), WrapPanelText(item.Name, rect.width - 112), compactStyle);
            string description = item is OwnedWeapon ? ContentDescription(id, "weapon") : EquipmentUpgradeRules.AccessoryDescription((OwnedAccessory)item, game.Inventory);
            if (item is OwnedWeapon weapon)
                description = (description + "\n" + EquipmentUpgradeRules.WeaponUpgradeText(weapon)).TrimEnd();
            var viewport = new Rect(rect.x + 20, rect.y + 94, rect.width - 40, 226);
            description = WrapPanelText(description, viewport.width - 20);
            float textHeight = compactStyle.CalcHeight(new GUIContent(description), viewport.width - 20);
            equipmentScroll = GUI.BeginScrollView(viewport, equipmentScroll, new Rect(0, 0, viewport.width - 20, textHeight));
            GUI.Label(new Rect(0, 0, viewport.width - 20, textHeight), description, compactStyle);
            GUI.EndScrollView();
            if (game.HasDisassemblyConfirmation)
            {
                if (GUI.Button(new Rect(rect.x + 20, rect.y + 361, 132, 32), "버리기 확정", compactButton))
                    game.ConfirmEquipmentDisassembly();
                if (GUI.Button(new Rect(rect.x + 164, rect.y + 361, 132, 32), "취소", compactButton))
                    game.CancelEquipmentDisassembly();
            }
            else
            {
                bool old = GUI.enabled;
                GUI.enabled = old && game.CanManageEquipment && game.Inventory.CanDisassemble(item.Identity);
                if (GUI.Button(new Rect(rect.x + 20, rect.y + 350, rect.width - 40, 38), "장비 버리기", compactButton))
                    game.RequestEquipmentDisassembly(item.Identity, item.UpgradeLevel);
                GUI.enabled = old;
            }
        }

        private void DrawInventorySlot(Rect rect, bool selected)
        {
            var style = selected ? selectedSlotStyle : slotStyle;
            if (style == null || style.normal.background == null)
            {
                var oldColor = GUI.color;
                GUI.color = selected ? new Color(1f, .82f, .25f) : new Color(.3f, .33f, .42f, .8f);
                GUI.Box(rect, string.Empty);
                GUI.color = oldColor;
                return;
            }

            GUI.Box(rect, string.Empty, style);
        }

        private void DrawWideInventorySlot(Rect rect, bool selected)
        {
            GUI.Box(rect, string.Empty, selected ? selectedSlotStyle : slotStyle);
        }

        private void TryBeginInventoryDrag(Rect rect, string key, Texture2D icon)
        {
            var current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition))
                return;
            inventoryDragKey = key;
            inventoryDragIcon = icon;
            inventoryDragStart = current.mousePosition;
            inventoryDragMoved = false;
            current.Use();
        }

        private void ProcessInventoryDrag(Rect[] inventoryCells, Rect[] quickSlotRects)
        {
            if (string.IsNullOrEmpty(inventoryDragKey))
                return;
            var current = Event.current;
            if (current.type == EventType.MouseDrag)
            {
                inventoryDragMoved |= Vector2.Distance(inventoryDragStart, current.mousePosition) > 5f;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 0)
            {
                if (inventoryDragMoved)
                {
                    var targetQuickSlot = -1;
                    for (var index = 0; index < quickSlotRects.Length; index++)
                        if (quickSlotRects[index].Contains(current.mousePosition))
                            targetQuickSlot = index;
                    var targetCell = -1;
                    for (var index = 0; index < inventoryCells.Length; index++)
                        if (inventoryCells[index].Contains(current.mousePosition))
                            targetCell = index;

                    var parts = inventoryDragKey.Split(':');
                    int sourceIndex;
                    if (parts.Length == 2 && int.TryParse(parts[1], out sourceIndex))
                    {
                        var sourceCell = parts[0] == "B" ? game.Inventory.GetCell(sourceIndex) : null;
                        if (sourceCell != null && targetQuickSlot >= 0 && sourceCell.Kind == InventoryEntryKind.Weapon)
                            game.AssignWeaponToSlot(sourceCell.ItemIndex, targetQuickSlot);
                        else if (sourceCell != null && targetCell >= 0)
                            game.Inventory.MoveBagItem(sourceIndex, targetCell);
                        else if (parts[0] == "Q" && targetQuickSlot >= 0 && targetQuickSlot != sourceIndex)
                            game.SwapWeaponSlots(sourceIndex, targetQuickSlot);
                        else if (parts[0] == "Q" && targetCell >= 0)
                            game.UnequipWeaponSlot(sourceIndex, targetCell);
                    }
                }

                ClearInventoryDrag();
                current.Use();
            }
            else if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                ClearInventoryDrag();
                current.Use();
            }

            if (Event.current.type != EventType.Repaint || inventoryDragIcon == null)
                return;
            var mouse = Event.current.mousePosition;
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .9f);
            ItemIconLibrary.DrawGUI(new Rect(mouse.x - 28f, mouse.y - 28f, 56f, 56f), inventoryDragIcon);
            GUI.color = oldColor;
        }

        private void ClearInventoryDrag()
        {
            inventoryDragKey = string.Empty;
            inventoryDragIcon = null;
            inventoryDragMoved = false;
        }

        private void DrawItemTooltip(float width, float height, string key)
        {
            if (key.StartsWith("B:") && int.TryParse(key.Substring(2), out var cellIndex))
            {
                var cell = game.Inventory.GetCell(cellIndex);
                if (cell == null)
                    return;
                key = cell.TooltipKey;
            }
            var parts = key.Split(':');
            int index;
            if (parts.Length != 2 || !int.TryParse(parts[1], out index))
                return;

            string title;
            string category;
            string description;
            ItemSetType setType;
            Color rarityColor;
            Texture2D icon;
            if ((parts[0] == "W" && index >= 0 && index < game.Inventory.StoredWeapons.Count) ||
                (parts[0] == "Q" && index >= 0 && index < game.Inventory.QuickSlots.Count && game.Inventory.QuickSlots[index].Weapon != null))
            {
                var weapon = parts[0] == "Q" ? game.Inventory.QuickSlots[index].Weapon : game.Inventory.StoredWeapons[index];
                var owned = parts[0] == "Q" ? game.Inventory.QuickSlots[index].Item : game.Inventory.OwnedWeapons[index];
                title = weapon.Name;
                category = weapon.Rarity + " · " + WeaponFamilyCatalog.GetName(weapon.Family);
                description = (ContentDescription(weapon.Id, "weapon") + "\n" + EquipmentUpgradeRules.WeaponUpgradeText(owned)).TrimEnd();
                setType = weapon.SetType;
                rarityColor = weapon.Color;
                icon = InventoryIconFactory.Get(weapon.Id, weapon.Color, InventoryIconKind.Weapon);
            }
            else if ((parts[0] == "A" && index >= 0 && index < game.Inventory.Accessories.Count) ||
                     (parts[0] == "C" && index >= 0 && index < game.CurrentAccessoryChoices.Length))
            {
                var item = parts[0] == "C" ? game.CurrentAccessoryChoices[index] : game.Inventory.Accessories[index];
                var owned = parts[0] == "A" ? game.Inventory.OwnedAccessories[index] : null;
                title = owned == null ? item.Name : owned.DisplayName;
                category = item.Rarity + " · 장신구";
                description = owned == null ? EquipmentUpgradeRules.AccessoryDescription(item, 0, game.Inventory) : EquipmentUpgradeRules.AccessoryDescription(owned, game.Inventory);
                if (parts[0] == "C" && game.Inventory.StoredItemCount >= game.Inventory.UnlockedCapacity)
                    description += "\n가방이 가득 찼습니다.";
                setType = item.SetType;
                rarityColor = item.Color;
                icon = InventoryIconFactory.Get(item.Id, item.Color, InventoryIconKind.Accessory);
            }
            else
                return;

            float tooltipWidth = Mathf.Min(440f, width - 16f);
            float contentWidth = tooltipWidth - 66f;
            var descriptionHeight = detailStyle.CalcHeight(new GUIContent(description), contentWidth);
            var setText = setType == ItemSetType.None ? "" : GetSetProgressText(setType);
            var setHeight = setText.Length == 0 ? 0 : detailStyle.CalcHeight(new GUIContent(setText), contentWidth);
            var titleHeight = Mathf.Max(32, detailStyle.CalcHeight(new GUIContent(title), tooltipWidth - 122));
            var categoryHeight = detailStyle.CalcHeight(new GUIContent(category), tooltipWidth - 122);
            var layout = new ItemTooltipLayout(width, height, Event.current.mousePosition, titleHeight, categoryHeight, descriptionHeight, setHeight);
            if (tooltipScrollKey != key)
            {
                tooltipScrollKey = key;
                tooltipScroll = Vector2.zero;
            }
            var rect = layout.Panel;
            var oldColor = GUI.color;
            GUI.color = Color.white;
            GUI.Box(rect, string.Empty, slotStyle);
            GUI.color = oldColor;
            ItemIconLibrary.DrawGUI(layout.Icon, icon);
            GUI.color = rarityColor;
            GUI.Label(layout.Title, title, detailStyle);
            GUI.color = oldColor;
            GUI.Label(layout.Category, category, detailStyle);
            if (layout.NeedsScroll && Event.current.type == EventType.ScrollWheel)
            {
                tooltipScroll.y = Mathf.Clamp(tooltipScroll.y + Event.current.delta.y * 24, 0, Mathf.Max(0, layout.ContentHeight - layout.Viewport.height));
                Event.current.Use();
            }
            tooltipScroll = GUI.BeginScrollView(layout.Viewport, tooltipScroll, new Rect(0, 0, layout.ContentWidth, layout.ContentHeight));
            GUI.Label(new Rect(0, 0, layout.ContentWidth, descriptionHeight), description, detailStyle);
            GUI.Label(new Rect(0, layout.SetTop, layout.ContentWidth, setHeight), setText, detailStyle);
            GUI.EndScrollView();
        }

        private string GetSetProgressText(ItemSetType setType)
        {
            if (setType == ItemSetType.None)
                return "세트 없음";

            var count = Mathf.Min(3, game.Inventory.GetSetPieceCount(setType));
            var text = ItemSetCatalog.GetName(setType) + "  " + count + "/3";
            foreach (var bonus in ItemSetCatalog.Bonuses)
                if (bonus.SetType == setType)
                    text += "\n" + (count >= bonus.RequiredPieces ? "● " : "○ ") + bonus.RequiredPieces + "세트: " + bonus.Description;
            return text;
        }

        private void DrawActiveSetEffects(Rect rect)
        {
            GUI.Box(rect, string.Empty, slotStyle);
            GUI.Label(new Rect(rect.x + 24, rect.y + 36, rect.width - 48, 36), "활성 세트 효과", bodyStyle);
            var text = string.Empty;
            var activeCount = 0;
            foreach (ItemSetType setType in new[] { ItemSetType.BloodMoon, ItemSetType.Storm, ItemSetType.Guardian, ItemSetType.Arcanist })
            {
                var count = Mathf.Min(3, game.Inventory.GetSetPieceCount(setType));
                if (count < 2)
                    continue;
                if (activeCount > 0)
                    text += "\n\n";
                text += "● " + ItemSetCatalog.GetName(setType) + " " + count + "/3";
                foreach (var bonus in ItemSetCatalog.Bonuses)
                    if (bonus.SetType == setType && count >= bonus.RequiredPieces)
                        text += "\n   " + bonus.Description;
                activeCount++;
            }
            if (activeCount == 0)
                text = "활성 효과 없음";
            var viewport = new Rect(rect.x + 24, rect.y + 82, rect.width - 48, rect.height - 126);
            var contentWidth = viewport.width - 18;
            var contentHeight = detailStyle.CalcHeight(new GUIContent(text), contentWidth);
            activeSetScroll = GUI.BeginScrollView(viewport, activeSetScroll, new Rect(0, 0, contentWidth, contentHeight));
            GUI.Label(new Rect(0, 0, contentWidth, contentHeight), text, detailStyle);
            GUI.EndScrollView();
        }

        private void DrawStats(float width, float height)
        {
            if (game.Player == null || game.Player.Stats == null)
                return;
            var stats = game.Player.Stats;
            DrawPanel(new Rect(width / 2f - 330, height / 2f - 265, 660, 530));
            GUI.Label(new Rect(width / 2f - 275, height / 2f - 240, 550, 45), "CHARACTER STATS", titleStyle);

            var left = width / 2f - 270;
            var top = height / 2f - 165;
            GUI.Label(new Rect(left, top, 280, 34), "체력", bodyStyle);
            GUI.Label(new Rect(left + 300, top, 240, 34), stats.Health.ToString("0") + " / " + stats.MaxHealth.ToString("0"), bodyStyle);
            GUI.Label(new Rect(left, top + 38, 280, 34), "마나", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 38, 240, 34), stats.Mana.ToString("0") + " / " + stats.MaxMana.ToString("0"), bodyStyle);
            GUI.Label(new Rect(left, top + 76, 280, 34), "장착 무기 공격력", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 76, 240, 34), game.Player.Damage.ToString("0.0"), bodyStyle);
            GUI.Label(new Rect(left, top + 114, 280, 34), "치명타 확률 / 피해", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 114, 240, 34), (stats.CriticalChance * 100f).ToString("0") + "% / " + (stats.CriticalDamage * 100f).ToString("0") + "%", bodyStyle);
            GUI.Label(new Rect(left, top + 152, 280, 34), "방어력", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 152, 240, 34), stats.Armor.ToString("0.0"), bodyStyle);
            GUI.Label(new Rect(left, top + 190, 280, 34), "운", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 190, 240, 34), stats.Luck.ToString("0.0") + "  (추가 무기 " + (WeaponLootTable.GetDoubleDropChance(stats.Luck) * 100f).ToString("0") + "%)", bodyStyle);
            GUI.Label(new Rect(left, top + 228, 280, 34), "이동 속도", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 228, 240, 34), stats.MoveSpeed.ToString("0.00"), bodyStyle);
            GUI.Label(new Rect(left, top + 266, 280, 34), "마나 재생 / 무기 스킬", bodyStyle);
            GUI.Label(new Rect(left + 300, top + 266, 240, 34), stats.ManaRegeneration.ToString("0.0") + "/s  / x" + stats.SpecialDamageMultiplier.ToString("0.00"), bodyStyle);

            var weapon = game.Player.EquippedWeapon;
            var weaponText = weapon == null
                ? "무기 없음"
                : weapon.Rarity + " " + weapon.Name + "\n" + Key(GameAction.Guard) + ": " + WeaponActionDescription(weapon, game.Player.WeaponUpgradeLevel).Split('\n')[0];
            GUI.Label(new Rect(left, top + 310, 540, 72), weaponText, detailStyle);
        }

        float resultOpenedAt = -1f;
        bool resultRewardShown;
    }
}
