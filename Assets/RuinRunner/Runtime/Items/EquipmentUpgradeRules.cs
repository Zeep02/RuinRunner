using UnityEngine;

namespace RuinRunner
{
    public static class EquipmentUpgradeRules
    {
        static string Number(float value) => value.ToString("0.##");
        public static string AccessoryStats(AccessoryType type, int level)
        {
            float s = 1;
            switch (type)
            {
                case AccessoryType.LuckyDice: return "운 +" + Number(2 * s);
                case AccessoryType.StormFeather: return "이동 속도 +" + Number(.4f * s);
                case AccessoryType.GuardianSeal: return "방어력 +" + Number(5 * s);
                case AccessoryType.CrimsonRing: return "치명타 확률 +" + Number(5 * s) + "%";
                case AccessoryType.ManaPrism: return "최대 마나 +" + Number(14 * s);
                case AccessoryType.BulwarkBrooch: return "최대 체력 +" + Number(16 * s);
                case AccessoryType.BerserkerEmblem: return "공격력 +" + Number(10 * s) + "%";
                case AccessoryType.SageSigil: return "마나 재생 +" + Number(s) + ", 특수공격 피해 +" + Number(10 * s) + "%";
                case AccessoryType.RubyHeart: return "최대 체력 +" + Number(18 * s);
                case AccessoryType.SapphireFlask: return "최대 마나 +" + Number(18 * s);
                case AccessoryType.GuardianPlate: return "방어력 +" + Number(6 * s);
                case AccessoryType.WingedBoots: return "이동 속도 +" + Number(.55f * s);
                case AccessoryType.AssassinMark: return "치명타 확률 +" + Number(6 * s) + "%";
                case AccessoryType.ExecutionersCoin: return "치명타 피해 +" + Number(25 * s) + "%";
                case AccessoryType.FourLeafClover: return "운 +" + Number(3 * s);
                case AccessoryType.ArcaneLens: return "특수공격 피해 +" + Number(18 * s) + "%";
                case AccessoryType.VampireFang: return "처치 시 체력 " + Number(1.5f * s) + " 회복";
                case AccessoryType.SoulCapacitor: return "마나 재생 +" + Number(1.2f * s) + ", 처치 시 마나 +" + Number(2 * s);
                case AccessoryType.ManaThread: return "최대 마나 +"+Number(6*s);
                case AccessoryType.GuardBell: return "방어력 +"+Number(2*s);
                case AccessoryType.RushSpur: return "이동 속도 +"+Number(.15f*s);
                case AccessoryType.SteadyLocket: return "치명타 확률 +"+Number(2*s)+"%";
                case AccessoryType.PowderSatchel: return "특수공격 피해 +"+Number(4*s)+"%";
                case AccessoryType.LastLightPendant: return "최대 체력 +"+Number(8*s);
                default: return "";
            }
        }
        public static string WeaponMilestone(WeaponDefinition weapon)=>"";
        public static string AccessoryMilestone(AccessoryType type)=>"";
        public static string Next(OwnedEquipment item)
        {
            if(item==null)return "장비를 선택하세요.";
            if(item is OwnedWeapon w)return WeaponUpgradeText(w);
            return AccessoryDescription((OwnedAccessory)item,null);
        }
        public static string WeaponUpgradeText(OwnedWeapon item)
        {
            if(item==null)return "";
            string result=EquipmentMutationRules.Summary(item.Mutation);
            if(item.Oath!=EquipmentOath.None)result+="\n"+EquipmentMutationRules.OathName(item.Oath)+" · "+(item.Awakened?"각성":item.OathProgress+"/"+EquipmentMutationRules.Required(item.Oath))+"\n"+EquipmentMutationRules.OathResult(item.Oath);
            return result;
        }
        public static string AccessoryDescription(OwnedAccessory item,InventorySystem bag)=>AccessoryDescription(item.Definition,0,bag);
        public static string AccessoryDescription(AccessoryDefinition d,int ignoredLegacyLevel,InventorySystem bag)
            =>d.Description.Replace("원본 공격","직접 공격").Replace("원본 적중","직접 적중").Replace("적중 효과 재발동 없음.","장신구끼리 연쇄 발동하지 않음.");
    }
}
