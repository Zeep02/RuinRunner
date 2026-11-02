using System.Collections.Generic;

namespace RuinRunner
{
    public sealed partial class InventorySystem
    {
        public IEnumerable<OwnedWeapon> AllWeapons()
        { foreach(var slot in quickSlots)if(slot.Item!=null)yield return slot.Item;foreach(var item in ownedWeapons)yield return item; }
        public IEnumerable<OwnedEquipment> AllEquipment()
        { foreach(var item in AllWeapons())yield return item;foreach(var item in ownedAccessories)yield return item; }
        public bool HasRoomFor(InventoryEntryKind kind)
        { if(kind==InventoryEntryKind.Weapon)foreach(var slot in quickSlots)if(slot.Item==null)return true;return FindEmptyCell()>=0; }
        public bool TryAddConfirmed(string contentId,InventoryEntryKind kind,string serverIdentity,bool starter=false)
        {
            if(!string.IsNullOrEmpty(serverIdentity))foreach(var item in AllEquipment())if(item.ServerInstanceId==serverIdentity)return true;
            if(!HasRoomFor(kind))return false;
            var before=new HashSet<long>();foreach(var item in AllEquipment())before.Add(item.Identity);
            bool added=kind==InventoryEntryKind.Weapon?AddWeapon(WeaponLootTable.Find(contentId)):AddAccessory(AccessoryLootTable.Find(contentId));
            if(!added)return false;
            foreach(var item in AllEquipment())if(!before.Contains(item.Identity)){item.ServerInstanceId=serverIdentity??"";item.IsStartingEquipment=starter;break;}
            return true;
        }
        public OwnedEquipment FindServerItem(string identity)
        { foreach(var item in AllEquipment())if(!string.IsNullOrEmpty(identity)&&item.ServerInstanceId==identity)return item;return null; }
        public bool TryRemoveConfirmed(long identity)
        { var item=FindOwned(identity);return item!=null&&TryDisassemble(identity,item.UpgradeLevel,out _); }
        public bool TryMutate(long identity,EquipmentMutation mutation)
        {
            var item=FindOwned(identity) as OwnedWeapon;if(!EquipmentMutationRules.Compatible(item,mutation))return false;
            item.Mutation=mutation;ContentChanged(InventoryChangeKind.Balance);return true;
        }
        public bool TrySwear(long identity,EquipmentOath oath)
        {
            foreach(var weapon in AllWeapons())if(weapon.Oath!=EquipmentOath.None&&!weapon.Awakened)return false;
            var item=FindOwned(identity) as OwnedWeapon;if(!EquipmentMutationRules.Compatible(item,oath,this))return false;
            item.Oath=oath;item.OathProgress=0;ContentChanged(InventoryChangeKind.Balance);return true;
        }
        public void NotifyAwakening()=>ContentChanged(InventoryChangeKind.Balance);
    }
}
