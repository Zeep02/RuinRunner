using UnityEngine;

namespace RuinRunner
{
    /// <summary>Accessory choice, inventory focus and weapon slots.</summary>
    public sealed partial class GameController
    {
        public void SelectAccessory(AccessoryDefinition definition)
        {
            if (!IsChoosingAccessory || Player == null || Progression == null || definition == null)
                return;
            if (Time.frameCount == accessorySelectionFrame || System.Array.IndexOf(CurrentAccessoryChoices, definition) < 0)
                return;
            if (!Progression.SpendLevelReward())
                return;
            accessorySelectionFrame = Time.frameCount;
            // A full bag leaves the reward on the floor instead of losing it.
            if (!legacyFixture || !CollectAccessoryPickup(definition))
                SpawnEquipmentPickup(Player.transform.position, null, definition, 0, IsStageClearPending, "level-choice:" + accessoryOfferIndex);
            GameAudio.Play(GameSound.Inventory, AudioChannel.Inventory, .55f);
            IsChoosingAccessory = Progression.PendingLevelRewards > 0;
            CurrentAccessoryChoices = IsChoosingAccessory
                ? RunContent.Choices(RunSeed, accessoryOfferIndex++, Player.Stats.Luck)
                : new AccessoryDefinition[0];
            RefreshTimeScale();
        }

        private int accessorySelectionFrame = -1;
        private int accessoryOfferIndex;

        public void FocusInventoryCell(int cell)
        {
            if (IsInventoryOpen && cell >= 0 && cell < Inventory.UnlockedCapacity)
            {
                InventoryFocusQuickSlot = -1;
                InventoryFocusCell = cell;
                FocusEquipment(Inventory.GetOwnedCell(cell)?.Identity ?? 0);
            }
        }

        public void FocusInventoryQuickSlot(int slot)
        {
            if (!IsInventoryOpen || slot < 0 || slot >= Inventory.QuickSlots.Count)
                return;
            InventoryFocusQuickSlot = slot;
            FocusEquipment(Inventory.QuickSlots[slot].Item?.Identity ?? 0);
        }

        private void ReadInventoryInput()
        {
            if (InventoryFocusQuickSlot >= 0)
            {
                var slot = InventoryFocusQuickSlot;
                if (Settings.GetKeyDown(GameAction.MoveLeft))
                    FocusInventoryQuickSlot(Mathf.Max(0, slot - 1));
                else if (Settings.GetKeyDown(GameAction.MoveRight))
                    FocusInventoryQuickSlot(Mathf.Min(Inventory.QuickSlots.Count - 1, slot + 1));
                else if (Settings.GetKeyDown(GameAction.MoveDown))
                    FocusInventoryCell(Inventory.BagCell(Mathf.Min(Inventory.UnlockedCapacity / 4 - 1, Mathf.RoundToInt((95 + slot * 205 - 34) / 78f)), 0));
                ReadEquipmentMaintenanceInput();
                return;
            }
            var column = InventorySystem.BagColumn(InventoryFocusCell);
            var row = InventorySystem.BagRow(InventoryFocusCell);
            if (Settings.GetKeyDown(GameAction.MoveLeft))
                column--;
            else if (Settings.GetKeyDown(GameAction.MoveRight))
                column++;
            else if (Settings.GetKeyDown(GameAction.MoveUp))
            {
                if (row == 0)
                {
                    FocusInventoryQuickSlot(Mathf.Clamp(Mathf.FloorToInt((34 + column * 78) / 205f), 0, 2));
                    return;
                }
                row--;
            }
            else if (Settings.GetKeyDown(GameAction.MoveDown))
                row++;
            if (column != InventorySystem.BagColumn(InventoryFocusCell) || row != InventorySystem.BagRow(InventoryFocusCell))
                FocusInventoryCell(Inventory.BagCell(column, row));
            if (ReadEquipmentMaintenanceInput())
                return;
            var entry = Inventory.GetCell(InventoryFocusCell);
            if (entry == null || entry.Kind != InventoryEntryKind.Weapon)
                return;
            for (var slot = 0; slot < Inventory.QuickSlots.Count; slot++)
                if (Settings.GetKeyDown(slot == 0 ? GameAction.Weapon1 : slot == 1 ? GameAction.Weapon2 : GameAction.Weapon3))
                {
                    AssignWeaponToSlot(entry.ItemIndex, slot);
                    break;
                }
        }

        public void AssignWeaponToSlot(int weaponIndex, int slotIndex)
        {
            if (EconomyOperationPending || BackendHasPending)
                return;
            var weapon = Inventory.AssignToQuickSlot(weaponIndex, slotIndex);
            Player.EquipWeapon(weapon);
            if (weapon != null)
            {
                GameAudio.Play(GameSound.Inventory, AudioChannel.Inventory, .5f);
                LootMessage = (slotIndex + 1) + "번 슬롯 장착 — " + weapon.Name;
                LootMessageUntil = Time.unscaledTime + 2f;
            }
        }

        public void UnequipWeaponSlot(int slotIndex, int targetCell = -1)
        {
            if (EconomyOperationPending || BackendHasPending)
                return;
            var previous = slotIndex >= 0 && slotIndex < Inventory.QuickSlots.Count
                ? Inventory.QuickSlots[slotIndex].Weapon
                : null;
            if (previous == null)
                return;

            var weapon = Inventory.UnequipQuickSlot(slotIndex, targetCell);
            if (Inventory.QuickSlots[slotIndex].Weapon == previous)
                return;
            Player.EquipWeapon(weapon);
            GameAudio.Play(GameSound.Inventory, AudioChannel.Inventory, .5f);
            LootMessage = previous.Name + " 장착 해제";
            LootMessageUntil = Time.unscaledTime + 1.5f;
        }

        public void SwapWeaponSlots(int firstSlotIndex, int secondSlotIndex)
        {
            if (EconomyOperationPending || BackendHasPending)
                return;
            if (firstSlotIndex == secondSlotIndex)
                return;
            Inventory.SwapQuickSlots(firstSlotIndex, secondSlotIndex);
            Player.EquipWeapon(Inventory.EquippedWeapon);
            GameAudio.Play(GameSound.Inventory, AudioChannel.Inventory, .45f);
            LootMessage = (firstSlotIndex + 1) + "번과 " + (secondSlotIndex + 1) + "번 슬롯 교체";
            LootMessageUntil = Time.unscaledTime + 1.5f;
        }

        private void SelectWeaponSlot(int slotIndex)
        {
            var previous = Inventory.ActiveSlotIndex;
            var weapon = Inventory.SelectQuickSlot(slotIndex);
            Player.EquipWeapon(weapon);
            if (weapon != null && previous != Inventory.ActiveSlotIndex)
            {
                GameAudio.Play(GameSound.Inventory, AudioChannel.Inventory, .4f);
                LootMessage = (Inventory.ActiveSlotIndex + 1) + "번 무기 — " + weapon.Name;
                LootMessageUntil = Time.unscaledTime + 1.2f;
            }
        }

        private AccessoryDefinition GetAccessoryChoice(int index)
        {
            return index >= 0 && index < CurrentAccessoryChoices.Length ? CurrentAccessoryChoices[index] : null;
        }
    }
}
