using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public static partial class AccessoryLootTable
    {
        private static AccessoryDefinition[] Accessories =
        {
            new AccessoryDefinition("lucky_dice", AccessoryType.LuckyDice, "금 간 행운의 주사위", "운 +2", WeaponRarity.Common, ItemSetType.None, new Color(.8f, .8f, .8f)),
            new AccessoryDefinition("storm_feather", AccessoryType.StormFeather, "폭풍깃", "이동 속도 +0.4", WeaponRarity.Uncommon, ItemSetType.Storm, new Color(.3f, 1f, .5f)),
            new AccessoryDefinition("guardian_seal", AccessoryType.GuardianSeal, "수호자의 인장", "방어력 +5", WeaponRarity.Uncommon, ItemSetType.Guardian, new Color(.3f, 1f, .5f)),
            new AccessoryDefinition("crimson_ring", AccessoryType.CrimsonRing, "진홍 반지", "치명타 확률 +5%", WeaponRarity.Rare, ItemSetType.BloodMoon, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("mana_prism", AccessoryType.ManaPrism, "마나 프리즘", "최대 마나 +14", WeaponRarity.Rare, ItemSetType.Arcanist, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("bulwark_brooch", AccessoryType.BulwarkBrooch, "성벽 브로치", "최대 체력 +16", WeaponRarity.Rare, ItemSetType.Guardian, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("berserker_emblem", AccessoryType.BerserkerEmblem, "광전사의 문장", "공격력 +10%\n같은 적에 원본 공격 3회 적중: 0.6초마다 해당 공격 피해의 12% 출혈, 3회. 최대 12표식, 보스도 적용. 중복 3개까지 12/15/18%, 출혈 중첩 없이 갱신. 추가 피해는 원본 적중으로 계산하지 않음.", WeaponRarity.Epic, ItemSetType.BloodMoon, new Color(.75f, .3f, 1f)),
            new AccessoryDefinition("sage_sigil", AccessoryType.SageSigil, "대현자의 문양", "마나 재생 +1, 특수공격 피해 +10%\n마나 소비: 2.4초간 반경 1.15의 마력구 2개, 최대 4적에 각 1회. 특수 기준 피해 24%, 쿨다운 6초 공유. 중복 3개까지 24/28/32%. 벽 차단·보스 적용·적중 효과 재발동 없음.", WeaponRarity.Legendary, ItemSetType.Arcanist, new Color(1f, .65f, .15f)),
            new AccessoryDefinition("ruby_heart", AccessoryType.RubyHeart, "루비 심장", "최대 체력 +18", WeaponRarity.Common, ItemSetType.BloodMoon, new Color(.8f, .8f, .8f)),
            new AccessoryDefinition("sapphire_flask", AccessoryType.SapphireFlask, "사파이어 플라스크", "최대 마나 +18", WeaponRarity.Common, ItemSetType.Arcanist, new Color(.8f, .8f, .8f)),
            new AccessoryDefinition("guardian_plate", AccessoryType.GuardianPlate, "수호자의 철판", "방어력 +6", WeaponRarity.Uncommon, ItemSetType.Guardian, new Color(.3f, 1f, .5f)),
            new AccessoryDefinition("winged_boots", AccessoryType.WingedBoots, "날개 장화", "이동 속도 +0.55", WeaponRarity.Uncommon, ItemSetType.Storm, new Color(.3f, 1f, .5f)),
            new AccessoryDefinition("assassin_mark", AccessoryType.AssassinMark, "암살자의 표식", "치명타 확률 +6%", WeaponRarity.Rare, ItemSetType.None, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("executioners_coin", AccessoryType.ExecutionersCoin, "처형자의 동전", "치명타 피해 +25%", WeaponRarity.Rare, ItemSetType.None, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("four_leaf_clover", AccessoryType.FourLeafClover, "네잎클로버", "운 +3", WeaponRarity.Rare, ItemSetType.None, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("arcane_lens", AccessoryType.ArcaneLens, "비전 렌즈", "특수공격 피해 +18%\n특수행동 지점에 2초 동안 렌즈를 남긴다. 다음 직접 적중 시 렌즈에서 목표 방향 3칸으로 메아리. 피해 30%, 재사용 1.2초. 벽 차단·보스 적용·추가 효과 재발동 없음.", WeaponRarity.Epic, ItemSetType.None, new Color(.75f, .3f, 1f)),
            new AccessoryDefinition("vampire_fang", AccessoryType.VampireFang, "흡혈귀의 송곳니", "처치 시 체력 1.5 회복\n처치 또는 원본 적중 6회: 체력 2의 회수 파편, 8초 유지·최대 3개. 쿨다운 2초 공유, 0.65칸 접근 시 회수. 중복 3개까지 2/2.5/3 회복. 보스는 적중으로 생성.", WeaponRarity.Epic, ItemSetType.BloodMoon, new Color(.75f, .3f, 1f)),
            new AccessoryDefinition("soul_capacitor", AccessoryType.SoulCapacitor, "영혼 축전기", "마나 재생 +1.2, 처치 시 마나 +2\n마나 소비: 전방 4칸부터 최대 3적 연쇄 번개, 다음 간격 2.5칸. 특수 기준 피해 35%, 다음 적마다 ×0.8. 쿨다운 3초 공유. 중복 3개까지 35/40/45%. 벽 차단·보스 적용·적중 효과 재발동 없음.", WeaponRarity.Legendary, ItemSetType.Arcanist, new Color(1f, .65f, .15f)),
            new AccessoryDefinition("mana_thread", AccessoryType.ManaThread, "마나를 담는 잔", "최대 마나 +6\n공격 4회 적중하면 마나 4 회복.\n산탄·연사·여러 적 적중도 공격당 1회만 센다.\n재사용 1.5초 · 중복 3개까지 회복 4/5/6.", WeaponRarity.Common, ItemSetType.Arcanist, new Color(.8f,.8f,.8f)),
            new AccessoryDefinition("guard_bell", AccessoryType.GuardBell, "수호의 고리", "방어력 +2\n가드 또는 패링 성공 시 마나 4 회복.\n막기 타이밍을 다음 특수 공격으로 연결한다.\n재사용 4초 · 중복 3개까지 회복 4/5/6.", WeaponRarity.Uncommon, ItemSetType.Guardian, new Color(.3f,1f,.5f)),
            new AccessoryDefinition("rush_spur", AccessoryType.RushSpur, "돌진자의 군화", "이동 속도 +0.15\n대시 후 2초 안에 처음 맞힌 적을 강하게 밀친다.\n해당 공격 피해의 30%를 추가로 가한다.\n재사용 3초 · 중복 3개까지 30/35/40%.\n벽 너머 발동 불가 · 보스도 적용.", WeaponRarity.Uncommon, ItemSetType.Storm, new Color(.3f,1f,.5f)),
            new AccessoryDefinition("steady_locket", AccessoryType.SteadyLocket, "정조준 목걸이", "치명타 확률 +2%\n0.75초 동안 이동하지 않으면 다음 적중이 강화된다.\n처음 맞힌 1적에게 해당 공격 피해의 35% 추가.\n이동·대시하면 준비가 풀린다.\n재사용 2초 · 중복 3개까지 35/40/45%.", WeaponRarity.Rare, ItemSetType.None, new Color(.3f,.6f,1f)),
            new AccessoryDefinition("powder_satchel", AccessoryType.PowderSatchel, "마지막 탄피의 화약병", "특수공격 피해 +4%\n장착한 무기의 빈 탄창을 장전 완료하면 충격파 발생.\n1.8칸 안의 보이는 적 최대 3명에게 특수 기준 피해 22%.\n적을 밀쳐 다음 사격 공간을 확보한다.\n재사용 4초 · 중복 3개까지 22/26/30%.\n일부 탄약만 채우거나 다른 슬롯 장전은 발동하지 않는다.", WeaponRarity.Epic, ItemSetType.None, new Color(.75f,.3f,1f)),
            new AccessoryDefinition("last_light_pendant", AccessoryType.LastLightPendant, "마지막 불빛", "최대 체력 +8\n체력이 35% 이하일 때 피격을 보호막 10으로 받는다.\n보호막은 3초 유지하며 소모량만큼 피해를 막는다.\n재사용 10초 · 중복 3개까지 보호막 10/12/14.\n사망 후 부활 효과는 없다.", WeaponRarity.Legendary, ItemSetType.Guardian, new Color(1f,.65f,.15f)),
            new AccessoryDefinition("recall_spool",AccessoryType.RecallSpool,"되감는 실타래","기본 마무리 궤적을 2초 남긴다.\n다음 특수행동이 궤적을 회수해 피해 42%. 재사용 3초.",WeaponRarity.Epic,ItemSetType.BloodMoon,new Color(.75f,.3f,1)),
            new AccessoryDefinition("cadence_clasp",AccessoryType.CadenceClasp,"박자의 걸쇠","3초 안에 명중 → 대시 → 특수를 잇는다.\n전방을 밀어내고 마나 최대 6 회복. 재사용 4초.",WeaponRarity.Epic,ItemSetType.Storm,new Color(.75f,.3f,1)),
            new AccessoryDefinition("prism_cage",AccessoryType.PrismCage,"굴절 포획함","특수행동 후 0.6초 동안 전방 탄환 3개까지 포획.\n다음 공격 명중 때 부채꼴로 반환. 재사용 4초.",WeaponRarity.Legendary,ItemSetType.Guardian,new Color(1,.65f,.15f)),
            new AccessoryDefinition("trail_spindle",AccessoryType.TrailSpindle,"길짜는 물레","걸으며 남긴 표식 셋을 다음 공격으로 잇는다.\n표식 간격 1.25칸 · 연결 피해 35% · 재사용 3초.",WeaponRarity.Rare,ItemSetType.Storm,new Color(.3f,.6f,1)),
            new AccessoryDefinition("focus_prism",AccessoryType.FocusPrism,"집중의 프리즘","같은 적을 세 번 공격한 뒤 다른 적을 노린다.\n첫 적에게 연결 피해 55%. 연결 5칸 · 재사용 3초.",WeaponRarity.Epic,ItemSetType.Arcanist,new Color(.75f,.3f,1)),
            new AccessoryDefinition("mana_aegis",AccessoryType.ManaAegis,"마나 방벽","피격 시 마나 12를 피해 최대 10 방어로 바꾼다.\n마나가 부족하면 발동하지 않는다. 재사용 5초.",WeaponRarity.Rare,ItemSetType.Guardian,new Color(.3f,.6f,1))
        };

        public static IReadOnlyList<AccessoryDefinition> All => Accessories;

        public static int Count => Accessories.Length;
        public static AccessoryDefinition Find(AccessoryType type) => System.Array.Find(Accessories, item => item.Type == type);
        public static AccessoryDefinition Find(string id) => System.Array.Find(Accessories, item => item.Id == id);

        public static AccessoryDefinition[] CreateChoices(int count, float luck)
        {
            var remaining = EligibleAccessories();
            var choices = new List<AccessoryDefinition>();
            count = Mathf.Clamp(count, 0, remaining.Count);
            while (choices.Count < count)
            {
                if(!string.IsNullOrEmpty(PinnedCatalogVersion))
                {var direct=WeightedAccessory(remaining,luck);choices.Add(direct);remaining.Remove(direct);continue;}
                var rarity = WeaponLootTable.RollRarity(luck);
                // Choose the closest available tier when the rolled tier is exhausted.
                var candidates = new List<AccessoryDefinition>();
                var nearest = int.MaxValue;
                foreach (var item in remaining)
                {
                    var distance = Mathf.Abs((int)item.Rarity - (int)rarity);
                    if (distance < nearest) { candidates.Clear(); nearest = distance; }
                    if (distance == nearest) candidates.Add(item);
                }
                var chosen = WeightedAccessory(candidates);
                choices.Add(chosen);
                remaining.Remove(chosen);
            }
            // Do not place the best tier in a predictable slot.
            for (var i = choices.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                var item = choices[i]; choices[i] = choices[j]; choices[j] = item;
            }
            return choices.ToArray();
        }

        public static AccessoryDefinition Roll(float luck)
        {
            if(!string.IsNullOrEmpty(PinnedCatalogVersion))return WeightedAccessory(EligibleAccessories(),luck);
            var rarity = WeaponLootTable.RollRarity(luck);
            var candidates = new List<AccessoryDefinition>();
            foreach (var accessory in Accessories)
                if (accessory.Rarity == rarity) candidates.Add(accessory);

            if (candidates.Count == 0) return Accessories[0];
            return WeightedAccessory(candidates);
        }
    }
}
