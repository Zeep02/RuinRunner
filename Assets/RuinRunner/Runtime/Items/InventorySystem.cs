using System;
using System.Collections.Generic;

namespace RuinRunner
{
    public enum InventoryEntryKind { Weapon, Accessory }
    public enum InventoryChangeKind { Acquired, Removed, Upgraded, Reset, Balance }
    public sealed class InventoryCell
    {
        public InventoryEntryKind Kind { get; }
        public int ItemIndex { get; internal set; }
        internal InventoryCell(InventoryEntryKind kind, int index)
        {
            Kind = kind;
            ItemIndex = index;
        }
        public string TooltipKey => (Kind == InventoryEntryKind.Weapon ? "W:" : "A:") + ItemIndex;
    }

    public sealed partial class InventorySystem
    {
        readonly InventoryCell[] cells = new InventoryCell[MaximumCapacity];
        readonly List<OwnedWeapon> ownedWeapons = new List<OwnedWeapon>();
        readonly List<OwnedAccessory> ownedAccessories = new List<OwnedAccessory>();
        // Compatibility views contain definitions only. Mutable copy state lives exclusively in owned items.
        readonly List<WeaponDefinition> storedWeapons = new List<WeaponDefinition>();
        readonly List<AccessoryDefinition> accessories = new List<AccessoryDefinition>();
        readonly Dictionary<ItemSetType, int> setPieceCounts = new Dictionary<ItemSetType, int>();
        readonly HashSet<string> collectedSetPieces = new HashSet<string>();
        readonly HashSet<string> activatedSetBonuses = new HashSet<string>();
        readonly HashSet<string> materialAwards = new HashSet<string>();
        readonly WeaponSlotState[] quickSlots = { new WeaponSlotState(), new WeaponSlotState(), new WeaponSlotState() };
        public event Action<InventoryChangeKind> Changed;
        public event Action<OwnedWeapon, bool> ReloadCompleted;
        public const int Capacity = 20;
        public const int MaximumCapacity = 32;
        public int ExpansionRank { get; private set; }
        public int UnlockedCapacity => Capacity + ExpansionRank * 4;
        public int UpgradeMaterials { get; private set; }
        public int Revision { get; private set; }
        public int StoredItemCount => ownedWeapons.Count + ownedAccessories.Count;
        public IReadOnlyList<WeaponDefinition> StoredWeapons => storedWeapons;
        public IReadOnlyList<AccessoryDefinition> Accessories => accessories;
        public IReadOnlyList<OwnedWeapon> OwnedWeapons => ownedWeapons;
        public IReadOnlyList<OwnedAccessory> OwnedAccessories => ownedAccessories;
        public IReadOnlyList<WeaponSlotState> QuickSlots => quickSlots;
        public int ActiveSlotIndex { get; private set; }
        public WeaponSlotState ActiveSlot => quickSlots[ActiveSlotIndex];
        public WeaponDefinition EquippedWeapon => ActiveSlot.Weapon;
        public static int BagColumn(int cell) => cell < Capacity ? cell % 5 : 5 + (cell - Capacity) / 4;
        public static int BagRow(int cell) => cell < Capacity ? cell / 5 : (cell - Capacity) % 4;
        public int BagCell(int column, int row)
        {
            if (column < 0 || column >= 5 + ExpansionRank || row < 0 || row >= 4)
                return -1;
            return column < 5 ? row * 5 + column : Capacity + (column - 5) * 4 + row;
        }
        public void ConfigureExpansion(int rank)
        {
            int next = Math.Max(0, Math.Min(3, rank));
            for (int i = Capacity + next * 4; i < MaximumCapacity; i++)
                if (cells[i] != null)
                    throw new InvalidOperationException("Cannot lock occupied inventory slots.");
            ExpansionRank = next;
        }
        public InventoryCell GetCell(int index) => index >= 0 && index < UnlockedCapacity ? cells[index] : null;
        public OwnedEquipment GetOwnedCell(int index)
        {
            var cell = GetCell(index);
            if (cell == null)
                return null;
            return cell.Kind == InventoryEntryKind.Weapon ? (OwnedEquipment)ownedWeapons[cell.ItemIndex] : ownedAccessories[cell.ItemIndex];
        }
        public OwnedEquipment FindOwned(long identity)
        {
            foreach (var slot in quickSlots)
                if (slot.Item != null && slot.Item.Identity == identity)
                    return slot.Item;
            foreach (var item in ownedWeapons)
                if (item.Identity == identity)
                    return item;
            foreach (var item in ownedAccessories)
                if (item.Identity == identity)
                    return item;
            return null;
        }
        public int GetAccessoryUpgradeLevel(AccessoryType type) => 0;
        int FindEmptyCell()
        {
            for (int i = 0; i < UnlockedCapacity; i++)
                if (cells[i] == null)
                    return i;
            return -1;
        }
        public bool MoveBagItem(int source, int destination)
        {
            if (source < 0 || destination < 0 || source >= UnlockedCapacity || destination >= UnlockedCapacity || cells[source] == null)
                return false;
            var item = cells[source];
            cells[source] = cells[destination];
            cells[destination] = item;
            Revision++;
            return true;
        }
        void ContentChanged(InventoryChangeKind kind)
        {
            Revision++;
            RebuildSets();
            foreach (var bonus in ItemSetCatalog.Bonuses)
                if (GetSetPieceCount(bonus.SetType) < bonus.RequiredPieces)
                    activatedSetBonuses.Remove(bonus.SetType + ":" + bonus.RequiredPieces);
            Changed?.Invoke(kind);
        }
        public bool AddWeapon(WeaponDefinition definition)
        {
            if (definition == null)
                return false;
            for (int i = 0; i < quickSlots.Length; i++)
                if (quickSlots[i].Item == null)
                {
                    quickSlots[i].AssignItem(new OwnedWeapon(definition));
                    if (ActiveSlot.Item == null)
                        ActiveSlotIndex = i;
                    ContentChanged(InventoryChangeKind.Acquired);
                    return true;
                }
            int cell = FindEmptyCell();
            if (cell < 0)
                return false;
            cells[cell] = new InventoryCell(InventoryEntryKind.Weapon, ownedWeapons.Count);
            var item = new OwnedWeapon(definition);
            ownedWeapons.Add(item);
            storedWeapons.Add(item.Weapon);
            ContentChanged(InventoryChangeKind.Acquired);
            return true;
        }
        void RemoveCell(InventoryEntryKind kind, int index)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell == null || cell.Kind != kind)
                    continue;
                if (cell.ItemIndex == index)
                    cells[i] = null;
                else if (cell.ItemIndex > index)
                    cell.ItemIndex--;
            }
        }
        OwnedWeapon TakeStoredWeapon(int index)
        {
            var item = ownedWeapons[index];
            ownedWeapons.RemoveAt(index);
            storedWeapons.RemoveAt(index);
            RemoveCell(InventoryEntryKind.Weapon, index);
            return item;
        }
        public WeaponDefinition AssignToQuickSlot(int index, int slotIndex)
        {
            if (index < 0 || index >= ownedWeapons.Count || slotIndex < 0 || slotIndex >= quickSlots.Length)
                return EquippedWeapon;
            var item = ownedWeapons[index];
            var previous = quickSlots[slotIndex].Item;
            if (previous == null)
                TakeStoredWeapon(index);
            else
            {
                ownedWeapons[index] = previous;
                storedWeapons[index] = previous.Weapon;
            }
            quickSlots[slotIndex].AssignItem(item);
            ActiveSlotIndex = slotIndex;
            Revision++;
            return EquippedWeapon;
        }
        public WeaponDefinition UnequipQuickSlot(int slotIndex, int targetCell = -1)
        {
            if (slotIndex < 0 || slotIndex >= quickSlots.Length || quickSlots[slotIndex].Item == null)
                return EquippedWeapon;
            if (targetCell == -1)
                targetCell = FindEmptyCell();
            if (targetCell < 0 || targetCell >= UnlockedCapacity)
                return EquippedWeapon;
            if (cells[targetCell] != null)
                return cells[targetCell].Kind == InventoryEntryKind.Weapon ? AssignToQuickSlot(cells[targetCell].ItemIndex, slotIndex) : EquippedWeapon;
            var item = quickSlots[slotIndex].Item;
            cells[targetCell] = new InventoryCell(InventoryEntryKind.Weapon, ownedWeapons.Count);
            ownedWeapons.Add(item);
            storedWeapons.Add(item.Weapon);
            quickSlots[slotIndex].AssignItem(null);
            if (ActiveSlotIndex == slotIndex)
                for (int i = 0; i < quickSlots.Length; i++)
                    if (quickSlots[i].Item != null)
                    {
                        ActiveSlotIndex = i;
                        break;
                    }
            Revision++;
            return EquippedWeapon;
        }
        public WeaponDefinition SwapQuickSlots(int first, int second)
        {
            if (first < 0 || second < 0 || first >= quickSlots.Length || second >= quickSlots.Length || first == second)
                return EquippedWeapon;
            quickSlots[first].SwapWith(quickSlots[second]);
            Revision++;
            return EquippedWeapon;
        }
        public WeaponDefinition SelectQuickSlot(int index)
        {
            if (index >= 0 && index < quickSlots.Length && quickSlots[index].Item != null)
                ActiveSlotIndex = index;
            return EquippedWeapon;
        }
        public bool TryConsumeAmmo() => ActiveSlot.TryConsumeAmmo();
        public bool StartReload() => ActiveSlot.StartReload();
        public void Tick(float deltaTime)
        {
            foreach (var slot in quickSlots)
            {
                bool loading = slot.IsReloading, empty = slot.Item?.ReloadStartedFromEmpty ?? false;
                var item = slot.Item;
                slot.Tick(deltaTime);
                if (loading && !slot.IsReloading)
                    ReloadCompleted?.Invoke(item, empty);
            }
        }
        public void RefreshWeaponBalance()
        {
            foreach (var slot in quickSlots)
                slot.RefreshBalance();
            for (int i = 0; i < ownedWeapons.Count; i++)
            {
                ownedWeapons[i].RefreshBalance();
                storedWeapons[i] = ownedWeapons[i].Weapon;
            }
            ContentChanged(InventoryChangeKind.Balance);
        }
        public bool AddPassiveItem(PassiveItemDefinition item) => item != null && AddAccessory(AccessoryLootTable.Find((AccessoryType)(8 + (int)item.Type)));
        public bool AddAccessory(AccessoryDefinition definition)
        {
            if (definition == null)
                return false;
            int cell = FindEmptyCell();
            if (cell < 0)
                return false;
            cells[cell] = new InventoryCell(InventoryEntryKind.Accessory, ownedAccessories.Count);
            ownedAccessories.Add(new OwnedAccessory(definition));
            accessories.Add(definition);
            ContentChanged(InventoryChangeKind.Acquired);
            return true;
        }
        void RebuildSets()
        {
            setPieceCounts.Clear();
            collectedSetPieces.Clear();
            foreach (var slot in quickSlots)
                if (slot.Item != null)
                    RegisterSetPiece("weapon:" + slot.Item.Definition.Id, slot.Item.Definition.SetType);
            foreach (var item in ownedWeapons)
                RegisterSetPiece("weapon:" + item.Definition.Id, item.Definition.SetType);
            foreach (var item in ownedAccessories)
                RegisterSetPiece("accessory:" + item.Definition.Id, item.Definition.SetType);
        }
        void RegisterSetPiece(string id, ItemSetType set)
        {
            if (set != ItemSetType.None && collectedSetPieces.Add(id))
                setPieceCounts[set] = GetSetPieceCount(set) + 1;
        }
        public int GetSetPieceCount(ItemSetType set) => setPieceCounts.TryGetValue(set, out var count) ? count : 0;
        public List<SetBonusDefinition> CollectNewSetBonuses()
        {
            var result = new List<SetBonusDefinition>();
            foreach (var bonus in ItemSetCatalog.Bonuses)
                if (GetSetPieceCount(bonus.SetType) >= bonus.RequiredPieces && activatedSetBonuses.Add(bonus.SetType + ":" + bonus.RequiredPieces))
                    result.Add(bonus);
            return result;
        }
        // Legacy calls remain source compatible but cannot create the rejected +n economy.
        public bool AwardMaterials(int amount, string rewardIdentity) => false;
        public bool TryUpgrade(long identity, int expectedLevel) => false;
        public bool CanDisassemble(long identity)
        {
            var item = FindOwned(identity);
            if (item == null)
                return false;
            if (item is OwnedAccessory)
                return true;
            int weapons = ownedWeapons.Count;
            foreach (var slot in quickSlots)
                if (slot.Item != null)
                    weapons++;
            return weapons > 1;
        }
        public bool TryDisassemble(long identity, int expectedLevel, out int materials)
        {
            materials = 0;
            var item = FindOwned(identity);
            if (item == null || item.UpgradeLevel != expectedLevel || !CanDisassemble(identity))
                return false;
            materials = 0;
            if (item is OwnedAccessory accessory)
            {
                int index = ownedAccessories.IndexOf(accessory);
                ownedAccessories.RemoveAt(index);
                accessories.RemoveAt(index);
                RemoveCell(InventoryEntryKind.Accessory, index);
            }
            else
            {
                var weapon = (OwnedWeapon)item;
                int index = ownedWeapons.IndexOf(weapon);
                if (index >= 0)
                    TakeStoredWeapon(index);
                else
                    foreach (var slot in quickSlots)
                        if (slot.Item == weapon)
                        {
                            slot.AssignItem(null);
                            break;
                        }
                if (ActiveSlot.Item == null)
                {
                    bool found = false;
                    for (int i = 0; i < quickSlots.Length; i++)
                        if (quickSlots[i].Item != null)
                        {
                            ActiveSlotIndex = i;
                            found = true;
                            break;
                        }
                    if (!found && ownedWeapons.Count > 0)
                        ActiveSlot.AssignItem(TakeStoredWeapon(0));
                }
            }
            ContentChanged(InventoryChangeKind.Removed);
            return true;
        }
        public void Reset()
        {
            Array.Clear(cells, 0, cells.Length);
            ownedWeapons.Clear();
            storedWeapons.Clear();
            ownedAccessories.Clear();
            accessories.Clear();
            setPieceCounts.Clear();
            collectedSetPieces.Clear();
            activatedSetBonuses.Clear();
            materialAwards.Clear();
            foreach (var slot in quickSlots)
                slot.AssignItem(null);
            ActiveSlotIndex = 0;
            ExpansionRank = 0;
            UpgradeMaterials = 0;
            ContentChanged(InventoryChangeKind.Reset);
        }
    }
}
