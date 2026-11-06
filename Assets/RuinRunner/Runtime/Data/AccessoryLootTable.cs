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
            new AccessoryDefinition("berserker_emblem", AccessoryType.BerserkerEmblem, "광전사의 문장", "공격력 +10%\n같은 적을 3번 때리면 출혈: 0.6초마다 그 공격 피해의 12%를 3번 줍니다.\n보스에게도 통합니다. 출혈은 겹치지 않고 시간만 새로 갱신됩니다.\n같은 장신구 2개 15% · 3개 18%", WeaponRarity.Epic, ItemSetType.BloodMoon, new Color(.75f, .3f, 1f)),
            new AccessoryDefinition("sage_sigil", AccessoryType.SageSigil, "대현자의 문양", "마나 재생 +1, 무기 스킬 피해 +10%\n마나를 쓰면 2.4초 동안 주위에 마력구 2개가 돌며 적 4마리까지 한 번씩 때립니다.\n피해: 무기 스킬의 24% · 재사용 6초\n같은 장신구 2개 28% · 3개 32%", WeaponRarity.Legendary, ItemSetType.Arcanist, new Color(1f, .65f, .15f)),
            new AccessoryDefinition("ruby_heart", AccessoryType.RubyHeart, "루비 심장", "최대 체력 +18", WeaponRarity.Common, ItemSetType.BloodMoon, new Color(.8f, .8f, .8f)),
            new AccessoryDefinition("sapphire_flask", AccessoryType.SapphireFlask, "사파이어 플라스크", "최대 마나 +18", WeaponRarity.Common, ItemSetType.Arcanist, new Color(.8f, .8f, .8f)),
            new AccessoryDefinition("guardian_plate", AccessoryType.GuardianPlate, "수호자의 철판", "방어력 +6", WeaponRarity.Uncommon, ItemSetType.Guardian, new Color(.3f, 1f, .5f)),
            new AccessoryDefinition("winged_boots", AccessoryType.WingedBoots, "날개 장화", "이동 속도 +0.55", WeaponRarity.Uncommon, ItemSetType.Storm, new Color(.3f, 1f, .5f)),
            new AccessoryDefinition("assassin_mark", AccessoryType.AssassinMark, "암살자의 표식", "치명타 확률 +6%", WeaponRarity.Rare, ItemSetType.None, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("executioners_coin", AccessoryType.ExecutionersCoin, "처형자의 동전", "치명타 피해 +25%", WeaponRarity.Rare, ItemSetType.None, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("four_leaf_clover", AccessoryType.FourLeafClover, "네잎클로버", "운 +3", WeaponRarity.Rare, ItemSetType.None, new Color(.3f, .6f, 1f)),
            new AccessoryDefinition("arcane_lens", AccessoryType.ArcaneLens, "비전 렌즈", "무기 스킬 피해 +18%\n무기 스킬을 쓴 자리에 렌즈가 2초 남습니다. 그동안 적을 때리면 렌즈에서 그쪽으로 빛이 뻗습니다.\n피해 30% · 사거리 3칸 · 재사용 1.2초", WeaponRarity.Epic, ItemSetType.None, new Color(.75f, .3f, 1f)),
            new AccessoryDefinition("vampire_fang", AccessoryType.VampireFang, "흡혈귀의 송곳니", "처치 시 체력 1.5 회복\n적을 처치하거나 6번 때리면 체력 조각이 떨어집니다. 가까이 가면 체력 2 회복.\n조각은 8초 동안 남고 최대 3개 · 재사용 2초\n같은 장신구 2개 2.5 · 3개 3", WeaponRarity.Epic, ItemSetType.BloodMoon, new Color(.75f, .3f, 1f)),
            new AccessoryDefinition("soul_capacitor", AccessoryType.SoulCapacitor, "영혼 축전기", "마나 재생 +1.2, 처치 시 마나 +2\n마나를 쓰면 앞쪽 적에게 번개가 떨어져 최대 3마리까지 이어집니다.\n피해: 무기 스킬의 35%, 이어질 때마다 20% 감소 · 재사용 3초\n같은 장신구 2개 40% · 3개 45%", WeaponRarity.Legendary, ItemSetType.Arcanist, new Color(1f, .65f, .15f)),
            new AccessoryDefinition("mana_thread", AccessoryType.ManaThread, "마나를 담는 잔", "최대 마나 +6\n공격을 4번 맞히면 마나 4 회복.\n산탄이나 여러 적을 한 번에 맞혀도 공격 한 번으로 셉니다.\n재사용 1.5초 · 같은 장신구 2개 5 · 3개 6", WeaponRarity.Common, ItemSetType.Arcanist, new Color(.8f,.8f,.8f)),
            new AccessoryDefinition("guard_bell", AccessoryType.GuardBell, "수호의 고리", "방어력 +2\n가드나 패링에 성공하면 마나 4 회복.\n재사용 4초 · 같은 장신구 2개 5 · 3개 6", WeaponRarity.Uncommon, ItemSetType.Guardian, new Color(.3f,1f,.5f)),
            new AccessoryDefinition("rush_spur", AccessoryType.RushSpur, "돌진자의 군화", "이동 속도 +0.15\n대시 후 2초 안에 처음 맞힌 적을 강하게 밀치고 그 공격 피해의 30%를 더 줍니다.\n재사용 3초 · 같은 장신구 2개 35% · 3개 40%", WeaponRarity.Uncommon, ItemSetType.Storm, new Color(.3f,1f,.5f)),
            new AccessoryDefinition("steady_locket", AccessoryType.SteadyLocket, "정조준 목걸이", "치명타 확률 +2%\n0.75초 동안 가만히 서 있으면 다음 공격이 처음 맞힌 적에게 피해 35%를 더 줍니다.\n움직이거나 대시하면 풀립니다.\n재사용 2초 · 같은 장신구 2개 40% · 3개 45%", WeaponRarity.Rare, ItemSetType.None, new Color(.3f,.6f,1f)),
            new AccessoryDefinition("powder_satchel", AccessoryType.PowderSatchel, "마지막 탄피의 화약병", "무기 스킬 피해 +4%\n빈 탄창을 끝까지 장전하면 충격파가 터져 가까운 적 3마리를 밀어냅니다.\n피해: 무기 스킬의 22% · 범위 1.8칸 · 재사용 4초\n같은 장신구 2개 26% · 3개 30%", WeaponRarity.Epic, ItemSetType.None, new Color(.75f,.3f,1f)),
            new AccessoryDefinition("last_light_pendant", AccessoryType.LastLightPendant, "마지막 불빛", "최대 체력 +8\n체력이 35% 이하일 때 맞으면 3초 동안 피해를 10 막는 보호막이 생깁니다.\n재사용 10초 · 같은 장신구 2개 12 · 3개 14", WeaponRarity.Legendary, ItemSetType.Guardian, new Color(1f,.65f,.15f)),
            new AccessoryDefinition("recall_spool",AccessoryType.RecallSpool,"되감는 실타래","콤보 마지막 공격의 궤적이 2초 동안 남습니다.\n그동안 무기 스킬을 쓰면 궤적이 되돌아오며 피해 42%를 줍니다. 재사용 3초",WeaponRarity.Epic,ItemSetType.BloodMoon,new Color(.75f,.3f,1)),
            new AccessoryDefinition("cadence_clasp",AccessoryType.CadenceClasp,"박자의 걸쇠","3초 안에 공격 적중 → 대시 → 무기 스킬을 이으면\n앞쪽 적을 밀어내고 마나를 최대 6 회복합니다. 재사용 4초",WeaponRarity.Epic,ItemSetType.Storm,new Color(.75f,.3f,1)),
            new AccessoryDefinition("prism_cage",AccessoryType.PrismCage,"굴절 포획함","무기 스킬을 쓴 뒤 0.6초 동안 앞에서 오는 탄환을 3개까지 붙잡습니다.\n다음 공격을 맞히면 붙잡은 탄환을 부채꼴로 되쏩니다. 재사용 4초",WeaponRarity.Legendary,ItemSetType.Guardian,new Color(1,.65f,.15f)),
            new AccessoryDefinition("trail_spindle",AccessoryType.TrailSpindle,"길짜는 물레","걸어가면 1.25칸마다 표식이 남습니다.\n표식 3개가 모이면 다음 공격이 표식을 이어 피해 35%를 줍니다. 재사용 3초",WeaponRarity.Rare,ItemSetType.Storm,new Color(.3f,.6f,1)),
            new AccessoryDefinition("focus_prism",AccessoryType.FocusPrism,"집중의 프리즘","같은 적을 3번 공격한 뒤 5칸 안의 다른 적을 맞히면\n두 적이 이어져 첫 적에게 피해 55%를 줍니다. 재사용 3초",WeaponRarity.Epic,ItemSetType.Arcanist,new Color(.75f,.3f,1)),
            new AccessoryDefinition("mana_aegis",AccessoryType.ManaAegis,"마나 방벽","맞을 때 마나 12를 써서 피해를 최대 10 막습니다.\n마나가 부족하면 발동하지 않습니다. 재사용 5초",WeaponRarity.Rare,ItemSetType.Guardian,new Color(.3f,.6f,1)),
            new AccessoryDefinition("retribution_thorn",AccessoryType.RetributionThorn,"보복의 가시","방어력 +{armor}\n맞으면 가까운 적 {effect.targetCount}마리의 발밑에서 가시가 솟아 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Common,ItemSetType.None,new Color(.8f,.8f,.8f)),
            new AccessoryDefinition("mirror_shard",AccessoryType.MirrorShard,"거울 조각","치명타 확률 +1%\n공격을 {effect.hitCount}번 맞히면 무기 끝에서 빛의 창이 뻗어 나가 일직선의 적에게 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n길이 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Common,ItemSetType.None,new Color(.8f,.8f,.8f)),
            new AccessoryDefinition("cinder_seed",AccessoryType.CinderSeed,"불씨 씨앗","적을 처치하면 쓰러진 자리에서 불기둥이 솟아 주변에 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Common,ItemSetType.None,new Color(.8f,.8f,.8f)),
            new AccessoryDefinition("thunder_charm",AccessoryType.ThunderCharm,"번개 부적","치명타 확률 +1%\n공격을 {effect.hitCount}번 맞히면 맞은 적에게 번개가 떨어져 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Uncommon,ItemSetType.None,new Color(.3f,1f,.5f)),
            new AccessoryDefinition("poison_censer",AccessoryType.PoisonCenser,"독 향로","공격을 {effect.hitCount}번 맞히면 맞은 적의 발밑에 독 웅덩이가 {effect.seconds}초 동안 고입니다.\n안에 있는 적은 0.5초마다 공격력의 {effect.damageFraction:p}% 피해를 받습니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Uncommon,ItemSetType.None,new Color(.3f,1f,.5f)),
            new AccessoryDefinition("chain_spark",AccessoryType.ChainSpark,"불똥 사슬","공격을 {effect.hitCount}번 맞히면 맞은 적에서 전기가 튀어 최대 {effect.targetCount}마리에게 이어집니다.\n피해: 공격력의 {effect.damageFraction:p}%, 튈 때마다 20% 감소 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Uncommon,ItemSetType.None,new Color(.3f,1f,.5f)),
            new AccessoryDefinition("soul_lantern",AccessoryType.SoulLantern,"넋 등불","처치 시 마나 +{manaOnKill}\n적을 처치하면 넋이 빠져나와 가장 가까운 적에게 날아가 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n탐지 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Uncommon,ItemSetType.None,new Color(.3f,1f,.5f)),
            new AccessoryDefinition("vine_snare",AccessoryType.VineSnare,"덩굴 올가미","같은 적을 {effect.hitCount}번 맞히면 덩굴이 {effect.seconds}초 동안 묶고 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n보스는 묶이는 대신 느려집니다. 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Uncommon,ItemSetType.None,new Color(.3f,1f,.5f)),
            new AccessoryDefinition("shadow_twin_ring",AccessoryType.ShadowTwinRing,"그림자 쌍가락지","치명타 피해 +5%\n공격을 {effect.hitCount}번 맞히면 그림자 칼날이 맞은 적과 근처의 적을 합쳐 {effect.targetCount}마리까지 다시 벱니다.\n피해: 공격력의 {effect.damageFraction:p}% · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Rare,ItemSetType.None,new Color(.3f,.6f,1f)),
            new AccessoryDefinition("quake_bell",AccessoryType.QuakeBell,"진동 종","공격을 {effect.hitCount}번 맞히면 맞은 적의 자리에 충격파가 터져 주변을 {effect.seconds}초 동안 멈춥니다.\n피해: 공격력의 {effect.damageFraction:p}% · 범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Rare,ItemSetType.None,new Color(.3f,.6f,1f)),
            new AccessoryDefinition("frost_wave_necklace",AccessoryType.FrostWaveNecklace,"서리 물결 목걸이","이동 속도 +{moveSpeed}\n대시하면 떠난 자리에 서리가 터져 주변 적에게 공격력의 {effect.damageFraction:p}% 피해를 주고 {effect.seconds}초 동안 느리게 합니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Rare,ItemSetType.None,new Color(.3f,.6f,1f)),
            new AccessoryDefinition("guardian_feather",AccessoryType.GuardianFeather,"수호 깃털","방어력 +{armor}\n가드나 패링에 성공하면 앞쪽으로 깃털이 흩날려 적을 밀어내고 공격력의 {effect.damageFraction:p}% 피해를 줍니다. 체력 {effect.heal} 회복.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Rare,ItemSetType.None,new Color(.3f,.6f,1f)),
            new AccessoryDefinition("blood_pact",AccessoryType.BloodPact,"피의 서약","최대 체력 +{maxHealth}\n체력이 {effect.healthThreshold:p}% 이하일 때 공격을 {effect.hitCount}번 맞히면 맞은 적의 피를 빨아 공격력의 {effect.damageFraction:p}% 피해를 주고 체력 {effect.heal} 회복.\n재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Rare,ItemSetType.None,new Color(.3f,.6f,1f)),
            new AccessoryDefinition("swap_flash",AccessoryType.SwapFlash,"교체 섬광","무기를 바꾸면 앞쪽에서 마력이 터져 적을 밀어내고 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Rare,ItemSetType.None,new Color(.3f,.6f,1f)),
            new AccessoryDefinition("meteor_compass",AccessoryType.MeteorCompass,"유성 나침반","무기 스킬 피해 +{specialDamageBonus:p}%\n무기 스킬을 쓰면 가까운 적 {effect.targetCount}마리에게 유성이 떨어져 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n탐지 {effect.range}칸 · 폭발 {effect.radius}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Epic,ItemSetType.None,new Color(.75f,.3f,1f)),
            new AccessoryDefinition("gravity_pearl",AccessoryType.GravityPearl,"중력 진주","공격을 {effect.hitCount}번 맞히면 맞은 적의 자리에 작은 블랙홀이 열려 주변 적을 끌어모은 뒤 터집니다.\n피해: 공격력의 {effect.damageFraction:p}% · 범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Epic,ItemSetType.None,new Color(.75f,.3f,1f)),
            new AccessoryDefinition("time_hourglass",AccessoryType.TimeHourglass,"시간의 모래시계","마나 재생 +{manaRegeneration}\n무기 스킬을 쓰면 앞쪽의 시간이 느려져 적이 {effect.seconds}초 동안 거의 움직이지 못하고 공격력의 {effect.damageFraction:p}% 피해를 받습니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Epic,ItemSetType.None,new Color(.75f,.3f,1f)),
            new AccessoryDefinition("holy_tithe",AccessoryType.HolyTithe,"신성한 십일조","처치 시 체력 +{lifeOnKill}\n적을 {effect.killCount}마리 처치할 때마다 가장 가까운 적에게 심판의 빛이 내려 공격력의 {effect.damageFraction:p}% 피해를 주고 체력 {effect.heal} 회복.\n탐지 {effect.range}칸 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Epic,ItemSetType.None,new Color(.75f,.3f,1f)),
            new AccessoryDefinition("storm_core",AccessoryType.StormCore,"폭풍의 핵","마나 재생 +{manaRegeneration}, 공격력 +{weaponDamageBonus:p}%\n{effect.cooldownSeconds}초마다 가까운 적 {effect.targetCount}마리에게 벼락이 저절로 떨어져 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n탐지 {effect.range}칸 · 장신구 하나 더마다 +{effect.extraCopyDamageFraction:p}%",WeaponRarity.Legendary,ItemSetType.None,new Color(1f,.65f,.15f)),
            new AccessoryDefinition("phoenix_ash",AccessoryType.PhoenixAsh,"불사조의 재","최대 체력 +{maxHealth}\n쓰러질 피해를 받으면 재에서 되살아나 최대 체력의 {effect.healFraction:p}%를 회복하고, 앞쪽에 폭발을 일으켜 공격력의 {effect.damageFraction:p}% 피해를 줍니다.\n범위 {effect.range}칸 · 재사용 {effect.cooldownSeconds}초 · 하나만 효과가 있습니다.",WeaponRarity.Legendary,ItemSetType.None,new Color(1f,.65f,.15f))
        };

        // Every accessory this build has mechanics for. An older pinned catalog lists fewer; a later binding can still name the rest.
        private static readonly AccessoryDefinition[] Known = Accessories;

        public static IReadOnlyList<AccessoryDefinition> All => Accessories;

        public static int Count => Accessories.Length;
        public static AccessoryDefinition Find(AccessoryType type) => System.Array.Find(Accessories, item => item.Type == type) ?? System.Array.Find(Known, item => item.Type == type);
        public static AccessoryDefinition Find(string id) => System.Array.Find(Accessories, item => item.Id == id) ?? System.Array.Find(Known, item => item.Id == id);

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
