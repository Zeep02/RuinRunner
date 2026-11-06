using System;
using System.Collections.Generic;

namespace RuinRunner
{
    // Published IDs are persistence contracts; enum ordinals are never recycled.
    public enum TalentId
    {
        ComboMemory = 1, StoredGuard = 2, CrossChamber = 3, TurningDash = 4, BrakeDash = 5, ReturnDash = 6, SurveyNext = 7, SupplyChoice = 8, ReturnTrail = 9, ChestIntent = 10, MerchantBarter = 11, ReserveOffer = 12,
        GlassBlade = 13, LastStand = 14, CorpseBurst = 15, BloodPact = 16, VoidRush = 17, ChainChant = 18, GhostMine = 19, RazorWind = 20, CloseCall = 21, Miser = 22, Revenant = 23, Gambler = 24,
        ExecutionerEdge = 25, FirstBlood = 26, Bloodthirst = 27, FullCup = 28, ManaHeart = 29, HunterBreath = 30, DoubleStep = 31, Momentum = 32, PhantomStep = 33, CursedPower = 34, Duelist = 35, LateBloom = 36
    }
    public enum TalentFamily { Blade, Mana, Step, Fate }
    public sealed class TalentDefinition
    {
        public readonly TalentId Type;
        public readonly string Id, Name, Summary;
        public readonly TalentFamily Family;
        public TalentDefinition(TalentId type, string id, string name, TalentFamily family, string summary)
        {
            Type = type;
            Id = id;
            Name = name;
            Family = family;
            Summary = summary;
        }
    }
    public sealed class MasteryTrack
    {
        public readonly string Id, Name, Effect;
        public MasteryTrack(string id, string name, string effect)
        {
            Id = id;
            Name = name;
            Effect = effect;
        }
    }
    public static class TalentCatalog
    {
        public const int PerFamily = 6, MasteryRanks = 5;
        // Published costs live in the server catalog; this is what the tree displays as the next price.
        public static readonly int[] MasteryCosts = { 2, 3, 5, 8, 12 };
        public static readonly MasteryTrack[] Mastery ={
            new MasteryTrack("vigor","강인함","최대 체력 +8"),
            new MasteryTrack("focus","집중","최대 마나 +8"),
            new MasteryTrack("edge","예기","공격력 +4%"),
            new MasteryTrack("sorcery","비술","스킬 피해 +5%"),
            new MasteryTrack("precision","정밀","치명타 확률 +2%"),
            new MasteryTrack("ferocity","맹위","치명타 피해 +8%"),
            new MasteryTrack("swift","질주","이동 속도 +0.12"),
            new MasteryTrack("wisdom","깨달음","경험치 +8%")
        };
        public static string MasteryId(MasteryTrack track, int rank) => "mastery_" + track.Id + "_" + rank;
        public static int MasteryRank(string[] unlocked, MasteryTrack track)
        {
            int rank = 0;
            if (unlocked == null)
                return 0;
            while (rank < MasteryRanks && Array.IndexOf(unlocked, MasteryId(track, rank + 1)) >= 0)
                rank++;
            return rank;
        }
        public static readonly TalentDefinition[] All ={
            new TalentDefinition(TalentId.GlassBlade,"glass_blade","유리 칼날",TalentFamily.Blade,"주는 피해 +60%.\n최대 체력이 절반이 됩니다."),
            new TalentDefinition(TalentId.LastStand,"last_stand","벼랑 끝",TalentFamily.Blade,"잃은 체력만큼 피해가 늘어 빈사일 때 +120%.\n회복량은 절반이 됩니다."),
            new TalentDefinition(TalentId.CorpseBurst,"corpse_burst","시체 폭죽",TalentFamily.Blade,"처치한 적이 터져 주변을 휩쓸고, 그 폭발에 죽은 적도 터집니다."),
            new TalentDefinition(TalentId.ExecutionerEdge,"executioner_edge","처형인의 날",TalentFamily.Blade,"체력 30% 이하인 적에게 주는 피해 +60%."),
            new TalentDefinition(TalentId.FirstBlood,"first_blood","첫 칼",TalentFamily.Blade,"체력이 가득한 적에게 주는 피해 +100%."),
            new TalentDefinition(TalentId.Bloodthirst,"bloodthirst","피의 갈증",TalentFamily.Blade,"처치할 때마다 5초간 피해 +8%, 5번까지.\n맞으면 전부 사라집니다."),
            new TalentDefinition(TalentId.BloodPact,"blood_pact","피의 계약",TalentFamily.Mana,"마나가 모자라면 부족한 만큼 체력을 치르고 스킬을 씁니다."),
            new TalentDefinition(TalentId.VoidRush,"void_rush","공허 폭주",TalentFamily.Mana,"마나가 바닥나면 4초간 스킬이 공짜입니다.\n끝나면 6초간 마나가 차오르지 않습니다."),
            new TalentDefinition(TalentId.ChainChant,"chain_chant","연쇄 영창",TalentFamily.Mana,"2초 안에 스킬을 이을 때마다 스킬 피해와 마나 소모가 함께 20%씩 늘어납니다. 5번까지."),
            new TalentDefinition(TalentId.FullCup,"full_cup","넘치는 잔",TalentFamily.Mana,"마나가 가득할 때 쓴 스킬의 피해 +80%."),
            new TalentDefinition(TalentId.ManaHeart,"mana_heart","마나 심장",TalentFamily.Mana,"최대 마나 +75%.\n마나 재생이 절반이 됩니다."),
            new TalentDefinition(TalentId.HunterBreath,"hunter_breath","사냥꾼의 호흡",TalentFamily.Mana,"처치할 때마다 마나 10을 얻습니다.\n전투 중에는 마나가 저절로 차지 않습니다."),
            new TalentDefinition(TalentId.GhostMine,"ghost_mine","터지는 잔상",TalentFamily.Step,"대시를 시작한 자리에 잔상이 남았다가 터집니다."),
            new TalentDefinition(TalentId.RazorWind,"razor_wind","칼바람",TalentFamily.Step,"대시로 스친 적을 모두 베고, 벤 적 하나당 마나 4를 얻습니다."),
            new TalentDefinition(TalentId.CloseCall,"close_call","종이 한 장",TalentFamily.Step,"대시로 공격을 피하면 시간이 잠깐 느려지고 마나 15를 얻으며, 3초간 피해 +50%."),
            new TalentDefinition(TalentId.DoubleStep,"double_step","세 번째 걸음",TalentFamily.Step,"대시를 한 번 더 모아 둘 수 있습니다."),
            new TalentDefinition(TalentId.Momentum,"momentum","가속",TalentFamily.Step,"움직이는 동안 주는 피해 +25%."),
            new TalentDefinition(TalentId.PhantomStep,"phantom_step","유령 걸음",TalentFamily.Step,"대시 무적 시간이 두 배가 됩니다.\n대시 충전은 30% 느려집니다."),
            new TalentDefinition(TalentId.Miser,"miser","수전노",TalentFamily.Fate,"가진 골드 5당 피해 +1%.\n쓰면 그만큼 약해집니다."),
            new TalentDefinition(TalentId.Revenant,"revenant","망령의 약속",TalentFamily.Fate,"원정 중 한 번, 죽는 순간 되살아나며 주변을 날려 버립니다.\n이후 최대 체력 -40%."),
            new TalentDefinition(TalentId.Gambler,"gambler","도박꾼의 손",TalentFamily.Fate,"공격과 스킬마다 피해가 0.4배에서 2.2배 사이로 갈립니다."),
            new TalentDefinition(TalentId.CursedPower,"cursed_power","저주받은 재능",TalentFamily.Fate,"처치 경험치가 두 배가 됩니다.\n주는 피해 -25%."),
            new TalentDefinition(TalentId.Duelist,"duelist","결투가",TalentFamily.Fate,"가까운 적이 하나면 피해 +70%.\n셋 이상이면 -20%."),
            new TalentDefinition(TalentId.LateBloom,"late_bloom","대기만성",TalentFamily.Fate,"레벨 1에서는 피해 -20%.\n레벨이 오를 때마다 +6%.")
        };
        public static TalentDefinition Find(string id)
        {
            foreach (var t in All)
                if (t.Id == id)
                    return t;
            return null;
        }
        public static TalentDefinition Find(TalentId id)
        {
            foreach (var t in All)
                if (t.Type == id)
                    return t;
            return null;
        }
    }
    public sealed class TalentLoadout
    {
        readonly HashSet<TalentId> active = new HashSet<TalentId>();
        public const int MaximumActive = 4;
        public bool Has(TalentId id) => active.Contains(id);
        public string[] Ids
        {
            get
            {
                var result = new List<string>();
                foreach (var t in TalentCatalog.All)
                    if (Has(t.Type))
                        result.Add(t.Id);
                return result.ToArray();
            }
        }
        public TalentLoadout(IEnumerable<string> ids = null)
        {
            if (ids == null)
                return;
            var families = new HashSet<TalentFamily>();
            foreach (var id in ids)
            {
                var t = TalentCatalog.Find(id);
                if (t == null || active.Count >= MaximumActive || !families.Add(t.Family))
                    continue;
                active.Add(t.Type);
            }
        }
    }
}
