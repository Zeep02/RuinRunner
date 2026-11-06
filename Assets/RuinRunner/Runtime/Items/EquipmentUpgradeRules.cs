using UnityEngine;

namespace RuinRunner
{
    public static class EquipmentUpgradeRules
    {
        static string Number(float value) => value.ToString("0.##");
        public static string Next(OwnedEquipment item)
        {
            if(item==null)return "장비를 선택하세요.";
            if(item is OwnedWeapon w)return WeaponUpgradeText(w);
            return AccessoryDescription((OwnedAccessory)item,null);
        }
        public static string WeaponUpgradeText(OwnedWeapon item)
        {
            if(item==null)return "";
            string result=item.Mutation==EquipmentMutation.None?"":EquipmentMutationRules.Summary(item.Mutation);
            if(item.Oath!=EquipmentOath.None)result+=(result.Length>0?"\n":"")+EquipmentMutationRules.OathName(item.Oath)+" · "+(item.Awakened?"각성":item.OathProgress+"/"+EquipmentMutationRules.Required(item.Oath))+"\n"+EquipmentMutationRules.OathResult(item.Oath);
            return result;
        }
        public static string AccessoryDescription(OwnedAccessory item,InventorySystem bag)=>AccessoryDescription(item.Definition,0,bag);
        public static string AccessoryDescription(AccessoryDefinition d,int ignoredLegacyLevel,InventorySystem bag)
            =>d.Description;
    }
}
