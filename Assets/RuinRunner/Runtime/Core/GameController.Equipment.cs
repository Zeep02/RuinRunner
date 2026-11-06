using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameController
    {
        long equipmentSelection;
        long disassemblyIdentity;
        int disassemblyLevel, disassemblyRevision, equipmentOperationFrame = -1;
        float equipmentOperationUntil;
        public string EquipmentMessage { get; private set; } = "";
        public OwnedEquipment SelectedEquipment => Inventory.FindOwned(equipmentSelection);
        public bool CanManageEquipment => IsRunActive && IsInventoryOpen && !IsGameOver && Player != null && !Player.IsDead && !IsStageTransitioning && !IsChoosingAccessory && !EconomyOperationPending && !BackendHasPending;
        public bool EquipmentOperationReady => equipmentOperationFrame != Time.frameCount && Time.unscaledTime >= equipmentOperationUntil;
        public bool HasDisassemblyConfirmation => CanManageEquipment && disassemblyIdentity != 0 &&
            equipmentSelection == disassemblyIdentity && Inventory.Revision == disassemblyRevision &&
            SelectedEquipment != null && SelectedEquipment.UpgradeLevel == disassemblyLevel;

        void ResetEquipmentSelection()
        {
            equipmentSelection = 0; InventoryFocusQuickSlot = -1; CancelEquipmentDisassembly(); EquipmentMessage = ""; equipmentOperationFrame = -1; equipmentOperationUntil = 0;
        }
        public void FocusEquipment(long identity)
        {
            if (!CanManageEquipment) return;
            if (equipmentSelection != identity) { CancelEquipmentDisassembly(); EquipmentMessage = ""; }
            equipmentSelection = Inventory.FindOwned(identity) != null ? identity : 0;
        }
        public void CancelEquipmentDisassembly() { disassemblyIdentity = 0; }
        bool ReadEquipmentMaintenanceInput()
        {
            if (!CanManageEquipment) return false;
            if (HasDisassemblyConfirmation)
            {
                if (Settings.GetKeyDown(GameAction.Interact)) ConfirmEquipmentDisassembly();
                // A repeated request or upgrade key cannot confirm deletion.
                return true;
            }
            var item = SelectedEquipment;
            if (item == null) return false;
            if (Settings.GetKeyDown(GameAction.Dismantle))
            { RequestEquipmentDisassembly(item.Identity, item.UpgradeLevel); return true; }
            return false;
        }

        public bool RequestEquipmentDisassembly(long identity, int expectedLevel)
        {
            if (!CanManageEquipment || !EquipmentOperationReady || equipmentSelection != identity) return false;
            var item = Inventory.FindOwned(identity);
            if (item == null || item.UpgradeLevel != expectedLevel || !Inventory.CanDisassemble(identity)) return false;
            disassemblyIdentity = identity; disassemblyLevel = expectedLevel; disassemblyRevision = Inventory.Revision;
            return true;
        }
        public bool ConfirmEquipmentDisassembly()
        {
            if (!HasDisassemblyConfirmation || !EquipmentOperationReady) { CancelEquipmentDisassembly(); return false; }
            var name = SelectedEquipment.DisplayName;
            var item = SelectedEquipment;
            CancelEquipmentDisassembly();
            DiscardConfirmed(item);
            equipmentOperationFrame = Time.frameCount; equipmentOperationUntil = Time.unscaledTime + .28f; equipmentSelection = 0;
            EquipmentMessage = "버리기 요청";
            PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .5f);
            return true;
        }
        void AwardEncounterMaterials(int room)
        {
            if(!legacyFixture)_ = ClaimGold("combat:"+CurrentStageIndex+":"+room,"combat");
            if(Player!=null)DropGold(Player.transform.position,BackendCatalog?.economy?.combatGold??0);

        }
    }
}
