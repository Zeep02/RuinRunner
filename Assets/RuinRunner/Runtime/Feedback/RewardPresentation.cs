using UnityEngine;

namespace RuinRunner
{
    public static class RewardPresentation
    {
        public static WeaponRarity Highest(ChestRewards rewards)
        {
            var grade=rewards.Weapon?.Rarity??WeaponRarity.Common;
            if(rewards.BonusWeapon!=null&&rewards.BonusWeapon.Rarity>grade)grade=rewards.BonusWeapon.Rarity;
            if(rewards.Accessory!=null&&rewards.Accessory.Rarity>grade)grade=rewards.Accessory.Rarity;
            return grade;
        }
        public static string Name(WeaponRarity grade)
        {
            switch(grade){case WeaponRarity.Uncommon:return "고급";case WeaponRarity.Rare:return "희귀";
                case WeaponRarity.Epic:return "에픽";case WeaponRarity.Legendary:return "전설";default:return "일반";}
        }
        public static Color Color(WeaponRarity grade)
        {
            switch(grade){case WeaponRarity.Uncommon:return new Color(.42f,1f,.61f);case WeaponRarity.Rare:return new Color(.39f,.76f,1f);
                case WeaponRarity.Epic:return new Color(.83f,.52f,1f);case WeaponRarity.Legendary:return new Color(1f,.78f,.30f);
                default:return new Color(.86f,.91f,.92f);}
        }
        public static float OpenDuration(WeaponRarity grade)=>.36f+(int)grade*.07f;
        public static float RevealDuration(WeaponRarity grade)=>.46f+(int)grade*.045f;
        public static void Reveal(Vector2 point,WeaponRarity grade,int order,bool chest)
        {
            var color=Color(grade);var size=chest?1.15f:.70f;
            switch(grade)
            {
                case WeaponRarity.Common:
                    NativeCombatEffect.Create("RewardRing",point,0,size,.24f,color,order);break;
                case WeaponRarity.Uncommon:
                    NativeCombatEffect.Create("RewardGlint",point,0,size,.38f,color,order);
                    NativeCombatEffect.Create("RewardSpark",point+Vector2.up*.22f,0,.45f,.48f,color,order+1);break;
                case WeaponRarity.Rare:
                    NativeCombatEffect.Create("RewardGlint",point,0,size,.48f,color,order);
                    NativeCombatEffect.Create("RewardSpark",point+new Vector2(-.28f,.24f),0,.48f,.50f,color,order+1);
                    NativeCombatEffect.Create("RewardSpark",point+new Vector2(.28f,.36f),0,.40f,.55f,color,order+1);break;
                case WeaponRarity.Epic:
                    NativeCombatEffect.Create("RewardGlint",point,0,size*1.15f,.60f,color,order);
                    NativeCombatEffect.Create("RewardSpark",point+Vector2.up*.34f,0,.88f,.60f,color,order+1);break;
                case WeaponRarity.Legendary:
                    NativeCombatEffect.Create("RewardGlint",point,0,size*1.4f,.65f,color,order);
                    NativeCombatEffect.Create("RewardSpark",point+Vector2.up*.48f,0,1.05f,.70f,color,order+1);break;
            }
            if(grade>=WeaponRarity.Epic)
            {
                NativeCombatEffect.Create("RewardSpark",point+new Vector2(-.42f,.14f),-20,.38f,.60f,color,order+1);
                NativeCombatEffect.Create("RewardSpark",point+new Vector2(.42f,.22f),20,.38f,.68f,color,order+1);
                if(grade==WeaponRarity.Legendary)
                    NativeCombatEffect.Create("RewardGlint",point+Vector2.up*.7f,0,.44f,.80f,color,order+1);
            }
            CombatAudio.Native("reward-"+grade.ToString().ToLowerInvariant(),AudioChannel.Inventory,chest?.42f:.15f,1,true);
        }
        public static void Collect(Vector2 point,WeaponRarity grade,int order)
        {
            NativeCombatEffect.Create("RewardGlint",point,0,.70f,.28f,Color(grade),order);
            CombatAudio.Native("reward-pickup",AudioChannel.Inventory,.32f,1,true);
        }
    }
}
